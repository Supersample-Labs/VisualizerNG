# VisualizerNG for macOS

VisualizerNG is a native macOS audio visualizer built with SwiftUI, ScreenCaptureKit and Accelerate. It includes ten visualization styles, five color palettes, adjustable rotation, a demo signal, and CRT snow/interlace effects.

## Build

Requirements: macOS 13 or later and Xcode command-line tools.

```sh
bash scripts/build.sh
```

The optimized Release app is created at `build/VisualizerNG.app`. The build is unsigned for local use and GitHub distribution. To run it locally:

```sh
open build/VisualizerNG.app
```

macOS asks for Screen & System Audio Recording permission when capture starts. Audio is analyzed in memory and is not saved or uploaded. The Demo option generates a local test signal without capturing audio.

## GitHub releases

Build with the command above, then package the app for a release:

```sh
ditto -c -k --sequesterRsrc --keepParent build/VisualizerNG.app VisualizerNG-macOS.zip
```

Unsigned apps downloaded from GitHub may require the user to approve opening them in macOS Privacy & Security settings. For signed and notarized releases, configure Developer ID signing and notarization in a release workflow.

The app retains the existing icon in `Assets/App.icns`.
