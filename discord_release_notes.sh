#!/usr/bin/env bash
# Formats a changelog entry as Discord release notes, split into messages that fit Discord's
# 2000-character limit. Prints the messages to stdout separated by "---", or posts them with --post.
#
# Usage: discord_release_notes.sh [--title TITLE] [--pre-header TEXT] [--post]
#   --title TITLE      Text before the date in the header line (default: "Updated")
#   --pre-header TEXT  Line placed above the header, e.g. a role mention: <@&ROLE_ID>
#   --post             Post each message to the webhook in DISCORD_WEBHOOK_URL
#
# Environment:
#   CHANGELOG_CONTENT    The changelog entry, in Markdown
#   VERSION              The version shown in the header line
#   DISCORD_WEBHOOK_URL  The webhook to post to; required by --post
set -euo pipefail

# Makes ${#var} count characters instead of bytes
export LC_ALL=C.UTF-8

discord_limit=2000
max_post_attempts=5

title="Updated"
pre_header=""
post=false

while [ "$#" -gt 0 ]; do
  case "$1" in
    --title)
      title="$2"
      shift 2
      ;;
    --pre-header)
      pre_header="$2"
      shift 2
      ;;
    --post)
      post=true
      shift
      ;;
    *)
      echo "Unknown argument: $1" >&2
      exit 2
      ;;
  esac
done

if [ "$post" = true ] && [ -z "${DISCORD_WEBHOOK_URL:-}" ]; then
  echo "--post requires DISCORD_WEBHOOK_URL to be set." >&2
  exit 2
fi

header="**${title} $(date +%Y-%m-%d)** (${VERSION})"
subheader="Release notes:"

# Discord doesn't support reference-style links ([text][label] plus a
# separate "[label]: url" line), so those are converted to inline links.
# It also embeds link previews unless the URL is wrapped: [text](<url>).
discord_body=$(python3 - <<'PY'
import os
import re

text = os.environ["CHANGELOG_CONTENT"]

# Collect and remove reference definitions: [label]: url "optional title"
definitions = {}
definition_re = re.compile(
    r"""^ {0,3}\[([^\]]+)\]:[ \t]*<?(\S+?)>?(?:[ \t]+(?:"[^"]*"|'[^']*'|\([^)]*\)))?[ \t]*$\n?""",
    re.M,
)


def take_definition(m):
    definitions[m.group(1).lower()] = m.group(2)
    return ""


text = definition_re.sub(take_definition, text).strip("\n")

# GitHub alert markers render as plain text on Discord
alert_emoji = {
    "NOTE": "ℹ️",
    "TIP": "💡",
    "IMPORTANT": "‼️",
    "WARNING": "⚠️",
    "CAUTION": "🚨",
}
for alert, emoji in alert_emoji.items():
    text = text.replace(f"[!{alert}]", f"{emoji}{alert}{emoji}")


def inline(label, url):
    return f"[{label}](<{url}>)"


def full_reference(m):
    label = m.group(2) or m.group(1)
    url = definitions.get(label.lower())
    return inline(m.group(1), url) if url else m.group(0)


def shortcut_reference(m):
    url = definitions.get(m.group(1).lower())
    return inline(m.group(1), url) if url else m.group(0)


# [text][label] and [text][]
text = re.sub(r"\[([^\]]+)\]\[([^\]]*)\]", full_reference, text)
# [label]
text = re.sub(r"\[([^\]]+)\](?![\[(:])", shortcut_reference, text)
# Inline links
text = re.sub(r"\]\((https?:[^)>\s]+)\)", r"](<\1>)", text)
text = re.sub(r"^### (.*)$", r"**\1**", text, flags=re.M)

print(text)
PY
)
mapfile -t body_lines <<< "$discord_body"

all_lines=()
if [ -n "$pre_header" ]; then
  all_lines+=("$pre_header")
fi
all_lines+=("$header" "$subheader" "")

# A line over the limit is broken at its last space within the limit, or at the limit if it has none.
for line in "${body_lines[@]}"; do
  while [ "${#line}" -gt "$discord_limit" ]; do
    part="${line:0:discord_limit}"
    if [[ "$part" == *[^\ ]\ * ]]; then
      part="${part% *}"
    fi
    all_lines+=("$part")
    line="${line:${#part}}"
    line="${line# }"
  done
  all_lines+=("$line")
done

# Posts one message, retrying when rate limited or when Discord has a transient failure.
post_message() {
  local payload response_file status attempt retry_after
  payload=$(jq -n --arg content "$1" \
    '{content: $content, allowed_mentions: {parse: ["roles"]}, flags: 4}')
  response_file=$(mktemp)

  for ((attempt = 1; attempt <= max_post_attempts; attempt++)); do
    status=$(curl -sS --max-time 30 -o "$response_file" -w '%{http_code}' \
      -H 'Content-Type: application/json' -d @- \
      "${DISCORD_WEBHOOK_URL}?wait=true" <<< "$payload") || status=000

    case "$status" in
      2*)
        rm -f "$response_file"
        return 0
        ;;
      429)
        retry_after=$(jq -r '.retry_after // 1' "$response_file" 2>/dev/null || echo 1)
        sleep "$retry_after"
        ;;
      5* | 000)
        sleep "$((attempt * 2))"
        ;;
      *)
        break
        ;;
    esac
  done

  echo "::error title=Discord Post Failed::Discord responded with HTTP $status: $(cat "$response_file")"
  rm -f "$response_file"
  return 1
}

first_chunk=true

flush() {
  if [ "$post" = true ]; then
    post_message "$chunk"
    return
  fi

  if [ "$first_chunk" = true ]; then
    first_chunk=false
  else
    echo
    echo "---"
    echo
  fi
  printf '%s\n' "$chunk"
}

chunk=""
for line in "${all_lines[@]}"; do
  if [ -z "$chunk" ]; then
    candidate="$line"
  else
    candidate="$chunk"$'\n'"$line"
  fi
  if [ "${#candidate}" -gt "$discord_limit" ] && [ -n "$chunk" ]; then
    flush
    chunk="$line"
  else
    chunk="$candidate"
  fi
done

if [ -n "$chunk" ]; then
  flush
fi
