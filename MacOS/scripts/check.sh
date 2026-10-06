#!/bin/bash
set -euo pipefail
cd "$(dirname "$0")/.."
mkdir -p build/module-cache
swiftc -swift-version 5 -module-cache-path "$PWD/build/module-cache" Sources/Audio.swift Checks/main.swift -o build/AudioChecks
build/AudioChecks
