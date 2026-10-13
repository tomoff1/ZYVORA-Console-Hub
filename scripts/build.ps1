$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $root
$env:DOTNET_CLI_HOME = Join-Path $root '.build'
$env:DOTNET_CLI_TELEMETRY_OPTOUT = '1'
$env:DOTNET_CLI_WORKLOAD_UPDATE_NOTIFY_DISABLE = 'true'
$env:MSBuildEnableWorkloadResolver = 'false'
$buildTemp = Join-Path $root '.local/temp'
New-Item -ItemType Directory -Force $buildTemp | Out-Null
$env:TEMP = $buildTemp
$env:TMP = $buildTemp
dotnet restore src/Zyvora.Desktop/Zyvora.Desktop.csproj -r win-x64 --configfile NuGet.Config -p:SelfContained=true
if ($LASTEXITCODE -ne 0) { throw 'Restauration desktop échouée' }
dotnet msbuild src/Zyvora.Desktop/Zyvora.Desktop.csproj /t:Publish /p:Configuration=Release /p:RuntimeIdentifier=win-x64 /p:SelfContained=true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true "/p:PublishDir=$root\dist\app\" /p:MSBuildEnableWorkloadResolver=false
if ($LASTEXITCODE -ne 0) { throw 'Compilation desktop échouée' }
Compress-Archive -Path dist/app/* -DestinationPath dist/app.zip -Force
$manifest = [ordered]@{}
Get-ChildItem -LiteralPath dist/app -File -Recurse | ForEach-Object { $relative = $_.FullName.Substring((Resolve-Path dist/app).Path.Length + 1).Replace('\','/'); $manifest[$relative] = (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant() }
[IO.File]::WriteAllText((Join-Path $root 'dist/app-manifest.json'), ($manifest | ConvertTo-Json), [Text.UTF8Encoding]::new($false))
dotnet restore src/Zyvora.Setup/Zyvora.Setup.csproj -r win-x64 --configfile NuGet.Config -p:SelfContained=true
if ($LASTEXITCODE -ne 0) { throw 'Restauration installateur échouée' }
dotnet msbuild src/Zyvora.Setup/Zyvora.Setup.csproj /t:Publish /p:Configuration=Release /p:RuntimeIdentifier=win-x64 /p:SelfContained=true /p:PublishSingleFile=true /p:IncludeNativeLibrariesForSelfExtract=true "/p:PublishDir=$root\dist\setup\" /p:MSBuildEnableWorkloadResolver=false
if ($LASTEXITCODE -ne 0) { throw 'Compilation installateur échouée' }
Copy-Item -LiteralPath dist/setup/ZYVORA-Setup.exe -Destination dist/ZYVORA-Setup-0.1.0-win-x64.exe
Copy-Item -LiteralPath dist/app.zip -Destination dist/ZYVORA-0.1.0-win-x64-portable.zip
Get-ChildItem dist/ZYVORA-* -File | ForEach-Object { '{0}  {1}' -f (Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash.ToLowerInvariant(), $_.Name } | Set-Content -LiteralPath dist/SHA256SUMS.txt -Encoding ascii
