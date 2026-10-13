# Use a project-local runtime cache when the system TEMP drive has no space.
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$env:DOTNET_BUNDLE_EXTRACT_BASE_DIR = Join-Path $root '.local/runtime'
New-Item -ItemType Directory -Force $env:DOTNET_BUNDLE_EXTRACT_BASE_DIR | Out-Null
Start-Process -FilePath (Join-Path $root 'dist/ZYVORA-Setup-0.1.0-win-x64.exe') -WindowStyle Hidden
