# VisualizerNG

A standalone Windows audio visualizer built with C#/.NET 8, WPF, NAudio WASAPI loopback capture, and OpenTK OpenGL rendering.

## Build

```powershell
cd .\Visualizer.App
dotnet restore
dotnet build
```

## Run

```powershell
cd .\Visualizer.App
dotnet run
```

## Publish Self-Contained

```powershell
cd .\Visualizer.App
dotnet publish -c Release
```

The self-contained app is written to:

```text
Visualizer.App\bin\Release\net8.0-windows\win-x64\publish\VisualizerNG.exe
```

That publish folder can be moved to another Windows x64 machine without installing the .NET runtime. Keep the files in the folder together.

Play audio from Spotify, YouTube, or another Windows app, select the active speakers/headphones in the output-device dropdown, then switch between `Oscilloscope` and `Spectrum`.

The app starts with `Follow Windows default output` selected and follows output changes within three seconds. You can also select a specific device to keep capturing that output.

If the line stays flat, check the status below the output selector (hover to read the full message). `No audio detected` means you should select the speakers/headphones actually playing the song. If your browser or music app uses a different output in Windows Volume mixer, select that output in the visualizer too. Audio must be playing and unmuted. The app captures Windows playback directly; microphone access is not required.

`F11` toggles fullscreen visualization mode. `Escape` exits fullscreen.

Choose from `Oscilloscope`, `Circular oscilloscope`, `Spectrum`, and `Radial spectrum`. The circular oscilloscope wraps the live waveform around a ring; the radial spectrum places frequency bars around a circle. Both stay circular when you resize the window.

The `Colors` dropdown offers Lagoon, Electric blue, Neon violet, Sunset, and Lime for every mode. Color choices are remembered separately for each mode while the app is open. Spectrum peaks and the radial spectrum's inner ring use matching accent colors.

## Verify audio capture

Run `dotnet run --project Visualizer.Checks` for the audio analysis checks. Add `-- --live` to play a quiet two-second test tone through the Windows default output and verify that loopback capture receives it.

With the visualizer open in windowed mode and following the Windows default output, run `powershell -ExecutionPolicy Bypass -File .\Visualizer.Checks\Check-Animation.ps1` to verify that the actual on-screen meter animates during playback. This catches rendering startup failures that the audio-only checks cannot detect.

### Shapes, rotation, and CRT controls

Choose circular, triangle, square, pentagon, hexagon, octagon, or star oscilloscope shapes in the Visualizer menu. Closed shapes use a mirrored audio loop with full displacement around the outline, including the join.

The **Rotation** and **CRT effects** groups sit next to each other below the main controls.

- **Rotation:** select **Off**, **2D spin**, or **3D spin**. Selecting a spin mode starts the animation immediately. Angle adjusts the Z rotation offset; Tilt and Turn are available in 3D spin. Off returns to an unrotated view while preserving offsets. Reset clears all offsets and stops rotation. The shapes remain planar waveforms viewed in perspective in 3D mode.
- **CRT effects:** select **Off**, **Snow**, **Interlace**, or **Snow + Interlace**. Each effect has its own intensity slider, enabled when relevant. Interlace has three downward-moving scan bands with a subtle glow so they remain visible without snow. Choices remain selected when switching visualizers or entering fullscreen during the session.

Run `dotnet run --project Visualizer.RenderChecks -c Release` for GPU rendering checks across all modes, rotation, CRT effects, and viewport sizes. An OpenGL 3.3-capable desktop session is required. Add `-- --ui` to check mode-dependent controls and WPF layout at minimum and normal window widths and save control previews to the temporary directory.