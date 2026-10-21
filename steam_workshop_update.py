# /// script
# requires-python = ">=3.9"
# dependencies = ["websocket-client==1.9.2", "rsa==4.9.1", "pyasn1==0.6.4"]
# ///
"""Updates the tags and description of a Steam Workshop item.

This logs in to a Steam CM server as a Steam client (the same way the Steam client and the
steam-user/steam-session Node libraries do) and sends the PublishedFile.Update#1 service call with
the new tags and description. Nothing is sent when the item is already up to date.

The dependencies are declared inline (PEP 723), so `uv run steam_workshop_update.py` or
`pipx run steam_workshop_update.py` installs them into a temporary environment.

Credentials come from the environment:
    STEAM_USERNAME, STEAM_PASSWORD  the account that owns the Workshop item
    STEAM_TFASEED                   the mobile authenticator's shared_secret (base64 or hex), used
                                    to generate the Steam Guard code; required when the account has
                                    the mobile authenticator enabled

Examples:
    steam_workshop_update.py 2009463077 --versions-from MyMod/About/About.xml
    steam_workshop_update.py 2009463077 --description-from MyMod/SteamDescription.txt
    steam_workshop_update.py 2009463077 --add 1.7 --remove 1.3 --dry-run
    steam_workshop_update.py 2009463077 --check-login
"""

import argparse
import base64
import collections
import gzip
import hashlib
import json
import os
import random
import re
import socket
import struct
import sys
import time
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ElementTree

import rsa
import websocket

from steam_guard import steam_guard_code

RIMWORLD_APPID = 294100
VERSION_TAG = re.compile(r"\d+\.\d+")

WEBAPI_BASE = "https://api.steampowered.com"
HTTP_TIMEOUT = 30
SOCKET_TIMEOUT = 30
SESSION_ATTEMPTS = 3
AUTH_POLL_TIMEOUT = 60
VERIFY_ATTEMPTS = 5
VERIFY_DELAY = 2

PROTOCOL_VERSION = 65580
PROTO_MASK = 0x80000000
JOBID_NONE = 0xFFFFFFFFFFFFFFFF
DEVICE_NAME = "steam_workshop_update.py"

EMSG_MULTI = 1
EMSG_SERVICE_METHOD_RESPONSE = 147
EMSG_SERVICE_METHOD_CALL = 151
EMSG_CLIENT_HEARTBEAT = 703
EMSG_CLIENT_LOG_OFF = 706
EMSG_CLIENT_LOG_ON_RESPONSE = 751
EMSG_CLIENT_LOGGED_OFF = 757
EMSG_CLIENT_LOGON = 5514
EMSG_SERVICE_METHOD_CALL_NON_AUTHED = 9804
EMSG_CLIENT_HELLO = 9805

GUARD_NONE = 1
GUARD_EMAIL_CODE = 2
GUARD_DEVICE_CODE = 3
GUARD_DEVICE_CONFIRMATION = 4
GUARD_EMAIL_CONFIRMATION = 5

PLATFORM_STEAM_CLIENT = 1
SESSION_EPHEMERAL = 0
OS_WIN11 = 20

ERESULT_OK = 1
ERESULT_NAMES = {
    1: "OK",
    2: "Fail",
    3: "NoConnection",
    5: "InvalidPassword",
    8: "InvalidParam",
    9: "FileNotFound",
    10: "Busy",
    15: "AccessDenied",
    16: "Timeout",
    20: "ServiceUnavailable",
    27: "Expired",
    48: "TryAnotherCM",
    63: "AccountLogonDenied",
    65: "InvalidLoginAuthCode",
    84: "RateLimitExceeded",
    85: "AccountLoginDeniedNeedTwoFactor",
    88: "TwoFactorCodeMismatch",
}
TRANSIENT_ERESULTS = {2, 3, 10, 16, 20, 48}
CODE_MISMATCH_ERESULTS = {65, 88}

NETWORK_ERRORS = (OSError, websocket.WebSocketException)


class SteamError(Exception):
    def __init__(self, action, eresult, message=None):
        name = ERESULT_NAMES.get(eresult, "unknown")
        text = f"{action} failed with EResult {eresult} ({name})"
        if message:
            text += f": {message}"
        super().__init__(text)
        self.eresult = eresult


# Protobuf wire format


