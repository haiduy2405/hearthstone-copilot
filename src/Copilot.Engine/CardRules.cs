using System.Collections.Generic;

namespace Copilot.Engine
{
    /// <summary>Effects the solver can actually play. Anything else in hand is ignored by the search.</summary>
    public enum Fx
    {
        None = 0,
        /// <summary>Spell: 2 damage to all enemies, again if strictly the highest Cost card in hand.</summary>
        Blasphemy,
        /// <summary>Spell: 6 damage split among all enemies; only deterministic with no enemy minions.</summary>
        Tachyon,
        /// <summary>Spell: +1/+1 to all your minions.</summary>
        Entropic,
        /// <summary>Spell: +X/+X to a chosen minion, X = 2 * this card's Cost.</summary>
        Floating,
        /// <summary>Minion: Battlecry gives adjacent minions +2/+2 (we choose the board slot).</summary>
        Vaultkeeper,
        /// <summary>Minion with Rush whose Battlecry may cast a Shred of Time (3 damage to our hero).</summary>
        RushShred,
        /// <summary>Generic minion whose whole text is keywords including Charge: attacks the turn it is played.</summary>
        ChargeBody,
        /// <summary>Generic minion whose whole text is keywords including Rush: attacks minions only the turn it is played.</summary>
        RushBody
    }

    public sealed class CardRule
    {
        public Fx Effect { get; }
        /// <summary>True when the text reading is not yet confirmed in a live game; a line using it is capped at Likely.</summary>
        public bool Unverified { get; }
        public string Note { get; }
        public CardRule(Fx effect, bool unverified, string note) { Effect = effect; Unverified = unverified; Note = note; }
    }

    /// <summary>
    /// Registry keyed by CardId. Every entry is derived from the card text in docs/deck-cards.md
    /// (HearthDb 37.0.2). Ambiguities are modelled in the direction that is worse for us.
    /// </summary>
    public static class CardRules
    {
        private static readonly Dictionary<string, CardRule> Rules = new Dictionary<string, CardRule>
        {
            { "BE_086", new CardRule(Fx.Blasphemy, false, "Blasphemy: tie on highest Cost treated as NOT doubled (conservative)") },
            { "TIME_027", new CardRule(Fx.Tachyon, true, "Tachyon Barrage: assumes the enemy hero is a valid target of 'all enemies'") },
            { "TIME_026", new CardRule(Fx.Entropic, false, "Entropic Continuity") },
            { "BE_094", new CardRule(Fx.Floating, true, "Floating Lessons: assumes +X/+X with X = 2 * its current Cost") },
            { "BE_151", new CardRule(Fx.Vaultkeeper, false, "Vaultkeeper") },
            { "TIME_029", new CardRule(Fx.RushShred, false, "Ruinous Velocidrake: Rush; battlecry copy ignored; needs hero health > 3") },
        };

        public static CardRule Get(string cardId) =>
            cardId != null && Rules.TryGetValue(cardId, out var r) ? r : null;
    }
}
