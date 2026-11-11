# Decisions (ADR log)

## ADR-1: Three code projects, not nine
`Copilot.Engine` (netstandard2.0), `Copilot.HdtPlugin` (net472, includes the WPF panel), `Copilot.Tests`. Splitting State/Rules/Search/Strategy/Overlay/Persistence adds overhead with no benefit for a single-user tool; split later only when a module gets its own consumer.

## ADR-2: Engine targets netstandard2.0, runs in-process
The net472 plugin can reference a netstandard2.0 library directly. No IPC, no extra process. Tests run on modern .NET without Hearthstone or HDT.

## ADR-3: Own bounded engine, no third-party simulator
No maintained simulator covering the current Standard pool was found. Engine models only what it can prove and reports everything else as unsupported.

## ADR-4: Three-level claim discipline
Result status is `Proven` / `Likely` / `None` / `Inconclusive`. Any unmodelled enemy or own card, or any enemy secret, caps a found line at `Likely`. Budget or cancellation yields `Inconclusive`, never `None`. A false "lethal" is the worst failure mode.

## ADR-5: Deck-driven coverage
Mechanic coverage is prioritised by the user's actual deck (Zoo Warlock, Standard), starting from real card text via HearthDb, instead of chasing the whole card pool.

## ADR-6: Replay before live trust
The plugin records a snapshot (including raw tags) at each own turn start. Fixtures with an `expected` status are replayed in tests. Adapter assumptions are validated against these recordings.

## ADR-7: Deferred
Opponent modelling, runtime LLM advisor, ML, persistence of statistics: out of scope until lethal + ranking are measured on real games.

## ADR-8: No heuristic action ranking (yet)
Trade-vs-face / develop-vs-hold scores cannot be validated without a large set of reviewed games, and a confident wrong suggestion is worse than none. The panel therefore shows only things that are exact (lethal lines) or strictly bounded (visible-board danger). Ranking can return once reviewed fixtures exist to test it against.

## ADR-9: Conservative modelling of ambiguous rules
When card text is ambiguous the engine assumes the outcome that is worse for us (Blasphemy tie, Tachyon needing an empty enemy board, Velocidrake hero margin). Rules whose reading still needs a live check are flagged `Unverified` in `CardRules` and cap the claim at `Likely`.

## ADR-10: Zakajz stays out of the search
Its Battlecry makes spells cost more and only discounts minions that cannot attack this turn, so it can never create a lethal line the other cards do not already find.
