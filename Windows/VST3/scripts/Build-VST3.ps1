param([string]$BuildDirectory = (Join-Path $env:LOCALAPPDATA 'TrackVisualizerBuild'), [switch]$SkipTests)
$ErrorActionPreference = 'Stop'
$source = Split-Path $PSScriptRoot -Parent
$cmake = (Get-Command cmake -ErrorAction SilentlyContinue).Source
if (!$cmake) {
    $vswhere = Join-Path ${env:ProgramFiles(x86)} 'Microsoft Visual Studio\Installer\vswhere.exe'
    if (!(Test-Path $vswhere)) { throw 'Install Visual Studio 2022 Build Tools with Desktop development with C++ and CMake.' }
    $vs = & $vswhere -latest -products '*' -requires Microsoft.VisualStudio.Component.VC.Tools.x86.x64 -property installationPath
    $cmake = Join-Path $vs 'Common7\IDE\CommonExtensions\Microsoft\CMake\CMake\bin\cmake.exe'
}
if (!(Test-Path $cmake)) { throw 'CMake was not found. Install the Visual Studio CMake tools.' }
New-Item -ItemType Directory -Force $BuildDirectory | Out-Null
$juce = Join-Path $BuildDirectory 'JUCE'
if (!(Test-Path "$juce\CMakeLists.txt")) {
    git clone --depth 1 --branch 8.0.12 https://github.com/juce-framework/JUCE.git $juce
    if ($LASTEXITCODE) { throw 'JUCE download failed' }
}
$revision = git -C $juce rev-parse HEAD
if ($revision -ne '29396c22c93392d6738e021b83196283d6e4d850') { throw 'JUCE checkout does not match pinned 8.0.12 revision.' }
$build = Join-Path $BuildDirectory 'build'
& $cmake -S $source -B $build -G 'Visual Studio 17 2022' -A x64 "-DFETCHCONTENT_SOURCE_DIR_JUCE=$juce"
if ($LASTEXITCODE) { throw 'Configure failed' }
& $cmake --build $build --config Release --parallel 4
if ($LASTEXITCODE) { throw 'Build failed' }
if (!$SkipTests) {
    $ctest = Join-Path (Split-Path $cmake) 'ctest.exe'
    & $ctest --test-dir $build -C Release --output-on-failure
    if ($LASTEXITCODE) { throw 'Validation failed' }
}
$dist = Join-Path $source 'dist\Windows-x64'
New-Item -ItemType Directory -Force $dist | Out-Null
Copy-Item -LiteralPath "$build\TrackVisualizer_artefacts\Release\VST3\Track Visualizer.vst3" -Destination $dist -Recurse -Force
Write-Host "Built: $dist\Track Visualizer.vst3"
