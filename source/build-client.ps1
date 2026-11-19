param([string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repositoryRoot 'build' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
New-Item -ItemType Directory -Path (Join-Path $OutputDirectory 'data') -Force | Out-Null
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (!(Test-Path -LiteralPath $compiler)) { throw '.NET Framework 4.x compiler is required on Windows x64.' }
$sourceFiles = @('Native.cs','InputCapture.cs','Settings.cs','Bridge.cs','Client.cs','Preview.cs','PreviewDpi.cs','KeyboardMap.cs','Tests.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
& $compiler /nologo /target:winexe /platform:x64 /optimize+ /codepage:65001 "/out:$OutputDirectory/DS3CameraClient.exe" /reference:System.dll /reference:System.Core.dll /reference:System.Drawing.dll /reference:System.Windows.Forms.dll /reference:System.Xml.dll $sourceFiles
if ($LASTEXITCODE -ne 0) { throw "Client build failed: $LASTEXITCODE" }
$config = Join-Path $OutputDirectory 'camera-settings.xml'
if (!(Test-Path -LiteralPath $config)) { Copy-Item -LiteralPath (Join-Path $repositoryRoot 'camera-settings.example.xml') -Destination $config }
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'licenses') -Destination $OutputDirectory -Recurse -Force
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'THIRD-PARTY-NOTICES.md') -Destination $OutputDirectory -Force
Copy-Item -LiteralPath (Join-Path $repositoryRoot 'LICENSE') -Destination $OutputDirectory -Force
Write-Output "Built frontend in $OutputDirectory; camera DLL unchanged."
