#!/bin/bash
# Build and publish Greenshot for Ubuntu/Linux
set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SRC="$SCRIPT_DIR/src"
OUT="$SCRIPT_DIR/publish"

echo "Building Greenshot for Linux..."
dotnet publish "$SRC/Greenshot/Greenshot.csproj" \
    --configuration Release \
    --runtime linux-x64 \
    --self-contained true \
    --output "$OUT" \
    -p:PublishSingleFile=false \
    -p:PublishReadyToRun=true

# Pack a distributable zip
VERSION=$(date +%Y%m%d)
ZIP="$SCRIPT_DIR/greenshot-linux-x64-$VERSION.zip"
(cd "$OUT" && zip -r "$ZIP" .)
echo ""
echo "Build complete! Output: $OUT"
echo "Distributable:  $ZIP"
echo ""
echo "To install system-wide:"
echo "  sudo cp $OUT/Greenshot /usr/local/bin/greenshot"
echo "  sudo cp $SCRIPT_DIR/greenshot.desktop /usr/share/applications/"
echo ""
echo "To run directly:"
echo "  $OUT/Greenshot"
