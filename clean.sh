#!/bin/bash
# Clean build artifacts and publish output for all projects
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SRC="$SCRIPT_DIR/src"

echo "Cleaning Greenshot build artifacts..."

# Remove bin/obj from all projects
find "$SRC" -type d \( -name bin -o -name obj \) -exec rm -rf {} + 2>/dev/null || true

# Remove publish output
if [ -d "$SCRIPT_DIR/publish" ]; then
    rm -rf "$SCRIPT_DIR/publish"
    echo "Removed publish/"
fi

echo "Done. Run ./build.sh to rebuild."
