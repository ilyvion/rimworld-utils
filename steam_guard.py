"""Prints the current Steam Guard mobile authenticator code.

The shared_secret (base64 or hex) comes from the STEAM_TFASEED environment variable. This has no
dependencies, so it runs with a plain `python3 steam_guard.py`.
"""

import base64
import hashlib
import hmac
import os
import re
import struct
import sys
import time


def steam_guard_code(shared_secret, timestamp=None):
    """Generates a Steam Guard mobile authenticator code, the same as steam-totp's generateAuthCode."""
    secret = shared_secret.strip()
    key = bytes.fromhex(secret) if re.fullmatch(r"[0-9a-fA-F]{40}", secret) else base64.b64decode(secret)
    counter = int(time.time() if timestamp is None else timestamp) // 30
    digest = hmac.new(key, struct.pack(">Q", counter), hashlib.sha1).digest()
    start = digest[19] & 0x0F
    value = struct.unpack(">I", digest[start : start + 4])[0] & 0x7FFFFFFF
    chars = "23456789BCDFGHJKMNPQRTVWXY"
    code = ""
    for _ in range(5):
        code += chars[value % len(chars)]
        value //= len(chars)
    return code


def main():
    shared_secret = os.environ.get("STEAM_TFASEED")
    if not shared_secret:
        print("Error: STEAM_TFASEED must be set.", file=sys.stderr)
        return 1
    print(steam_guard_code(shared_secret))
    return 0


if __name__ == "__main__":
    sys.exit(main())
