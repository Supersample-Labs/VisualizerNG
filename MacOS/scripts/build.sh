#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."

APP="$PWD/build/VisualizerNG.app"
DERIVED_DATA="$PWD/build/DerivedData"

xcodebuild \
  -project VisualizerNG.xcodeproj \
  -scheme VisualizerNG \
  -configuration Release \
  -destination 'generic/platform=macOS' \
  -derivedDataPath "$DERIVED_DATA" \
  CODE_SIGNING_ALLOWED=NO \
  build

PRODUCT="$DERIVED_DATA/Build/Products/Release/VisualizerNG.app"
if [[ ! -d "$PRODUCT" ]]; then
  echo "Release app was not produced at: $PRODUCT" >&2
  exit 1
fi
rm -rf "$APP"
ditto "$PRODUCT" "$APP"
echo "Release build created: $APP"
