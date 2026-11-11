# Progress

## Milestones
| MS | Scope | State |
|----|-------|-------|
| M0 | Research, ADRs | Done (see research.md) |
| M1 | Engine lethal + tests | Done (tests pass on user's PC) |
| M2 | HDT plugin: load, snapshot, panel, recorder | Verified live in 2 games (panel, snapshots, no errors) |
| M3 | Deck specialisation (Zoo Warlock) | Engine done + tested; **hand/cost reading not yet verified live** |
| M4 | Threat warning (visible board) | Done + tested; action ranking intentionally not built (see decisions.md ADR-8) |
| M5 | Benchmarks, packaging | Test-run benchmark done; live CPU/FPS and release zip pending |

## Verification log
Only entries with a command or observation behind them.

- 2026-11-11 `dotnet test tests\Copilot.Tests` on the user's PC (SDK 8.0.428): 27 passed, 0 failed.
- 2026-11-11 `dotnet build src\Copilot.HdtPlugin -c Release` against HDT 1.59.11: succeeded, 0 warnings, 0 errors.
- 2026-11-11 `DeckDump` decoded the Zoo Warlock deck code (16 distinct cards, Standard, Gul'dan) -> `docs/deck-cards.md`.
- 2026-11-11 `tools\deploy.ps1` copied `HearthstoneCopilot.dll` + `Copilot.Engine.dll` to `%APPDATA%\HearthstoneDeckTracker\Plugins\HearthstoneCopilot`. **Not yet loaded by HDT** (HDT was running during deploy; needs restart).

- 2026-11-11 First live game (Standard, HDT 1.59.11): plugin loaded, panel shown, 8 snapshots written, no errors in `plugin.log`. Verified against raw tags: hero health = HEALTH-DAMAGE, board/secret counts, Support classification. **Bug found:** `OnTurnStart` fires before HDT updates turn tags, so those snapshots had MyTurn=false, mana 0, no attacks. Fix: record once per own turn when state is settled (MyTurn and mana > 0), plus an extra snapshot whenever the solver reports Proven/Likely. Also `GameId` now a short id per game.
- 2026-11-11 After fix: build 0 errors, 27/27 tests; **fix not yet deployed/verified live** (HDT was running).
- 2026-11-11 Second live game: panel showed `LETHAL: NO` and a 7-step `LETHAL: LIKELY` on turn 6 (opp 5 health, Taunts 4/4 and 6/6, 8 face damage available). Line checked by hand: legal and sufficient. Saved as `fixtures/live/g1-t6-clear-two-taunts.json` (expected `Likely`). Own Battlecry-only and spell-cost-aura cards no longer cap the result (`InertOnBoard`); `LETHAL: NO` no longer prints warnings. `dotnet test`: 36 passed. **This build not yet deployed** (HDT running).
- 2026-11-11 Engine v2 (card plays: Blasphemy, Tachyon, Entropic, Floating, Vaultkeeper, Velocidrake; mana; board limit; Deathrattle armor; threat warning). `dotnet test`: 52 passed. `dotnet build` plugin: 0 warnings, 0 errors. **Not yet deployed.**
- 2026-11-11 Benchmark (user's PC, Release-like test run, after warm-up), worst-case board (6 attackers, 4 Taunts, 6 playable cards): 18 enemy health -> Proven, 18,759 nodes, 6 ms; 30 health -> budget hit (200,001 nodes), 113 ms, `Inconclusive`; 200 health -> `None` in 7 nodes (bound pruning). No CPU/FPS measurement in a live game yet.
- 2026-11-11 `tools\package.ps1` on the user's PC: Release tests 55 passed, build 0 errors, produced `dist\HearthstoneCopilot-0.2.0.zip` (24 KB: plugin DLL, engine DLL, INSTALL.txt). v0.2.0 **not yet deployed/run live** (HDT was running).
- 2026-11-11 Third live game (v0.2.0): hand read correctly (CardId, Cost from `COST` tag, Kind, buffed stats), 6 turn snapshots + 1 lethal snapshot, no errors. Turn 6 reported `LIKELY` (two Taunts 3/6 and 3/4, 10 face damage vs 9 health); line verified by hand. Saved as `fixtures/live/g2-t6-two-taunts-two-rush-face.json`. Still unobserved live: Mawman Cultist's aura on hand costs, a Tol'vir Vanguard on the enemy board.
- 2026-11-11 v0.3.0 UI per user request: big red `LETHAL` / `LETHAL?` banner, `DANGER` banner, hidden otherwise; steps only on demand (F8 or menu); `config.json` for position, scale, opacity, hotkey, click-through, sound. `dotnet test`: 62 passed; plugin build 0 errors. **Not yet seen live.**
- 2026-11-11 v0.3.0 seen live: loaded, config.json created, no errors; banner hidden when no lethal, user confirmed a banner appeared but too small. v0.3.1: 72pt banner with white border, always-on green status dot, beep (Sound default true; distinct patterns for LETHAL, LETHAL?, DANGER). Build 0 errors, 62 tests pass; **not yet seen live**.

## Next live check (user)
1. Close HDT, run `tools\deploy.ps1`, start HDT (the plugin stays enabled), play a game with this deck.
2. Expect the panel on your turns and files in `%APPDATA%\HearthstoneCopilot\snapshots`; read `plugin.log` for errors.
3. Check in the newest snapshots: `Me.Hand` costs match the real card costs in game (also with Mawman Cultist on board), and `Opp.Board` Deathrattle armor shows for Tol'vir Vanguard. Review `*-lethal.json` files and set `expected` once you confirm what really was possible.
- 2026-11-11 v0.4.0 (build 0 errors, `dotnet test` 74 passed, **not yet seen live**): Mulligan advisor (hand-written ranking for the Zoo list, not win-rate data; reads `MULLIGAN_STATE == INPUT` and `FIRST_PLAYER` — both tags unverified live), generic Charge/Rush hand minions in the solver (keyword-only text), DANGER details list what is not counted. Arena draft advisor **not built**: no legitimately downloadable, current card-score data (HearthArena publishes scores on its site only; the Icy Veins sheets page gives no links; the public Google sheet found dates from 2018 and is not a ranking).
