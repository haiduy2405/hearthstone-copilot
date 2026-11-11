using System;
using System.Collections.Generic;
using System.Text;

namespace Copilot.Engine.Model
{
    /// <summary>How well the engine understands a card's effect.</summary>
    public enum Support
    {
        /// <summary>Vanilla or fully modelled.</summary>
        Supported = 0,
        /// <summary>Only keywords the engine models (Taunt, Divine Shield, ...).</summary>
        KeywordOnly = 1,
        /// <summary>Has effect text the engine does not model. Blocks "proven" claims.</summary>
        Unknown = 2,
        /// <summary>Effect (Battlecry, cost aura) cannot influence an attack-only line for a minion already in play.</summary>
        InertOnBoard = 3
    }

    public sealed class MinionState
    {
        public int Id { get; set; }
        public string CardId { get; set; }
        public string Name { get; set; }
        public int Attack { get; set; }
        public int Health { get; set; }
        public bool Taunt { get; set; }
        public bool DivineShield { get; set; }
        public bool Poisonous { get; set; }
        public bool Stealth { get; set; }
        public bool Immune { get; set; }
        /// <summary>Attacks still available this turn (0 if frozen/exhausted/summoning sick; 2 for unused Windfury).</summary>
        public int AttacksRemaining { get; set; }
        /// <summary>False for Rush minions that cannot hit the hero yet.</summary>
        public bool CanAttackHero { get; set; } = true;
        public bool Frozen { get; set; }
        /// <summary>Attacks per turn the minion could make (0 = cannot attack, 1, 2 Windfury, 4 Mega-Windfury). Defaults to 1.</summary>
        public int MaxAttacks { get; set; } = 1;
        /// <summary>Armor the owner gains when this minion dies (parsed from "Deathrattle: Gain N Armor").</summary>
        public int DeathrattleArmor { get; set; }
        public Support Support { get; set; }
        /// <summary>Base card text (markup included), kept so reviews can see why a card was Unknown.</summary>
        public string Text { get; set; }

        public override string ToString() => (Name ?? CardId ?? "?") + "#" + Id;
    }

    public enum CardKind { Other = 0, Minion = 1, Spell = 2 }

    public sealed class HandCard
    {
        public int Id { get; set; }
        public string CardId { get; set; }
        public string Name { get; set; }
        /// <summary>Current Cost as HDT reports it (auras included).</summary>
        public int Cost { get; set; }
        public CardKind Kind { get; set; }
        public int Attack { get; set; }
        public int Health { get; set; }
        public string Text { get; set; }
    }

    public sealed class SideState
    {
        public int HeroEntityId { get; set; }
        public int HeroHealth { get; set; }
        public int Armor { get; set; }
        public bool HeroImmune { get; set; }
        /// <summary>Hero attack value (weapon included). Only meaningful for the side to move.</summary>
        public int HeroAttack { get; set; }
        public int HeroAttacksRemaining { get; set; }
        public int Mana { get; set; }
        public int HandCount { get; set; }
        /// <summary>Number of secrets in play. Their identity is unknown to the engine.</summary>
        public int Secrets { get; set; }
        public List<MinionState> Board { get; set; } = new List<MinionState>();
        /// <summary>Own hand only (the opponent's hand is hidden information and is never read).</summary>
        public List<HandCard> Hand { get; set; } = new List<HandCard>();
    }

    /// <summary>Raw entity record kept in dumps so adapter assumptions can be audited offline.</summary>
    public sealed class RawEntity
    {
        public int Id { get; set; }
        public string CardId { get; set; }
        public string Name { get; set; }
        public Dictionary<string, int> Tags { get; set; } = new Dictionary<string, int>();
    }

    public sealed class GameSnapshot
    {
        public string GameId { get; set; }
        public int Turn { get; set; }
        public bool MyTurn { get; set; }
        public string Format { get; set; }
        public string HdtVersion { get; set; }
        /// <summary>True while the player is choosing which starting cards to replace.</summary>
        public bool InMulligan { get; set; }
        /// <summary>True when the player moves first (3 starting cards, no Coin).</summary>
        public bool OnThePlay { get; set; }
        public DateTime Timestamp { get; set; }
        public SideState Me { get; set; } = new SideState();
        public SideState Opp { get; set; } = new SideState();
        /// <summary>Optional audit data (hero + in-play entities) written by the plugin.</summary>
        public List<RawEntity> Raw { get; set; } = new List<RawEntity>();

        /// <summary>Cheap change-detection key covering everything the solver reads.</summary>
        public string Signature()
        {
            var sb = new StringBuilder(256);
            sb.Append(Turn).Append(MyTurn ? 'M' : 'O').Append(InMulligan ? 'G' : '-').Append(OnThePlay ? 'P' : '-');
            AppendSide(sb, Me);
            AppendSide(sb, Opp);
            return sb.ToString();
        }

        private static void AppendSide(StringBuilder sb, SideState s)
        {
            sb.Append('|').Append(s.HeroHealth).Append(',').Append(s.Armor).Append(',').Append(s.HeroAttack)
              .Append(',').Append(s.HeroAttacksRemaining).Append(',').Append(s.Mana).Append(',').Append(s.Secrets);
            foreach (var h in s.Hand) sb.Append('h').Append(h.Id).Append(':').Append(h.Cost);
            foreach (var m in s.Board)
                sb.Append(';').Append(m.Id).Append(',').Append(m.Attack).Append(',').Append(m.Health)
                  .Append(m.Taunt ? 'T' : '-').Append(m.DivineShield ? 'D' : '-').Append(m.Poisonous ? 'P' : '-')
                  .Append(m.Stealth ? 'S' : '-').Append(m.Immune ? 'I' : '-').Append(m.AttacksRemaining)
                  .Append(m.CanAttackHero ? 'H' : '-');
        }
    }
}