def _varint(value):
    value &= 0xFFFFFFFFFFFFFFFF
    out = bytearray()
    while True:
        byte = value & 0x7F
        value >>= 7
        if value:
            out.append(byte | 0x80)
        else:
            out.append(byte)
            return bytes(out)


def pb_varint(field, value):
    return _varint(field << 3) + _varint(int(value))


def pb_fixed32(field, value):
    return _varint(field << 3 | 5) + struct.pack("<I", value)


def pb_fixed64(field, value):
    return _varint(field << 3 | 1) + struct.pack("<Q", value)


def pb_bytes(field, value):
    return _varint(field << 3 | 2) + _varint(len(value)) + value


def pb_string(field, value):
    return pb_bytes(field, value.encode("utf-8"))


def _read_varint(data, offset):
    result = 0
    shift = 0
    while True:
        byte = data[offset]
        offset += 1
        result |= (byte & 0x7F) << shift
        if not byte & 0x80:
            return result, offset
        shift += 7


def pb_decode(data):
    """Decodes a protobuf message into {field number: [raw values]}."""
    fields = {}
    offset = 0
    while offset < len(data):
        key, offset = _read_varint(data, offset)
        wire_type = key & 7
        if wire_type == 0:
            value, offset = _read_varint(data, offset)
        elif wire_type == 1:
            value = int.from_bytes(data[offset : offset + 8], "little")
            offset += 8
        elif wire_type == 2:
            length, offset = _read_varint(data, offset)
            value = bytes(data[offset : offset + length])
            offset += length
        elif wire_type == 5:
            value = int.from_bytes(data[offset : offset + 4], "little")
            offset += 4
        else:
            raise ValueError(f"Unsupported protobuf wire type {wire_type}")
        fields.setdefault(key >> 3, []).append(value)
    return fields


def pb_first(fields, field, default=None):
    values = fields.get(field)
    return values[0] if values else default


def pb_text(fields, field, default=""):
    value = pb_first(fields, field)
    return value.decode("utf-8") if value is not None else default


# Steam CM connection


class CMConnection:
    def __init__(self, endpoint):
        self._ws = websocket.create_connection(f"wss://{endpoint}/cmsocket/", timeout=SOCKET_TIMEOUT)
        self._pending = collections.deque()
        self._next_job = 0
        self._last_sent = time.monotonic()
        self.steamid = 0
        self.session_id = 0

    def close(self):
        self._ws.close()

    def send(self, emsg, body, *, steamid=0, session_id=0, job_source=JOBID_NONE, target_job_name=None, realm=None):
        header = (
            pb_fixed64(1, steamid)
            + pb_varint(2, session_id)
            + pb_fixed64(10, job_source)
            + pb_fixed64(11, JOBID_NONE)
        )
        if target_job_name is not None:
            header += pb_string(12, target_job_name)
        if realm is not None:
            header += pb_varint(32, realm)
        self._ws.send_binary(struct.pack("<II", emsg | PROTO_MASK, len(header)) + header + body)
        self._last_sent = time.monotonic()

    def receive(self):
        while not self._pending:
            opcode, data = self._ws.recv_data()
            if opcode == websocket.ABNF.OPCODE_CLOSE:
                raise ConnectionError("Steam CM closed the connection")
            if opcode == websocket.ABNF.OPCODE_BINARY:
                self._unpack(data)
        return self._pending.popleft()

    def _unpack(self, packet):
        (raw_emsg,) = struct.unpack_from("<I", packet)
        if not raw_emsg & PROTO_MASK:
            return
        (header_length,) = struct.unpack_from("<I", packet, 4)
        header = pb_decode(packet[8 : 8 + header_length])
        body = packet[8 + header_length :]
        emsg = raw_emsg & ~PROTO_MASK

        if emsg != EMSG_MULTI:
            self._pending.append((emsg, header, body))
            return

        multi = pb_decode(body)
        payload = pb_first(multi, 2, b"")
        if pb_first(multi, 1, 0):
            payload = gzip.decompress(payload)
        offset = 0
        while offset < len(payload):
            (size,) = struct.unpack_from("<I", payload, offset)
            self._unpack(payload[offset + 4 : offset + 4 + size])
            offset += 4 + size

    def call(self, method, body):
        """Calls a unified service method, as a logged-on user once logon() has succeeded."""
        if self.session_id:
            if time.monotonic() - self._last_sent > 5:
                self.send(EMSG_CLIENT_HEARTBEAT, b"", steamid=self.steamid, session_id=self.session_id)
            self._next_job += 1
            job = self._next_job
            self.send(
                EMSG_SERVICE_METHOD_CALL,
                body,
                steamid=self.steamid,
                session_id=self.session_id,
                job_source=job,
                target_job_name=method,
            )
        else:
            job = int.from_bytes(os.urandom(8), "big") & 0x7FFFFFFFFFFFFFFF
            self.send(EMSG_SERVICE_METHOD_CALL_NON_AUTHED, body, job_source=job, target_job_name=method, realm=1)

        while True:
            emsg, header, response = self.receive()
            if emsg == EMSG_CLIENT_LOGGED_OFF:
                raise SteamError("Steam session", pb_first(pb_decode(response), 1, 2), "logged off by Steam")
            if pb_first(header, 11) != job:
                continue
            eresult = pb_first(header, 13, 2)
            if eresult != ERESULT_OK:
                raise SteamError(method, eresult, pb_text(header, 14, None))
            return pb_decode(response)

    def logon(self, refresh_token, steamid, account_name):
        body = (
            pb_varint(1, PROTOCOL_VERSION)
            + pb_string(6, "english")
            + pb_varint(7, OS_WIN11)
            + pb_varint(8, 1)
            + pb_bytes(11, pb_fixed32(1, 0))
            + pb_bytes(30, machine_id(account_name))
            + pb_varint(33, 2)
            + pb_varint(102, 1)
            + pb_string(108, refresh_token)
        )
        self.send(EMSG_CLIENT_LOGON, body, steamid=steamid)

        while True:
            emsg, header, response = self.receive()
            if emsg == EMSG_CLIENT_LOG_ON_RESPONSE:
                break
        eresult = pb_first(pb_decode(response), 1, 2)
        if eresult != ERESULT_OK:
            raise SteamError("ClientLogon", eresult)
        self.steamid = pb_first(header, 1, steamid)
        self.session_id = pb_first(header, 2, 0)

    def log_off(self):
        if self.session_id:
            self.send(EMSG_CLIENT_LOG_OFF, b"", steamid=self.steamid, session_id=self.session_id)


