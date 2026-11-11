# Spec v0.2 (revised from v0.1 draft)

Goal: reduce errors and cognitive load in live Standard games by adding decisions on top of HDT's observations. Read-only; the user plays manually.

## Scope
In: exact lethal, then action ranking, then threat warnings, for one deck (Zoo Warlock, Standard).
Out: deck/secret/hand tracking (HDT does it), opponent modelling, runtime LLM, ML, automation of any kind, memory reading.

## Claim discipline (hard rule)
`Proven` only if the whole line is modelled. Unmodelled card or enemy secret -> `Likely`. Budget/cancel -> `Inconclusive`. Never present a heuristic score as a win probability.

## Architecture
HDT event/OnUpdate -> `SnapshotAdapter` (HDT thread) -> immutable `GameSnapshot` -> change detection (`Signature`) -> cancel previous analysis -> `LethalSolver` (worker) -> `Presenter` -> `CopilotPanel` (UI thread, drop stale results).

## Milestones
M0 research -> M1 engine -> M2 plugin + recorder (user validates live) -> M3 deck support from real card text -> M4 ranking -> M5 threat warnings + benchmarks.

## Targets (to be measured, not promised)
Lethal result < 250 ms; no visible FPS impact; idle CPU negligible; no unbounded memory.

## Acceptance for each milestone
Tests green on the user's PC for engine work; live behaviour only counts as verified after the user runs a real game and the recorded snapshots are reviewed.
