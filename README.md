# Hearthstone Copilot

Read-only tactical companion for **Hearthstone Deck Tracker (HDT)**. HDT keeps tracking decks, hands and secrets; this plugin turns HDT's game state into decisions. First feature: **exact lethal detection** shown in a small click-through panel.

- Never touches the game client or memory. No input automation. Information comes only from HDT's own state.
- Works offline. No cloud AI at runtime.
- Status: **v0.1, M1**. See [docs/progress.md](docs/progress.md) for what is verified and what is not.

## Requirements
Windows 10/11, HDT 1.59.x (installed), .NET SDK 8, Git.

## Build and test
```powershell
dotnet test tests\Copilot.Tests          # engine tests (no Hearthstone needed)
.\tools\deploy.ps1                       # builds plugin and copies it to HDT\Plugins\HearthstoneCopilot
```
Close HDT before deploying. Then start HDT and enable the plugin in Options > Tracker > Plugins.
If HDT updates and the folder name changes, pass `-p:HdtDir="...\app-X.Y.Z"` to `dotnet build`.

## Data and logs
`%APPDATA%\HearthstoneCopilot\` holds `plugin.log` and `snapshots\*.json` (one per own turn start). These are the raw material for offline fixtures; they are git-ignored.

## Layout
| Path | Purpose |
|---|---|
| `src/Copilot.Engine` | netstandard2.0: state model, lethal solver, text classifier, presenter |
| `src/Copilot.HdtPlugin` | net472: HDT adapter, panel, snapshot recorder |
| `tests/Copilot.Tests` | unit tests and fixture replay |
| `tools/DeckDump` | prints real card data for a deck code (via HearthDb) |
| `fixtures/` | snapshot fixtures (`expected` field = required solver status) |
| `docs/` | spec, research, decisions, progress, limitations |

## What the panel shows
`LETHAL: YES (verified)` / `LIKELY` / `NO (modelled actions)` / `UNKNOWN`, the ordered steps (card plays and attacks) and damage vs. required. On `NO` it adds a `DANGER`/`Watch` line when the visible enemy board threatens your health. See `docs/known-limitations.md` for exactly what is modelled.

## Panel behaviour (v0.3)
Green status dot always shows during a match. Big (72pt) red `LETHAL` (verified) or `LETHAL?` (a line exists but something is unmodelled/unverified); dark red `DANGER` when the visible enemy board can kill you; nothing otherwise. Press **F8** (or Plugins menu > Hearthstone Copilot > Show/hide details) to see the steps. Settings: `%APPDATA%\HearthstoneCopilot\config.json` (`Left`, `Top`, `Scale`, `Opacity`, `Hotkey` F1-F12, `ClickThrough`, `Sound`, default on: beep per level); reload from the same menu.
