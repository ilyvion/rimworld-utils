#!/usr/bin/env python3
import sys
from bs4 import BeautifulSoup

# Convert HTML to Steam's BBCode-like markup
# This script processes HTML content and converts it to a format compatible with Steam's forums or workshop
# It handles various HTML tags and attributes, converting them to their Steam equivalents.
#
# It's useful for converting the HTML version of a mod workshop description back into a format that can be used in Steam's text fields.
#
# Usage: python html_to_steam.py <path_to_html_file>

def html_to_steam(html):
    soup = BeautifulSoup(html, "html.parser")

    def convert(node):
        if node.name is None:  # text node
            return node

        if node.name in ["b", "strong"]:
            return f"[b]{''.join(convert(c) for c in node.children)}[/b]"
        if node.name in ["i", "em"]:
            return f"[i]{''.join(convert(c) for c in node.children)}[/i]"
        if node.name == "u":
            return f"[u]{''.join(convert(c) for c in node.children)}[/u]"
        if node.name in ["s", "strike"]:
            return f"[strike]{''.join(convert(c) for c in node.children)}[/strike]"
        if node.name == "a" and node.has_attr("href"):
            return f"[url={node['href']}]{''.join(convert(c) for c in node.children)}[/url]"
        if node.name == "h1":
            return f"[h1]{''.join(convert(c) for c in node.children)}[/h1]"
        if node.name == "h2":
            return f"[h2]{''.join(convert(c) for c in node.children)}[/h2]"
        if node.name == "h3":
            return f"[h3]{''.join(convert(c) for c in node.children)}[/h3]"
        if node.name == "ul":
            return f"[list]{''.join(convert(c) for c in node.children)}[/list]"
        if node.name == "ol":
            return f"[olist]{''.join(convert(c) for c in node.children)}[/olist]"
        if node.name == "li":
            return f"[*]{''.join(convert(c) for c in node.children)}"
        if node.name == "table":
            return f"[table]{''.join(convert(c) for c in node.children)}[/table]"
        if node.name == "tr":
            return f"[tr]{''.join(convert(c) for c in node.children)}[/tr]"
        if node.name == "th":
            return f"[th]{''.join(convert(c) for c in node.children)}[/th]"
        if node.name == "td":
            return f"[td]{''.join(convert(c) for c in node.children)}[/td]"
        if node.name == "hr":
            return "[hr][/hr]"
        if node.name == "code":
            return f"[code]{''.join(convert(c) for c in node.children)}[/code]"
        if node.name == "blockquote":
            return f"[quote]{''.join(convert(c) for c in node.children)}[/quote]"
        if node.name == "img":
            return f"[img]{node['src']}[/img]"
        if node.name == "br":
            return "\n" # Convert <br> to newline

        # Fallback: just convert children
        return ''.join(convert(c) for c in node.children)

    return ''.join(convert(child) for child in soup.contents)

def main():
    if len(sys.argv) != 2:
        print("Usage: html2steam <path_to_html_file>")
        sys.exit(1)

    path = sys.argv[1]
    try:
        with open(path, "r", encoding="utf-8") as f:
            html = f.read()
    except Exception as e:
        print(f"Error reading {path}: {e}", file=sys.stderr)
        sys.exit(2)

    steam_markup = html_to_steam(html)
    print(steam_markup)

if __name__ == "__main__":
    main()
