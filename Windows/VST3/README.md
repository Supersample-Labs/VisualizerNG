# Track Visualizer — VST3 port

Independent C++/JUCE port of the Windows visualizer. Source, build scripts, checks and plug-in output live in this folder. The WPF app remains a separate application.

## Using it in a DAW

1. Copy the complete `dist/Windows-x64/Track Visualizer.vst3` bundle directory into your DAW's VST3 plug-in folder (typically `C:\Program Files\Common Files\VST3`, which may require administrator permission). Do not copy only the nested binary.
2. Rescan VST3 plug-ins in your DAW and insert **Track Visualizer** as an audio effect on an audio track, instrument track output, bus, or master channel.
3. Open the plug-in editor. It opens that instance's independent, resizable visualizer window. The small host panel has an **Open visualizer window** button to show it again.
4. Insert/open another instance on each additional track you want to visualize. Each instance receives only its own routed audio, has independent settings, and owns its own window. Keep multiple floating windows open together. Track names appear in the title when supplied by the host.

Closing the DAW's editor panel leaves its visualizer open. Closing the floating visualizer pauses its analysis; reopen it through the host panel. Removing the plug-in or closing the project closes that instance's window. Windows are opened on demand when opening the plug-in editor, not during DAW startup or scanning. Window positions and open/closed state are not session parameters; visualization settings are saved.

Audio is passed through unchanged with no added latency. Mono-to-mono and stereo-to-stereo are supported. A bounded single-producer/single-consumer queue transfers analysis samples to the UI; the audio callback does no rendering, FFT, heap allocation, or locking. Stereo analysis uses the channel with the higher block RMS so out-of-phase stereo does not disappear in a mono sum. Excess analysis data can be dropped when the UI cannot keep up; track audio is never dropped.

## Controls

- Ten modes: oscilloscope, circle, triangle, square, pentagon, hexagon, octagon, star, spectrum, radial spectrum.
- Five palettes: Lagoon, Electric blue, Neon violet, Sunset, Lime.
- Rotation: Off, 2D spin, 3D spin; angle, tilt, turn, and reset. Closed shapes animate around their full outline using mirrored waveform sampling.
- CRT: Off, Snow, Interlace, Snow + Interlace. Separate intensity controls, three downward-moving scan bands, and visible interlace without snow.
- Sensitivity and smoothing. All settings are host parameters, independently saved and automatable per instance.

The visualizer uses JUCE software drawing at 30 fps to avoid requiring a separate OpenGL context in the DAW. CRT grain is generated at half resolution. The visuals reproduce the original design rather than sharing WPF code or guaranteeing pixel-identical rendering. The track plug-in replaces the standalone app's Windows output-device selector.

## Build Windows x64

Requirements: Visual Studio 2022 Build Tools with **Desktop development with C++**, Windows SDK, CMake tools, Git, and internet access for the first JUCE download.

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\Build-VST3.ps1
```

This uses a local build cache at `%LOCALAPPDATA%\TrackVisualizerBuild` (also works when source is on a network drive), pins JUCE 8.0.12 to commit `29396c22c93392d6738e021b83196283d6e4d850`, runs tests, and copies the bundle to `dist/Windows-x64`. It does not install anything into DAW folders. Override the cache with `-BuildDirectory`. Tests briefly open and close visualizer windows.

## Build macOS VST3

With Xcode command-line tools, Git and CMake installed, from this folder:

```sh
cmake -S . -B build -G Xcode -DCMAKE_OSX_ARCHITECTURES="arm64;x86_64"
cmake --build build --config Release --target TrackVisualizer_VST3
```

Output: `build/TrackVisualizer_artefacts/Release/VST3/Track Visualizer.vst3`. Copy that bundle to `~/Library/Audio/Plug-Ins/VST3`. The macOS build requires building and validating on a Mac; Windows binaries cannot run on macOS. This project currently targets VST3 only, so it will not load in Logic Pro (which needs an AU build).

## Validation

`ctest --test-dir <build-directory>/build -C Release --output-on-failure` runs:

- A host check that discovers and loads the actual VST3, verifies parameter-state round-trip and bit-exact pass-through at 44.1/48/96 kHz, checks mono/stereo buses and variable blocks, and exercises editor lifecycle.
- A window check using the same source that creates two named track instances, verifies independent settings/windows, host-panel closure, visual rendering across shapes/effects, resizing, close/reopen and cleanup.

These are development checks, not certification in every DAW. Test scanning, track routing, automation, save/reopen and multiple windows in your DAW before relying on the plug-in in a production session.

## Dependencies

JUCE is fetched at build time. Its modules are available under AGPLv3 or a JUCE commercial license; review the included third-party notice and choose the appropriate licensing route before distributing a release. No signing, installer, AU target, or commercial distribution setup is included in this development port.
