# AGENTS.md

Handoff notes for any coding agent working in this repo. All repo content is English.

## Rules
1. Correctness over coverage. A tactical claim is only "Proven" if every card involved is modelled. Anything unmodelled caps the result at "Likely"; an exhausted search budget is "Inconclusive", never "None".
2. Never fabricate: builds, tests, card text, API availability. Card text comes from HearthDb (`tools/DeckDump`), not memory.
3. Read-only toward the game: no memory reading, no input automation, no hidden-information extraction.
4. Do not rebuild what HDT already does (deck/hand/secret tracking).
5. Engine (`Copilot.Engine`) must stay netstandard2.0 and free of HDT/WPF/Newtonsoft references. All HDT-specific code lives in `Copilot.HdtPlugin`.
6. HDT assemblies are compile-time only (`Private=false`). Never commit HDT binaries.
7. Every new mechanic ships with unit tests, including at least one "false lethal" test.
8. Do not commit secrets, logs or personal snapshots (`.gitignore` covers them).

## Commands
- `dotnet test tests\Copilot.Tests`
- `.\tools\deploy.ps1` (close HDT first)
- `dotnet run --project tools\DeckDump -- "<deckcode>" docs\deck-cards.md`

## Conventions
- Small commits, imperative messages. Update `docs/progress.md` when a milestone item changes state; keep it factual (verified / unverified).
- Adapter assumptions (attack readiness, Rush, Windfury tags) are unverified until checked against recorded snapshots; record the outcome in `docs/known-limitations.md`.
