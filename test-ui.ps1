$ErrorActionPreference = 'Stop'
$checkDirectory = Join-Path ([IO.Path]::GetTempPath()) ('RiverDesk-UI-Checks-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $checkDirectory | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
if (-not (Test-Path -LiteralPath $compiler)) {
    $compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe'
}
$sources = 'VersionInfo.cs', 'DeepCleanC-v1.2.0.cs', 'AppShell.cs', 'ForceDeleteFeature.cs',
    'UninstallerFeature.cs', 'UpdateForm.cs', 'tests/UiRegressionCheck.cs' |
    ForEach-Object { (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot $_)).ProviderPath }
$executable = Join-Path $checkDirectory 'UiRegressionCheck.exe'
& $compiler /nologo /warn:4 /target:exe /main:UiRegressionCheck /r:System.ServiceProcess.dll "/out:$executable" @sources
if ($LASTEXITCODE -ne 0) { throw 'UI regression check compilation failed.' }
$runOptions = @{
    FilePath = $executable
    WindowStyle = 'Hidden'
    PassThru = $true
    Wait = $true
    RedirectStandardOutput = Join-Path $checkDirectory 'result.log'
    RedirectStandardError = Join-Path $checkDirectory 'error.log'
}
$process = Start-Process @runOptions
Get-Content -LiteralPath (Join-Path $checkDirectory 'result.log'), (Join-Path $checkDirectory 'error.log')
if ($process.ExitCode -ne 0) { throw 'UI regression checks failed.' }