def machine_id(account_name):
    """Binary KeyValues blob the Steam client sends to identify the machine, derived from the account name."""

    def entry(name):
        digest = hashlib.sha1(f"SteamUser Hash {name} {account_name}".encode("utf-8")).hexdigest()
        return b"\x01" + name.encode("ascii") + b"\x00" + digest.encode("ascii") + b"\x00"

    return b"\x00MessageObject\x00" + entry("BB3") + entry("FF2") + entry("3B3") + b"\x08\x08"


def fetch_cm_endpoints():
    url = f"{WEBAPI_BASE}/ISteamDirectory/GetCMListForConnect/v1/?cellid=0&cmtype=websockets"
    with urllib.request.urlopen(url, timeout=HTTP_TIMEOUT) as response:
        servers = json.load(response)["response"]["serverlist"]
    servers = [s for s in servers if s.get("type") == "websockets" and s.get("realm") == "steamglobal"]
    if not servers:
        raise ConnectionError("Steam returned no WebSocket CM servers")
    servers.sort(key=lambda s: float(s.get("wtd_load", 0)))
    candidates = [s["endpoint"] for s in servers[:10]]
    random.shuffle(candidates)
    return candidates


# Authentication


def submit_guard_code(cm, client_id, steamid, shared_secret):
    for attempt in range(3):
        code = steam_guard_code(shared_secret)
        try:
            cm.call(
                "Authentication.UpdateAuthSessionWithSteamGuardCode#1",
                pb_varint(1, client_id) + pb_fixed64(2, steamid) + pb_string(3, code) + pb_varint(4, GUARD_DEVICE_CODE),
            )
            return
        except SteamError as e:
            if e.eresult not in CODE_MISMATCH_ERESULTS or attempt == 2:
                raise
        # Steam rejects a code that was already used, e.g. by SteamCMD moments earlier, so wait for the next one
        wait = 31 - time.time() % 30
        print(f"Steam Guard code was rejected; retrying with the next code in {wait:.0f} seconds.")
        time.sleep(wait)


