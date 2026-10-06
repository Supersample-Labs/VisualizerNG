$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient
Add-Type -AssemblyName UIAutomationTypes

# Test the actual running WPF app: capture-only checks cannot detect a stalled render loop.
$appProcess = Get-Process VisualizerNG -ErrorAction Stop | Select-Object -First 1
$root = [System.Windows.Automation.AutomationElement]::FromHandle($appProcess.MainWindowHandle)
$meterCondition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'LevelMeter')
$valueCondition = New-Object System.Windows.Automation.PropertyCondition([System.Windows.Automation.AutomationElement]::AutomationIdProperty, 'ValueText')
$meter = $root.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $meterCondition)
$value = if ($null -ne $meter) { $meter.FindFirst([System.Windows.Automation.TreeScope]::Descendants, $valueCondition) }
if ($null -eq $value) { throw 'Open the visualizer in windowed mode with its meters visible before running this check.' }

dotnet build $PSScriptRoot --nologo
if ($LASTEXITCODE -ne 0) { throw 'Could not build the audio checks.' }
$checkExe = Join-Path $PSScriptRoot 'bin/Debug/net8.0-windows/Visualizer.Checks.exe'
$toneProcess = Start-Process -FilePath $checkExe -ArgumentList '--live' -WindowStyle Hidden -PassThru
try {
    $values = 1..25 | ForEach-Object {
        $value.Current.Name
        Start-Sleep -Milliseconds 200
    }
    $toneProcess.WaitForExit()
    if ($toneProcess.ExitCode -ne 0) { throw 'Live loopback check failed.' }
    if (($values | Where-Object { $_ -ne '0%' }).Count -eq 0 -or
        ($values | Select-Object -Unique).Count -lt 3) {
        throw "The running app did not animate during playback. Meter values: $($values -join ', ')"
    }
    "PASS: The running app meter updates across frames: $($values -join ', ')"
}
finally {
    $toneProcess.Dispose()
}
