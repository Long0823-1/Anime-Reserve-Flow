$ErrorActionPreference = 'Stop'
$repoPath = Split-Path -Parent $PSScriptRoot
Push-Location $repoPath
try {
 dotnet publish src/App/AnimeReserve.App.csproj -c Release -r win-x64 --self-contained true -o build/release
 if ($LASTEXITCODE -ne 0) { throw 'Publish failed' }
 Copy-Item README.md,LICENSE,THIRD-PARTY-NOTICES.md build/release
 Copy-Item docs build/release/docs -Recurse -Force
 New-Item -ItemType Directory -Force build/release/Source | Out-Null
 Copy-Item src build/release/Source/src -Recurse -Force -Exclude bin,obj
} finally { Pop-Location }
