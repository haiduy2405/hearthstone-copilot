# Known limitations (v0.2)

## Modelled
Attacks (minion and hero), Taunt, Divine Shield, Poisonous, Windfury, Immune, Stealth, armor, enemy "Deathrattle: Gain N Armor", mana, 7-minion board limit, and these cards (see `CardRules.cs`, texts in `docs/deck-cards.md`):
Blasphemy, Tachyon Barrage, Entropic Continuity, Floating Lessons, Vaultkeeper, Ruinous Velocidrake (Rush). Search plays the fewest cards that win.

## Claim levels
- `Proven`: every card involved is modelled and no rule is marked unverified.
- `Likely`: a legal line exists but an enemy secret, an unmodelled card, or an **unverified rule** is involved. Unverified today: Tachyon Barrage (assumes the enemy hero is a valid target) and Floating Lessons (assumes +X/+X with X = 2 x current Cost).
- `Inconclusive`: node budget (200k) or cancellation hit; never reported as "no lethal".

## Deliberate conservative choices
- Blasphemy is doubled only when strictly the highest Cost in hand (a tie is treated as a single cast).
- Tachyon Barrage is played only when the enemy has no minion (random split otherwise).
- Velocidrake needs own hero health > 3 (its Battlecry may cast a Shred of Time: 3 damage to us); its copy effect is ignored.
- Zakajz is not played by the search: it only raises spell Cost and cannot help a lethal line.

## Not modelled
Discover/random effects (Cursed Catacombs), delayed Rituals, Kazakus choices, freeze effects, Lifesteal, Reborn, weapon durability, deck/draw effects, enemy hand and hero powers, our own Deathrattles other than summons, effects granted by enchantments (only their resulting tags are seen), Spell Damage bonuses (ignored = conservative).
Non-Taunt enemy minions are never attacked (they cannot raise face damage in the modelled rules).

## Unverified against live games
- Attack readiness is derived from raw tags (`NUM_TURNS_IN_PLAY`, `NUM_ATTACKS_THIS_TURN`, `FROZEN`, `CANT_ATTACK`, Charge/Rush/Windfury).
- Hand Cost is read from the `COST` tag; Mawman Cultist's aura should show up there.
- Threat warning uses the visible board only (hidden hand, secrets, Charge minions and burn are not counted), so it can only under-estimate danger.
- Panel appears only in constructed-style matches; it cannot be moved or resized yet.

## v0.4.0 additions
- Mulligan advice is a hand-written table for the current Zoo list; unknown cards fall back to "cheap minion = keep". It does not know the opponent's class. The tags used to detect the mulligan phase (`MULLIGAN_STATE`, `FIRST_PLAYER`) have not been seen live yet.
- Charge/Rush bodies: only cards whose entire text is keywords are played by the solver. A Charge card with any extra text is ignored (conservative).
- DANGER is still a visible-board upper bound; enemy hand, hero power and weapons are not counted.