def authenticate(cm, username, password, shared_secret):
    """Runs the credential login flow over the CM connection and returns (refresh token, SteamID)."""
    cm.send(EMSG_CLIENT_HELLO, pb_varint(1, PROTOCOL_VERSION))

    rsa_key = cm.call("Authentication.GetPasswordRSAPublicKey#1", pb_string(1, username))
    public_key = rsa.PublicKey(int(pb_text(rsa_key, 1), 16), int(pb_text(rsa_key, 2), 16))
    encrypted_password = base64.b64encode(rsa.encrypt(password.encode("utf-8"), public_key)).decode("ascii")

    device_details = (
        pb_string(1, f"{DEVICE_NAME}@{socket.gethostname()}")
        + pb_varint(2, PLATFORM_STEAM_CLIENT)
        + pb_varint(3, OS_WIN11)
        + pb_varint(4, 1)
    )
    session = cm.call(
        "Authentication.BeginAuthSessionViaCredentials#1",
        pb_string(2, username)
        + pb_string(3, encrypted_password)
        + pb_varint(4, pb_first(rsa_key, 3, 0))
        + pb_varint(5, 0)
        + pb_varint(7, SESSION_EPHEMERAL)
        + pb_string(8, "Unknown")
        + pb_bytes(9, device_details),
    )
    client_id = pb_first(session, 1)
    request_id = pb_first(session, 2)
    interval = struct.unpack("<f", struct.pack("<I", pb_first(session, 3, 0)))[0] or 1.0
    steamid = pb_first(session, 5)
    guards = {pb_first(pb_decode(c), 1) for c in session.get(4, [])}

    if GUARD_NONE not in guards:
        if GUARD_DEVICE_CODE in guards:
            if not shared_secret:
                raise RuntimeError("Steam requires a mobile authenticator code, but STEAM_TFASEED is not set.")
            submit_guard_code(cm, client_id, steamid, shared_secret)
        elif GUARD_EMAIL_CODE in guards:
            raise RuntimeError(
                "Steam requires an emailed Steam Guard code, which can't be automated. "
                "Enable the Steam mobile authenticator and set STEAM_TFASEED."
            )
        elif guards & {GUARD_DEVICE_CONFIRMATION, GUARD_EMAIL_CONFIRMATION}:
            print("Waiting for the login to be approved in the Steam mobile app or by email...")
        else:
            raise RuntimeError(f"Steam requires an unsupported login confirmation (types {sorted(guards)}).")

    deadline = time.monotonic() + AUTH_POLL_TIMEOUT
    while True:
        status = cm.call(
            "Authentication.PollAuthSessionStatus#1", pb_varint(1, client_id) + pb_bytes(2, request_id)
        )
        refresh_token = pb_text(status, 3)
        if refresh_token:
            return refresh_token, steamid
        client_id = pb_first(status, 1) or client_id
        if time.monotonic() > deadline:
            raise TimeoutError("Timed out waiting for Steam to approve the login.")
        time.sleep(interval)


# Workshop item updates


def fetch_public_details(item_id):
    """Returns the item's public details from the Web API, or None when Steam won't show them (e.g. a hidden item)."""
    data = urllib.parse.urlencode({"itemcount": 1, "publishedfileids[0]": item_id}).encode("ascii")
    url = f"{WEBAPI_BASE}/ISteamRemoteStorage/GetPublishedFileDetails/v1/"
    with urllib.request.urlopen(url, data=data, timeout=HTTP_TIMEOUT) as response:
        details = json.load(response)["response"]["publishedfiledetails"][0]
    if details.get("result") != ERESULT_OK:
        return None
    return {
        "title": details.get("title", ""),
        "appid": details.get("consumer_app_id"),
        "tags": [t["tag"] for t in details.get("tags", [])],
        "description": details.get("description", ""),
    }


def fetch_session_details(cm, item_id):
    response = cm.call("PublishedFile.GetDetails#1", pb_fixed64(1, item_id) + pb_varint(2, 1))
    details = pb_decode(pb_first(response, 1, b""))
    eresult = pb_first(details, 1, 2)
    if eresult != ERESULT_OK:
        raise SteamError(f"Reading Workshop item {item_id}", eresult)
    return {
        "title": pb_text(details, 16),
        "appid": pb_first(details, 5),
        "tags": [pb_text(pb_decode(t), 1) for t in details.get(52, [])],
        "description": pb_text(details, 17),
    }


def same_tags(a, b):
    return sorted(t.casefold() for t in a) == sorted(t.casefold() for t in b)


def normalize_description(text):
    # Steam stores descriptions with CRLF line endings and without trailing whitespace
    return text.replace("\r\n", "\n").rstrip()


