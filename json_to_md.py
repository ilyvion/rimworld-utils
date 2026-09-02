import json
import sys

if len(sys.argv) != 2:
    print("Usage: python json_to_md.py <comments.json>")
    sys.exit(1)

json_path = sys.argv[1]
md_path = json_path.rsplit('.', 1)[0] + '.md'

with open(json_path, "r", encoding="utf-8") as f:
    comments = json.load(f)

with open(md_path, "w", encoding="utf-8") as out:
    for c in comments:
        out.write(f"[{c['author']}]({c['author_link']}) said @ {c['timestamp']}:  \n{c['text']}\n\n")

print(f"Markdown written to {md_path}")
