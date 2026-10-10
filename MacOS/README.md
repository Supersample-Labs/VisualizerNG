# VisualizerNG for macOS

VisualizerNG is a native macOS audio visualizer built with SwiftUI, ScreenCaptureKit and Accelerate. It includes ten visualization styles, five color palettes, adjustable rotation, a demo signal, and CRT snow/interlace effects.

## Build

Requirements: macOS 13 or later and Xcode command-line tools.

```sh
bash scripts/build.sh
```

The optimized Release app is created at `build/VisualizerNG.app`. The build script applies and verifies an ad hoc signature on the complete app bundle. This requires no signing certificate and covers the app's Info.plist and resources, which the linker-generated executable signature alone does not cover. To run it locally:

```sh
open build/VisualizerNG.app
```

macOS asks for Screen & System Audio Recording permission when capture starts. Audio is analyzed in memory and is not saved or uploaded. The Demo option generates a local test signal without capturing audio.

For regular use, copy the app to `/Applications` and grant permission to that copy. If an older build remains stuck in a permission loop, quit the app, remove its entry from System Settings → Privacy & Security → Screen & System Audio Recording, then open the newly built app and grant access again. Ad hoc signing does not provide a stable identity across changed builds, so rebuilding may require granting permission again. Use an Apple Development certificate for a stable development signing identity.

## GitHub releases

Build with the command above, then package the app for a release:

```sh
ditto -c -k --sequesterRsrc --keepParent build/VisualizerNG.app VisualizerNG-macOS.zip
```

Ad hoc signed apps downloaded from GitHub may require the user to approve opening them in macOS Privacy & Security settings. Ad hoc signing does not establish a trusted publisher or notarize the app. For Developer ID signed and notarized releases, configure Developer ID signing and notarization in a release workflow.

The app retains the existing icon in `Assets/App.icns`.
