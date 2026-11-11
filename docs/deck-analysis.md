# Deck analysis: Zoo Warlock (Standard, Gul'dan)

Source of truth for card text: `docs/deck-cards.md` (HearthDb 37.0.2 bundled with HDT 1.59.11). Nothing below is taken from memory; items marked **TODO-dump** need the generated token's real text first.

Deck code: `AAECAf0GAtvgB+35Bw7QngbTngaEmQeirQekrQelrQeorQf2wQfF8AeB9geK9geN9gfp+Qfr+QcAAA==`

## What can produce lethal here
| Source | Cards | Deterministic? |
|---|---|---|
| Board attacks | all minions (2 copies each, Kazakus 1) | yes, already solved in v0.1 |
| Face burn | Blasphemy (2 to all enemies; again if highest Cost in hand) | yes; the "highest Cost" tie rule is unverified, treat only a strict highest as doubled until checked |
| Face burn | Tachyon Barrage (6 split among all enemies, immune to spell power) | only when the enemy has no minions (all 6 must hit the hero) |
| Attack buffs | Entropic Continuity (+1/+1 to your minions), Vaultkeeper (+2/+2 adjacent, position dependent) | yes |
| Attack buffs | Floating Lessons (stats = 2x its Cost to a chosen minion and a random one in hand) | chosen minion yes; random one no |
| Cost shaping | Mawman Cultist (ALL spells +1 Cost), Zakajz (minions -1, spells +1 in hand and deck) | read real costs from hand tags instead of modelling |
| Clearing Taunt | Blasphemy AoE, Rush from Ruinous Velocidrake (5/5 Rush: can hit minions only on the turn it is played) | yes |
| Defence | Glacial Shard (Freeze an enemy), Twilight Egg | not lethal-relevant |

## Not provable (always cap at `Likely` or ignore)
Cursed Catacombs (Discover), Ritual of C'Thun and Ritual of Summoning (delayed effects), Godfather Kazakus (custom choices), Cult Neophyte (opponent spell cost), Ruinous Velocidrake's copy effect (depends on Shreds of Time in the deck; our own deck contents are known to HDT, so this can be modelled later), Twilight Timehopper's drawback (3 damage to our own hero when a Shred is drawn: a survival risk, not a lethal source).

## M3 modelling order
1. **Hand play + mana**: read hand costs from tags; legal plays = minions (new attackers are summoning sick unless Rush/Charge) and spells.
2. **Blasphemy and Tachyon Barrage** as face/AoE damage with Taunt-clearing interaction (AoE first, then attacks).
3. **Entropic Continuity** and **Vaultkeeper** as attack buffs inside the search.
4. Velocidrake Rush as a Taunt-clearing attacker.
5. Fixtures per interaction (true lethal, false lethal from Taunt, false lethal from mana, Mawman tax making a line illegal).

## Open items (need data before coding)
- **TODO-dump:** Shred of Time, Whelp (Twilight Egg), C'thraxxi 2/4 Taunt (Ritual of Summoning). Extend `DeckDump` with a name lookup.
- Verify in a live game: Blasphemy tie rule; whether Tachyon Barrage can hit the enemy hero; what counts as a valid target set for "all enemies".
