"""Extracts all comments for a given Steam Workshop item using the PublishedFile_Public endpoint and correct payload."""

from datetime import datetime
import json
import sys
import requests
from bs4 import BeautifulSoup
import re
import urllib.parse


def build_workshop_page_url(workshop_item_id):
    return f"https://steamcommunity.com/sharedfiles/filedetails/?id={workshop_item_id}"


def fetch_workshop_page(workshop_item_id):
    url = build_workshop_page_url(workshop_item_id)
    headers = {
        "User-Agent": (
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
            "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/70.0.3538.77 Safari/537.36"
        ),
    }
    response = requests.get(url, headers=headers, timeout=10)
    if response.status_code == 200:
        return response.text
    print(f"Failed to fetch Workshop page {workshop_item_id}, status code: {response.status_code}")
    return None


def extract_owner_steam64_id(page_html):
    soup = BeautifulSoup(page_html, "html.parser")
    area = soup.find("div", class_="commentthread_area")
    if area and area.has_attr("id"):
        match = re.search(r"commentthread_PublishedFile_Public_(\d+)_\d+_area", area["id"])
        if match:
            return match.group(1)
    return None


def extract_sessionid(page_html):
    # Try to find sessionid from the page's JS
    match = re.search(r"g_sessionID\s*=\s*\"([a-f0-9]+)\"", page_html)
    if match:
        return match.group(1)
    return None


def fetch_comments(owner_steam64_id, workshop_item_id, sessionid, start=0, totalcount=0, count=10, extended_data=None):
    url = f"https://steamcommunity.com/comment/PublishedFile_Public/render/{owner_steam64_id}/{workshop_item_id}/"
    headers = {
        "Accept": "text/javascript, text/html, application/xml, text/xml, */*",
        "Content-type": "application/x-www-form-urlencoded; charset=UTF-8",
        "X-Requested-With": "XMLHttpRequest",
        "User-Agent": (
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
            "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/70.0.3538.77 Safari/537.36"
        ),
    }
    payload = {
        "start": start,
        "totalcount": totalcount,
        "count": count,
        "sessionid": sessionid,
        "feature2": "-1",
        "extended_data": extended_data or ""
    }
    response = requests.post(url, headers=headers, data=payload, timeout=10)
    if response.status_code == 200:
        return response.json()
    print(f"Failed to fetch comments for Workshop item {workshop_item_id}, status code: {response.status_code}")
    return None


def parse_comments_html(comments_html):
    soup = BeautifulSoup(comments_html, "html.parser")
    comments = soup.find_all("div", class_="commentthread_comment")
    parsed_comments = []
    for comment in comments:
        author_tag = comment.find("a", class_="commentthread_author_link")
        author = author_tag.text.strip() if author_tag else "Unknown"
        author_link = author_tag["href"] if author_tag else ""
        text_tag = comment.find("div", class_="commentthread_comment_text")
        text = text_tag.text.strip() if text_tag else ""
        timestamp_tag = comment.find("div", class_="commentthread_comment_timestamp", attrs={"data-timestamp": True})
        timestamp = timestamp_tag["title"] if timestamp_tag and timestamp_tag.has_attr("title") else (timestamp_tag.text.strip() if timestamp_tag else "Unknown date")
        parsed_comments.append({
            "timestamp": timestamp,
            "text": text,
            "author": author,
            "author_link": author_link,
        })
    return parsed_comments

if __name__ == "__main__":
    if len(sys.argv) != 2:
        print("Usage: python steam_comment_extractor.py <Workshop Item ID>")
        sys.exit(1)

    WORKSHOP_ITEM_ID_ARG = sys.argv[1]
    all_comments = []
    page_size = 10
    start = 0

    # Fetch initial page and comments
    page_html = fetch_workshop_page(WORKSHOP_ITEM_ID_ARG)
    if not page_html:
        sys.exit(1)

    # Extract owner Steam64 ID and sessionid
    owner_steam64_id = extract_owner_steam64_id(page_html)
    sessionid = extract_sessionid(page_html)
    if not owner_steam64_id or not sessionid:
        print(f"Could not extract owner Steam64 ID or sessionid from page; {owner_steam64_id}, {sessionid}.")
        sys.exit(1)

    # Find total_count
    soup = BeautifulSoup(page_html, "html.parser")
    total_count_tag = soup.find("span", class_="commentthread_count_label")
    if total_count_tag:
        try:
            x = total_count_tag.text.strip().split()[0].replace(",", "")
            print(f"Total comments found: {x}")
            total_count = int(x)
        except Exception:
            total_count = 0
    else:
        total_count = 0

    # Find extended_data (optional, fallback to empty string)
    # You may need to scrape this from the page's JS or HTML if required for more advanced requests
    extended_data = ""

    # Paginate and fetch all comments
    for start in range(0, total_count, page_size):
        ajax_data = fetch_comments(owner_steam64_id, WORKSHOP_ITEM_ID_ARG, sessionid, start=start, totalcount=total_count, count=page_size, extended_data=extended_data)
        comments_html_arg = ajax_data.get("comments_html", "") if ajax_data else ""
        page_comments = parse_comments_html(comments_html_arg)
        all_comments.extend(page_comments)

    current_date = datetime.now().strftime("%d%m%y")
    with open(
        f"steam_comments_{WORKSHOP_ITEM_ID_ARG}_{current_date}.json", "w", encoding="utf-8"
    ) as json_file:
        json.dump(all_comments, json_file, indent=4, ensure_ascii=False)