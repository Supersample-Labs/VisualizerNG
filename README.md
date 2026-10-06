# VisualizerNG

Real-time audio visualization for Windows and macOS, with waveform shapes, spectrum views, rotation, color palettes, and CRT effects. The two native applications share the same visualization capabilities and effects.

## Download

Get the portable apps from [GitHub Releases](https://github.com/Supersample-Labs/VisualizerNG/releases/latest).

- **Windows x64:** extract `VisualizerNG-Windows-x64.zip` and run `VisualizerNG.exe`. Keep the extracted files together; the .NET runtime is included. Requires Windows 10 version 2004 or later or Windows 11, and an OpenGL 3.3-capable graphics driver.
- **macOS:** extract `VisualizerNG-macOS.zip` and open `VisualizerNG.app`. Requires macOS 13 or later. The supplied build is unsigned; macOS may require approval in Privacy & Security before opening it. Allow Screen & System Audio Recording when prompted for audio capture.

## Features

- Ten visualization styles: oscilloscope, circle, triangle, square, pentagon, hexagon, octagon, star, spectrum, and radial spectrum.
- Five palettes: Lagoon, Electric blue, Neon violet, Sunset, and Lime.
- Off, 2D spin, and 3D spin rotation, with angle, tilt, turn, and reset controls.
- CRT snow and interlace effects with independent intensity controls.
- Adjustable sensitivity and smoothing, plus fullscreen visualization.
- Windows captures the selected playback device through WASAPI loopback and can follow the default output. macOS captures system audio through ScreenCaptureKit and also includes a demo signal.

Audio is analyzed locally in memory; the apps do not save or upload audio recordings.

## Build from source

### Windows

Install the .NET 8 SDK, then run from the repository root:

```powershell
dotnet run --project Windows/Visualizer.App/Visualizer.App.csproj
dotnet publish Windows/Visualizer.App/Visualizer.App.csproj -c Release -r win-x64 --self-contained true -o artifacts/VisualizerNG-Windows-x64
```

The published executable is `artifacts/VisualizerNG-Windows-x64/VisualizerNG.exe`.

Play audio in another app and select the speakers or headphones playing it. Press F11 for fullscreen and Escape to leave fullscreen. See [Windows documentation](Windows/README.md) for capture troubleshooting and controls.

### macOS

On macOS with Xcode installed:

```sh
cd MacOS
bash scripts/build.sh
open build/VisualizerNG.app
```

See the [macOS documentation](MacOS/README.md) for build, permission, and release packaging details. The macOS implementation and supplied ZIP are included unchanged.

## Validation

```powershell
dotnet run --project Windows/Visualizer.Checks -c Release
dotnet run --project Windows/Visualizer.RenderChecks -c Release
```

Rendering checks require an OpenGL 3.3-capable desktop session. Add `-- --ui` to the rendering command to verify controls and layout. See the Windows README for optional live audio and animation checks.

## Repository layout

- `Windows/Visualizer.App`: C#/.NET 8, WPF, NAudio, and OpenTK standalone app.
- `Windows/Visualizer.Checks` and `Windows/Visualizer.RenderChecks`: audio and GPU/UI checks.
- `Windows/VST3`: separate experimental JUCE track visualizer port; see its [README](Windows/VST3/README.md).
- `MacOS`: SwiftUI, ScreenCaptureKit, and Accelerate app, Xcode project, assets, and original packaged ZIP.

## Contributing

Issues and pull requests are welcome. Include your operating system, build version, and reproduction steps in bug reports. Run the relevant checks before submitting Windows changes.

Dependency license texts and notices are preserved in `Windows/Visualizer.App/Legal` and `Windows/VST3/ThirdParty`.

## License

VisualizerNG is available under the [MIT License](LICENSE). Third-party components retain their own licenses.

