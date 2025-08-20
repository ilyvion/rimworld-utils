#!/usr/bin/env bash
set -euo pipefail

# --- Check for --yes flag ---
auto_yes="false"
args=()
for arg in "$@"; do
    if [[ "$arg" == "--yes" ]]; then
        auto_yes="true"
    else
        args+=("$arg")
    fi
done
set -- "${args[@]}"

# --- Don't allow staged changes ---
if ! git diff --cached --quiet; then
    echo "There are already staged changes. Please commit or unstage them first."
    exit 1
fi

# --- Config ---
about_file="About/About.xml"
props_file="Directory.Build.props"
changelog_file="CHANGELOG.md"

# --- Get old version from props ---
old_version=$(grep -oPm1 '(?<=<VersionPrefix>)[^<]+' "$props_file") || {
    echo "Failed to detect old version in $props_file"
    exit 1
}

# --- Require new version ---
if [[ $# -lt 1 ]]; then
    echo "Usage: $0 <new-version> [--yes]"
    echo "Current version is: $old_version"
    exit 1
fi
new_version="$1"

# --- Today date ---
today=$(date +%Y-%m-%d)

# --- Pre-flight: check if tag exists ---
overwrite_tag="false"
if git rev-parse -q --verify "refs/tags/v${new_version}" >/dev/null; then
    if [[ "$auto_yes" == "true" ]]; then
        overwrite_tag="true"
        echo "Tag v${new_version} already exists. Auto-overwriting (--yes given)."
    else
        read -r -p "Tag v${new_version} already exists. Overwrite? [y/N] " ans
        case "$ans" in
            [yY]|[yY][eE][sS]) overwrite_tag="true" ;;
            *) echo "Aborting."; exit 1 ;;
        esac
    fi
fi

echo "Bumping version: $old_version → $new_version ($today)"

# --- Detect repo URL from changelog ---
unreleased_line=$(grep -E "^\[Unreleased\]:" "$changelog_file") || {
    echo "No [Unreleased] link found in $changelog_file"
    exit 1
}
repo_url=$(echo "$unreleased_line" | sed -E 's/^\[Unreleased\]: (.*)\/compare\/v.*$/\1/')
echo "Detected repo URL: $repo_url"

# --- Validate repo URL against git origin ---
origin_url=$(git remote get-url origin)
if [[ "$origin_url" == git@github.com:* ]]; then
    origin_normalized="https://github.com/${origin_url#git@github.com:}"
    origin_normalized="${origin_normalized%.git}"
elif [[ "$origin_url" == https://github.com/* ]]; then
    origin_normalized="${origin_url%.git}"
else
    echo "Unsupported git remote format: $origin_url"
    exit 1
fi

if [[ "$repo_url" != "$origin_normalized" ]]; then
    echo "Repo URL mismatch: changelog=$repo_url, origin=$origin_normalized"
    exit 1
fi

# --- Update About.xml ---
if grep -q "<modVersion" "$about_file"; then
    sed -i -E "s|(<modVersion[^>]*>)[^<]+(</modVersion>)|\1${new_version}\2|" "$about_file"
else
    echo "No <modVersion> element found in $about_file"
    exit 1
fi

# --- Update Directory.Build.props ---
if grep -q "<VersionPrefix>$old_version</VersionPrefix>" "$props_file"; then
    sed -i "s|<VersionPrefix>$old_version</VersionPrefix>|<VersionPrefix>$new_version</VersionPrefix>|" "$props_file"
else
    echo "Expected <VersionPrefix>$old_version</VersionPrefix> not found in $props_file"
    exit 1
fi

# --- Insert new changelog header ---
if grep -q "^## \[Unreleased\]" "$changelog_file"; then
    sed -i "/^## \[Unreleased\]/a\\
\\
## [${new_version}] - ${today}" "$changelog_file"
else
    echo "Expected '## [Unreleased]' section not found in $changelog_file"
    exit 1
fi

# --- Update [Unreleased] link ---
unreleased_pattern="^\[Unreleased\]: $repo_url/compare/v${old_version}\.\.\.HEAD"
if grep -Eq "$unreleased_pattern" "$changelog_file"; then
    sed -i "s|v${old_version}...HEAD|v${new_version}...HEAD|" "$changelog_file"
    sed -i "/^\[Unreleased\]:/a[${new_version}]: ${repo_url}/compare/v${old_version}..v${new_version}" "$changelog_file"
else
    echo "Expected [Unreleased] link with v${old_version} not found in $changelog_file"
    exit 1
fi

# --- Commit only intended files ---
git add "$about_file" "$props_file" "$changelog_file"
git commit -m "chore: prepare for v${new_version} release"

# --- Signed tag ---
# --- delete existing tag if user approved overwrite, then re-tag signed ---
if [[ "$overwrite_tag" == "true" ]]; then
    git tag -d "v${new_version}" >/dev/null
fi
git tag -s "v${new_version}" -m "Release v${new_version}"

echo "Version bump complete, committed, and tagged."
echo "Review the commit and tag; push when ready:"
echo "  git push --follow-tags"
