param([string]$ZigPath = $env:DS3_CAMERA_ZIG, [string]$OutputDirectory = '')
$ErrorActionPreference = 'Stop'
$repositoryRoot = Split-Path -Parent $PSScriptRoot
if (!$OutputDirectory) { $OutputDirectory = Join-Path $repositoryRoot 'build' }
$OutputDirectory = [IO.Path]::GetFullPath($OutputDirectory)
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
if (!$ZigPath) { $command = Get-Command zig -ErrorAction SilentlyContinue; if ($command) { $ZigPath = $command.Source } }
if (!$ZigPath -or !(Test-Path -LiteralPath $ZigPath)) { throw 'Pass -ZigPath C:\path\to\zig.exe (Zig 0.13.0), set DS3_CAMERA_ZIG, or add zig to PATH.' }
$zigVersion = (& $ZigPath version).Trim()
if ($LASTEXITCODE -ne 0 -or $zigVersion -ne '0.13.0') { throw "Expected Zig 0.13.0; found $zigVersion." }
$env:ZIG_GLOBAL_CACHE_DIR = Join-Path $repositoryRoot '.cache/zig-global'
$env:ZIG_LOCAL_CACHE_DIR = Join-Path $repositoryRoot '.cache/zig-local'
$nativeSources = @('CameraDll.cpp','vendor/minhook/src/buffer.c','vendor/minhook/src/hook.c','vendor/minhook/src/trampoline.c','vendor/minhook/src/hde/hde64.c') | ForEach-Object { Join-Path $PSScriptRoot $_ }
& $ZigPath c++ -target x86_64-windows-gnu -x c++ -std=c++17 -O2 -shared @nativeSources -luser32 -lversion -o (Join-Path $OutputDirectory 'DS3CameraCore.dll')
if ($LASTEXITCODE -ne 0) { throw "DLL build failed: $LASTEXITCODE" }
& (Join-Path $PSScriptRoot 'build-client.ps1') -OutputDirectory $OutputDirectory
Write-Output "Built camera client and DLL in $OutputDirectory"
