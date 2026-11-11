# Builds the plugin (Release) and copies it into HDT's Plugins folder.
# Close HDT first: it locks plugin DLLs while running.
param([string]$HdtPlugins = "$env:APPDATA\HearthstoneDeckTracker\Plugins")
$ErrorActionPreference = "Stop"
$root = Split-Path $PSScriptRoot -Parent
dotnet build "$root\src\Copilot.HdtPlugin\Copilot.HdtPlugin.csproj" -c Release
if ($LASTEXITCODE -ne 0) { throw "build failed" }
$out = "$root\src\Copilot.HdtPlugin\bin\Release\net472"
$dest = Join-Path $HdtPlugins "HearthstoneCopilot"
New-Item -ItemType Directory -Force $dest | Out-Null
Copy-Item "$out\HearthstoneCopilot.dll", "$out\Copilot.Engine.dll" $dest -Force
Get-ChildItem $dest | Select-Object Name, Length, LastWriteTime
Write-Host "Deployed to $dest. Start HDT, then enable the plugin: Options > Tracker > Plugins."
