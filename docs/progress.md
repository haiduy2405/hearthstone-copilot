# Progress

## Milestones
| MS | Scope | State |
|----|-------|-------|
| M0 | Research, ADRs | Done (see research.md) |
| M1 | Engine lethal + tests | Code written; build/test results below |
| M2 | HDT plugin: load, snapshot, panel, recorder | Code written; **live verification pending** |
| M3 | Deck specialisation (Zoo Warlock) | Not started; needs DeckDump output |
| M4 | Action ranking | Not started |
| M5 | Threat warnings, benchmarks, packaging | Not started |

## Verification log
Only entries with a command or observation behind them.

- 2026-11-11 `dotnet test tests\Copilot.Tests` on the user's PC (SDK 8.0.428): 27 passed, 0 failed.
- 2026-11-11 `dotnet build src\Copilot.HdtPlugin -c Release` against HDT 1.59.11: succeeded, 0 warnings, 0 errors.
- 2026-11-11 `DeckDump` decoded the Zoo Warlock deck code (16 distinct cards, Standard, Gul'dan) -> `docs/deck-cards.md`.
- 2026-11-11 `tools\deploy.ps1` copied `HearthstoneCopilot.dll` + `Copilot.Engine.dll` to `%APPDATA%\HearthstoneDeckTracker\Plugins\HearthstoneCopilot`. **Not yet loaded by HDT** (HDT was running during deploy; needs restart).

## Next live check (user)
1. Restart HDT, enable the plugin (Options > Tracker > Plugins), play a game.
2. Expect the panel on your turns and files in `%APPDATA%\HearthstoneCopilot\snapshots`; read `plugin.log` for errors.
3. Send or commit a few snapshots; they become the first real fixtures.
