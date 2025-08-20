#!/bin/bash
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
mkdir -p "$SCRIPT_DIR/refs"

rm -rf refs
refasmer -v --all -O refs -g "originals/**/*.dll"
