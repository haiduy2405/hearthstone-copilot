# Known limitations (v0.1)

- Lethal solver models attacks only: minion and hero attacks, Taunt, Divine Shield, Poisonous, Windfury, Immune, Stealth, armor. **Not modelled:** spells, hero powers, battlecries, deathrattles, auras, Lifesteal, Reborn, Freeze effects, weapon durability, mana. Cards with such text are classified `Unknown` and cap the result at `Likely`.
- Enemy secrets are never identified; any secret caps at `Likely`.
- Attack readiness is derived from raw tags (`NUM_TURNS_IN_PLAY`, `NUM_ATTACKS_THIS_TURN`, `FROZEN`, `CANT_ATTACK`, Charge/Rush/Windfury). **Unverified against live games.**
- Non-taunt enemy minions are never attacked (cannot improve face damage in the modelled rules).
- `TextSupport` judges by the card's base text; effects granted by enchantments are only seen through the resulting tags (Attack, Taunt, ...).
- Panel shows nothing outside Constructed/standard-style matches and cannot be moved or resized yet.
- Windows-specific behaviour (plugin loading, panel click-through, HDT thread assumptions) is verified only by the steps recorded in `progress.md`.