def compute_tags(current, versions, add, remove):
    """Applies the requested changes to the current tags, keeping the order of tags that stay."""
    supported = {v.casefold() for v in versions or []}
    removed = {t.casefold() for t in remove}
    if versions is not None:
        removed |= {t.casefold() for t in current if VERSION_TAG.fullmatch(t) and t.casefold() not in supported}

    result = [t for t in current if t.casefold() not in removed]
    present = {t.casefold() for t in result}
    for tag in (versions or []) + add:
        if tag.casefold() not in present:
            result.append(tag)
            present.add(tag.casefold())
    return result


def describe_change(old, new):
    old_keys = {t.casefold() for t in old}
    new_keys = {t.casefold() for t in new}
    added = [t for t in new if t.casefold() not in old_keys]
    removed = [t for t in old if t.casefold() not in new_keys]
    return f"added [{', '.join(added)}], removed [{', '.join(removed)}]"


def plan_changes(details, versions, add, remove, description):
    """Returns the (tags, description) to send, each None when that part is already up to date."""
    tags = compute_tags(details["tags"], versions, add, remove)
    new_tags = None if same_tags(details["tags"], tags) else tags
    new_description = None
    if description is not None and normalize_description(details["description"]) != normalize_description(description):
        new_description = normalize_description(description)
    return new_tags, new_description


def describe_changes(details, new_tags, new_description):
    changes = []
    if new_tags is not None:
        changes.append(f"tags {describe_change(details['tags'], new_tags)}")
    if new_description is not None:
        old_length = len(normalize_description(details["description"]))
        changes.append(f"description ({old_length} -> {len(new_description)} characters)")
    return "; ".join(changes)


def github_notice(title, message):
    """Adds a notice to the workflow run's annotations when running in GitHub Actions."""
    if os.environ.get("GITHUB_ACTIONS") != "true":
        return

    def escape(text):
        return text.replace("%", "%25").replace("\r", "%0D").replace("\n", "%0A")

    print(f"::notice title={escape(title).replace(':', '%3A').replace(',', '%2C')}::{escape(message)}")


def check_item(details, item_id, appid):
    if details["appid"] is not None and int(details["appid"]) != appid:
        raise RuntimeError(f"Workshop item {item_id} belongs to app {details['appid']}, not {appid}.")


def update_item_in_session(cm, appid, item_id, plan, check_only):
    details = fetch_session_details(cm, item_id)
    check_item(details, item_id, appid)
    title = details["title"]
    new_tags, new_description = plan(details)
    if check_only:
        print(f"Logged in to Steam. Tags on '{title}': [{', '.join(details['tags'])}]")
        if new_tags is not None or new_description is not None:
            print(f"Would update {describe_changes(details, new_tags, new_description)}")
        return
    if new_tags is None and new_description is None:
        print(f"'{title}' is already up to date.")
        return

    # The tag list is always sent, because Steam replaces the item's tags with whatever list it gets
    request = pb_varint(1, appid) + pb_fixed64(2, item_id)
    if new_description is not None:
        request += pb_string(4, new_description)
    request += b"".join(pb_string(6, t) for t in (details["tags"] if new_tags is None else new_tags))
    cm.call("PublishedFile.Update#1", request)

    for attempt in range(VERIFY_ATTEMPTS):
        actual = fetch_session_details(cm, item_id)
        tags_done = new_tags is None or same_tags(actual["tags"], new_tags)
        description_done = new_description is None or normalize_description(actual["description"]) == new_description
        if tags_done and description_done:
            print(f"Updated '{title}': {describe_changes(details, new_tags, new_description)}")
            print(f"Tags are now: [{', '.join(actual['tags'])}]")
            if new_tags is not None:
                github_notice(
                    "Steam Workshop Tags Updated",
                    f"Updated the tags on '{title}': {describe_change(details['tags'], actual['tags'])}. "
                    f"Tags are now: [{', '.join(actual['tags'])}]",
                )
            return
        if attempt + 1 < VERIFY_ATTEMPTS:
            time.sleep(VERIFY_DELAY)
    if not tags_done:
        raise RuntimeError(
            f"Steam accepted the update, but reports tags [{', '.join(actual['tags'])}] "
            f"instead of [{', '.join(new_tags)}]."
        )
    raise RuntimeError("Steam accepted the update, but reports a description that differs from the one sent.")


