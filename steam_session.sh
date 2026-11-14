#!/usr/bin/env bash
# Restores or saves the Steam login sessions release_mod.yml reuses between releases, so a release logs in to an
# existing session instead of authorizing a new device. The sessions are stored GPG-encrypted in a private GitHub
# repository, with a key derived from the Steam credentials.
#
# Usage: steam_session.sh restore|save
#
# Sessions (stored path in the repository -> local path):
#   steamcmd/config.vdf.gpg                  -> $HOME/Steam/config/config.vdf (SteamCMD's cached login)
#   steam_workshop_update/session.json.gpg   -> $STEAM_SCRIPT_SESSION (steam_workshop_update.py --session-file)
#
# Environment:
#   STEAM_SESSION_REPO                              owner/name of the private repository the sessions are stored in
#   GH_TOKEN                                        token with read and write access to that repository's contents
#   STEAM_USERNAME, STEAM_PASSWORD, STEAM_TFASEED   the Steam credentials the encryption key is derived from
#   STEAM_SCRIPT_SESSION                            local path of steam_workshop_update.py's session file
#   STEAM_SESSION_STATE                             directory where restore records what save compares against
#
# Neither command fails the release: problems are reported as workflow warnings, and the release then logs in with
# the credentials.

set -euo pipefail

: "${STEAM_SESSION_REPO:?}" "${GH_TOKEN:?}" "${STEAM_USERNAME:?}" "${STEAM_PASSWORD:?}" "${STEAM_TFASEED:?}"
: "${STEAM_SCRIPT_SESSION:?}" "${STEAM_SESSION_STATE:?}"

SESSIONS=(
    "steamcmd/config.vdf.gpg|$HOME/Steam/config/config.vdf"
    "steam_workshop_update/session.json.gpg|$STEAM_SCRIPT_SESSION"
)

GNUPGHOME=$(mktemp -d)
export GNUPGHOME
trap 'rm -rf "$GNUPGHOME"' EXIT

warn() {
    echo "::warning title=Steam Session::$1"
}

passphrase() {
    printf 'rimworld-utils steam session\n%s\n%s\n%s' "$STEAM_USERNAME" "$STEAM_PASSWORD" "$STEAM_TFASEED" |
        sha256sum | cut -d' ' -f1
}

gpg_run() {
    gpg --batch --quiet --no-tty --pinentry-mode loopback --passphrase-fd 3 "$@" 3<<<"$(passphrase)"
}

state_file() {
    echo "$STEAM_SESSION_STATE/${1//\//_}.$2"
}

# A session worth saving: SteamCMD's config.vdf only holds a login once it has a ConnectCache entry
is_session() {
    local remote=$1 local_path=$2
    [ -s "$local_path" ] || return 1
    if [[ "$remote" == steamcmd/* ]]; then
        grep -q '"ConnectCache"' "$local_path"
    fi
}

restore() {
    mkdir -p "$STEAM_SESSION_STATE"
    local entry remote local_path response error encrypted
    for entry in "${SESSIONS[@]}"; do
        remote=${entry%%|*}
        local_path=${entry#*|}
        error=$(mktemp)
        if ! response=$(gh api "repos/$STEAM_SESSION_REPO/contents/$remote" 2>"$error"); then
            if grep -q 'HTTP 404' "$error"; then
                echo "No stored session at $remote; logging in with the credentials."
            else
                warn "Couldn't read $remote from $STEAM_SESSION_REPO: $(tr '\n' ' ' <"$error")"
            fi
            rm -f "$error"
            continue
        fi
        rm -f "$error"
        jq -r '.sha' <<<"$response" >"$(state_file "$remote" blob)"

        encrypted=$(mktemp)
        jq -r '.content' <<<"$response" | base64 -d >"$encrypted"
        mkdir -p "$(dirname "$local_path")"
        if gpg_run --decrypt --output "$local_path.tmp" "$encrypted" 2>/dev/null; then
            chmod 600 "$local_path.tmp"
            mv "$local_path.tmp" "$local_path"
            sha256sum "$local_path" | cut -d' ' -f1 >"$(state_file "$remote" sha256)"
            echo "Restored the stored session from $remote."
        else
            rm -f "$local_path.tmp"
            warn "Couldn't decrypt $remote, e.g. because the Steam credentials changed; logging in with the credentials."
        fi
        rm -f "$encrypted"
    done
}

save() {
    local entry remote local_path blob_sha previous encrypted args
    for entry in "${SESSIONS[@]}"; do
        remote=${entry%%|*}
        local_path=${entry#*|}
        if ! is_session "$remote" "$local_path"; then
            continue
        fi
        previous=$(cat "$(state_file "$remote" sha256)" 2>/dev/null || true)
        if [ "$(sha256sum "$local_path" | cut -d' ' -f1)" = "$previous" ]; then
            echo "The session at $remote is unchanged."
            continue
        fi

        encrypted=$(mktemp)
        if ! gpg_run --symmetric --cipher-algo AES256 --yes --output "$encrypted" "$local_path"; then
            warn "Couldn't encrypt the session for $remote; the next release may log in as a new device."
            rm -f "$encrypted"
            continue
        fi
        args=(
            --method PUT "repos/$STEAM_SESSION_REPO/contents/$remote"
            -f "message=Update $remote from ${GITHUB_REPOSITORY:-local} ${GITHUB_REF_NAME:-}"
            -f "content=$(base64 -w0 "$encrypted")"
        )
        blob_sha=$(cat "$(state_file "$remote" blob)" 2>/dev/null || true)
        if [ -n "$blob_sha" ]; then
            args+=(-f "sha=$blob_sha")
        fi
        # A stale sha is rejected, which means another release stored a session in the meantime; that one is kept
        if gh api "${args[@]}" >/dev/null; then
            echo "Saved the session to $remote."
        else
            warn "Couldn't save the session to $remote in $STEAM_SESSION_REPO; the next release may log in as a new device."
        fi
        rm -f "$encrypted"
    done
}

case "${1:-}" in
restore) restore ;;
save) save ;;
*)
    echo "Usage: $0 restore|save" >&2
    exit 2
    ;;
esac
