# Builds Release and zips the plugin for installation: extract the folder into %APPDATA%\HearthstoneDeckTracker\Plugins
param([string]$Out = "$PSScriptRoot\..\dist")
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
dotnet test "$root\tests\Copilot.Tests" -c Release
if ($LASTEXITCODE -ne 0) { throw "tests failed; not packaging" }
dotnet build "$root\src\Copilot.HdtPlugin\Copilot.HdtPlugin.csproj" -c Release
if ($LASTEXITCODE -ne 0) { throw "build failed" }
$bin = "$root\src\Copilot.HdtPlugin\bin\Release\net472"
$stage = Join-Path $Out "HearthstoneCopilot"
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $stage | Out-Null
Copy-Item "$bin\HearthstoneCopilot.dll", "$bin\Copilot.Engine.dll" $stage
@"
Hearthstone Copilot - install
1. Close Hearthstone Deck Tracker.
2. Copy this folder ("HearthstoneCopilot") into %APPDATA%\HearthstoneDeckTracker\Plugins
3. Start HDT, Options > Tracker > Plugins, enable "Hearthstone Copilot".
Logs and recorded snapshots: %APPDATA%\HearthstoneCopilot
Built against HDT 1.59.11. Read-only: it never touches the game client.
"@ | Set-Content "$stage\INSTALL.txt"
$zip = Join-Path $Out "HearthstoneCopilot-0.2.0.zip"
Remove-Item $zip -ErrorAction SilentlyContinue
Compress-Archive -Path $stage -DestinationPath $zip
Get-Item $zip | Select-Object Name, Length
