# M0 research findings

Date: 2026-11-11. "Verified" = observed on the target PC or in upstream source during this work.

## HDT on the target machine (verified)
- HDT 1.59.11.9025 at `%LOCALAPPDATA%\HearthstoneDeckTracker\app-1.59.11\HearthstoneDeckTracker.exe`; runtime CLR v4.0.30319 (.NET Framework), WPF.
- Bundled: HearthDb 37.0.2, HearthMirror 32.4.2, MahApps.Metro 1.6.5.1, Newtonsoft.Json 13.0.0.
- Plugin folder: `%APPDATA%\HearthstoneDeckTracker\Plugins` (exists, empty).
- Windows 11 Pro, .NET Framework release 533509 (4.8.1), .NET runtimes 6-10 present; SDK 8.0.428 and Git installed during this work.

## Plugin interface (verified by reflection on the installed exe)
`Hearthstone_Deck_Tracker.Plugins.IPlugin`: `Name`, `Description`, `ButtonText`, `Author`, `Version`, `MenuItem` (properties); `OnLoad()`, `OnUnload()`, `OnButtonPress()`, `OnUpdate()` (methods).
Wiki: target .NET Framework 4.7.2, reference the HDT exe, do not ship dependencies HDT already loads, a plugin that throws on load is disabled, `OnUpdate` fires about every 100 ms.

## Game state access (verified: members exist; semantics unverified)
- `Hearthstone_Deck_Tracker.API.Core.Game` -> `GameV2`: `Entities` (`Dictionary<int, Entity>`), `PlayerEntity`, `OpponentEntity`, `GetTurnNumber()`, `IsRunning`, `IsInMenu`, `CurrentFormatType`, `OpponentSecretCount`, `IsConstructedMatch`.
- `Entity`: `GetTag(GameTag)`, `Tags`, `IsInPlay/IsInHand/IsMinion/IsInSecret`, `IsControlledBy(int)`, `Card`, `CardId`. No Zone/Controller/Exhausted/Frozen properties: raw tags are required.
- `GameEvents` (all `ActionList`): `OnGameStart/End`, `OnTurnStart(ActivePlayer)`, `OnInMenu`, Player/Opponent card events, `OnEntityWillTakeDamage`.
- Threading: GameV2 has no locking; `Entities` is a plain Dictionary. Read it only from the HDT thread that calls `OnUpdate`/events; hand a copied snapshot to worker threads. This is why the adapter runs in `OnUpdate` and only the solver runs on a worker.
- Hidden information: opponent hand/deck cards are unknown entities; the adapter reads only public zones (play, secret count).

## Data sources
- HearthDb ships with HDT (card database, deck-code serializer). Used by `tools/DeckDump` for real card text.
- HearthSim/hsdata: CardDefs.xml extracted from the client; no license file visible on the repo page (not needed: HearthDb already covers us).

## Simulator reuse
- SabberStone (HearthSim and forks) exists but is a standalone C# simulator; I found no evidence it tracks the current Standard pool, and I could not check its last commit from this environment. Treated as **not reusable** for Standard 2026. Decision: small own engine with explicit coverage boundaries (see decisions.md).

## Blockers / open items
- Attack readiness (summoning sickness, Rush, Windfury counters) is derived from tags; must be validated on real games.
- Card effects of the user's deck are 2026 cards unknown to this author; M3 starts from `DeckDump` output, not memory.
- ToS: the plugin only reads HDT state and displays advice; the user plays manually. Do not use in tournaments or modes that forbid assistance.