def update_item(appid, item_id, plan, username, password, shared_secret, check_only=False):
    endpoints = fetch_cm_endpoints()
    for attempt in range(SESSION_ATTEMPTS):
        endpoint = endpoints[attempt % len(endpoints)]
        cm = None
        try:
            cm = CMConnection(endpoint)
            refresh_token, steamid = authenticate(cm, username, password, shared_secret)
            cm.logon(refresh_token, steamid, username)
            update_item_in_session(cm, appid, item_id, plan, check_only)
            cm.log_off()
            return
        except (*NETWORK_ERRORS, SteamError) as e:
            transient = not isinstance(e, SteamError) or e.eresult in TRANSIENT_ERESULTS
            if not transient or attempt + 1 == SESSION_ATTEMPTS:
                raise
            print(f"Steam CM {endpoint} failed ({e}); trying another server.")
            time.sleep(2)
        finally:
            if cm is not None:
                cm.close()


def read_supported_versions(about_path):
    root = ElementTree.parse(about_path).getroot()
    versions = [li.text.strip() for li in root.findall("./supportedVersions/li") if li.text and li.text.strip()]
    if not versions:
        raise RuntimeError(f"{about_path} has no <supportedVersions> entries.")
    return versions


def main():
    parser = argparse.ArgumentParser(
        description="Update the tags and description of a Steam Workshop item.",
        epilog="Credentials are read from STEAM_USERNAME, STEAM_PASSWORD and STEAM_TFASEED.",
    )
    parser.add_argument("item_id", type=int, help="the Workshop item's published file id")
    parser.add_argument("--appid", type=int, default=RIMWORLD_APPID, help=f"Steam app id (default {RIMWORLD_APPID})")
    parser.add_argument(
        "--versions-from",
        metavar="ABOUT_XML",
        help="make the item's version tags (e.g. '1.6') match <supportedVersions> in this About.xml",
    )
    parser.add_argument("--add", action="append", default=[], metavar="TAG", help="tag to add (repeatable)")
    parser.add_argument("--remove", action="append", default=[], metavar="TAG", help="tag to remove (repeatable)")
    parser.add_argument(
        "--description-from",
        metavar="FILE",
        help="make the item's description match this file (Steam markup, e.g. SteamDescription.txt)",
    )
    mode = parser.add_mutually_exclusive_group()
    mode.add_argument("--dry-run", action="store_true", help="show the change without logging in to Steam")
    mode.add_argument(
        "--check-login",
        action="store_true",
        help="log in to Steam and read the item without changing it; with other options, show the change",
    )
    args = parser.parse_args()

    if not (args.versions_from or args.add or args.remove or args.description_from or args.check_login):
        parser.error("nothing to do; pass --versions-from, --add, --remove, --description-from or --check-login")
    conflicting = {t.casefold() for t in args.add} & {t.casefold() for t in args.remove}
    if conflicting:
        parser.error(f"tags both added and removed: {', '.join(sorted(conflicting))}")

    try:
        versions = read_supported_versions(args.versions_from) if args.versions_from else None
        description = None
        if args.description_from:
            with open(args.description_from, encoding="utf-8") as f:
                description = f.read()

        def plan(details):
            return plan_changes(details, versions, args.add, args.remove, description)

        if not args.check_login:
            public = fetch_public_details(args.item_id)
            if public is None:
                print(f"Workshop item {args.item_id} isn't publicly visible; it will be read after logging in.")
                if args.dry_run:
                    return 0
            else:
                check_item(public, args.item_id, args.appid)
                new_tags, new_description = plan(public)
                if new_tags is None and new_description is None:
                    print(f"'{public['title']}' is already up to date. Tags: [{', '.join(public['tags'])}]")
                    return 0
                print(f"'{public['title']}' needs updating: {describe_changes(public, new_tags, new_description)}")
                if args.dry_run:
                    return 0

        username = os.environ.get("STEAM_USERNAME")
        password = os.environ.get("STEAM_PASSWORD")
        if not username or not password:
            raise RuntimeError("STEAM_USERNAME and STEAM_PASSWORD must be set.")
        update_item(
            args.appid,
            args.item_id,
            plan,
            username,
            password,
            os.environ.get("STEAM_TFASEED"),
            check_only=args.check_login,
        )
        return 0
    except (*NETWORK_ERRORS, RuntimeError, SteamError, ElementTree.ParseError, UnicodeDecodeError) as e:
        print(f"Error: {e}", file=sys.stderr)
        return 1


if __name__ == "__main__":
    sys.exit(main())
