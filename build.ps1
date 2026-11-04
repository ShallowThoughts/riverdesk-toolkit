param(
    [string]$NsisCompiler = (Join-Path $env:LOCALAPPDATA 'tauri\NSIS\makensis.exe'),
    [switch]$Sign,
    [string]$SigningKey = (Join-Path $env:LOCALAPPDATA 'RiverDeskToolkit\release.dpapi'),
    [string]$Notes = 'RiverDesk Toolkit first installer release.'
)

$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Security
$encoding = [Text.UTF8Encoding]::new($false)
$dist = Join-Path $PSScriptRoot 'dist'
New-Item -ItemType Directory -Path $dist -Force | Out-Null
$publicKeyPath = Join-Path $PSScriptRoot 'update-public.xml'
$rsa = $null
try {
    if ($Sign) {
        $rsa = [Security.Cryptography.RSACryptoServiceProvider]::new(3072, [Security.Cryptography.CspParameters]::new(24))
        $rsa.PersistKeyInCsp = $false
        $scope = [Security.Cryptography.DataProtectionScope]::CurrentUser
        if (Test-Path -LiteralPath $SigningKey) {
            $plain = [Security.Cryptography.ProtectedData]::Unprotect([IO.File]::ReadAllBytes($SigningKey), $null, $scope)
            try { $rsa.FromXmlString($encoding.GetString($plain)) } finally { [Array]::Clear($plain, 0, $plain.Length) }
        } elseif (Test-Path -LiteralPath $publicKeyPath) {
            throw 'The signing key is missing. Recover it before publishing.'
        } else {
            New-Item -ItemType Directory -Path (Split-Path -Parent $SigningKey) -Force | Out-Null
            $plain = $encoding.GetBytes($rsa.ToXmlString($true))
            try {
                $encrypted = [Security.Cryptography.ProtectedData]::Protect($plain, $null, $scope)
                $stream = [IO.File]::Open($SigningKey, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
                try { $stream.Write($encrypted, 0, $encrypted.Length) } finally { $stream.Dispose() }
            } finally { [Array]::Clear($plain, 0, $plain.Length) }
        }
        if (Test-Path -LiteralPath $publicKeyPath) {
            if ([IO.File]::ReadAllText($publicKeyPath).Trim() -ne $rsa.ToXmlString($false)) { throw 'Update signing key mismatch.' }
        } else {
            [IO.File]::WriteAllText($publicKeyPath, $rsa.ToXmlString($false), $encoding)
        }
    }
    if (-not (Test-Path -LiteralPath $publicKeyPath)) { throw 'Update verification public key is missing.' }

    $source = Get-Content -Raw -LiteralPath (Join-Path $PSScriptRoot 'UpdateForm.cs')
    $version = [regex]::Match($source, 'Number = "(\d+\.\d+\.\d+)\.0"').Groups[1].Value
    if (-not $version) { throw 'Application version not found.' }
    $app = Join-Path $dist 'RiverDeskToolkit.exe'
    $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
    if (-not (Test-Path -LiteralPath $csc)) { $csc = Join-Path $env:WINDIR 'Microsoft.NET\Framework\v4.0.30319\csc.exe' }
    $sources = 'DeepCleanC-v1.2.0.cs', 'AppShell.cs', 'ForceDeleteFeature.cs', 'UninstallerFeature.cs', 'UpdateForm.cs' |
        ForEach-Object { Join-Path $PSScriptRoot $_ }
    $arguments = '/nologo', '/warn:4', '/target:winexe', '/optimize+', '/platform:anycpu', '/r:System.ServiceProcess.dll',
        "/resource:$publicKeyPath,RiverDesk.UpdatePublicKey", "/out:$app"
    & $csc @arguments @sources
    if ($LASTEXITCODE -ne 0) { throw 'Application compilation failed.' }

    & $NsisCompiler "/DVERSION=$version" (Join-Path $PSScriptRoot 'installer.nsi')
    if ($LASTEXITCODE -ne 0) { throw 'Installer compilation failed.' }
    $installer = Join-Path $dist 'RiverDeskToolkit-Setup.exe'
    if ($Sign) {
        $hash = (Get-FileHash -LiteralPath $installer -Algorithm SHA256).Hash.ToLowerInvariant()
        $newline = [string][char]10
        $payload = @($version, $hash, [Convert]::ToBase64String($encoding.GetBytes($Notes))) -join $newline
        $signature = [Convert]::ToBase64String($rsa.SignData($encoding.GetBytes($payload), [Security.Cryptography.CryptoConfig]::MapNameToOID('SHA256')))
        [IO.File]::WriteAllText((Join-Path $dist 'update.txt'), $payload + $newline + $signature + $newline, $encoding)
    }
    Get-Item -LiteralPath $installer
} finally {
    if ($rsa) { $rsa.Dispose() }
}
