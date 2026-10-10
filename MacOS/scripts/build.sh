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
# Sign the complete bundle so macOS can associate capture consent with the app.
# The linker-generated executable signature alone does not seal Info.plist.
BUNDLE_IDENTIFIER=$(/usr/libexec/PlistBuddy -c 'Print :CFBundleIdentifier' "$APP/Contents/Info.plist")
codesign --force --sign - --identifier "$BUNDLE_IDENTIFIER" "$APP"
codesign --verify --strict --verbose=2 "$APP"
echo "Release build created: $APP"
