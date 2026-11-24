param([string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repositoryRoot 'build' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$sources = @('PreviewHost.cs','PreviewDpi.cs','UiMotion.cs','UiTextCache.cs','DarkDialog.cs','Preview.cs','KeyboardMap.cs','Settings.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 "/win32icon:$PSScriptRoot/assets/camera-icon.ico" "/out:$OutputDirectory/DS3CameraPreview.exe" "/resource:$PSScriptRoot/assets/camera-icon.png,DS3Camera.camera-icon.png" "/resource:$PSScriptRoot/assets/game-wordmark.png,DS3Camera.game-wordmark.png" "/resource:$PSScriptRoot/assets/bonfire-symbol.png,DS3Camera.bonfire-symbol.png" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xml.dll $sources
if ($LASTEXITCODE -ne 0) { throw "Preview build failed: $LASTEXITCODE" }
Write-Output "Built isolated appearance preview in $OutputDirectory."
