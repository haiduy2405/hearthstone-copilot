using Copilot.Engine.Model;

namespace Copilot.Engine
{
    public sealed class ThreatInfo
    {
        /// <summary>Upper bound of damage the VISIBLE enemy board and weapon can deal to our hero next turn.</summary>
        public int VisibleDamage { get; set; }
        /// <summary>Our hero's health plus armor.</summary>
        public int HeroEffectiveHealth { get; set; }
        public bool Danger { get; set; }
        public bool Watch { get; set; }
        public bool HasOurTaunt { get; set; }
    }

    /// <summary>
    /// Visible-board threat check. It ignores everything hidden (hand, secrets, charge minions, burn), so it can
    /// only under-estimate danger; it never invents damage. Our Taunt minions are noted, not simulated.
    /// </summary>
    public static class ThreatAnalyzer
    {
        public static ThreatInfo Analyze(GameSnapshot s)
        {
            var t = new ThreatInfo { HeroEffectiveHealth = s.Me.HeroHealth + s.Me.Armor };
            int dmg = 0;
            foreach (var m in s.Opp.Board)
            {
                if (m.Attack <= 0 || m.Frozen || m.MaxAttacks <= 0) continue;
                dmg += m.Attack * m.MaxAttacks;
            }
            if (s.Opp.HeroAttack > 0) dmg += s.Opp.HeroAttack;
            foreach (var m in s.Me.Board)
                if (m.Taunt && !m.Stealth) { t.HasOurTaunt = true; break; }

            t.VisibleDamage = dmg;
            t.Danger = t.HeroEffectiveHealth > 0 && dmg >= t.HeroEffectiveHealth;
            t.Watch = !t.Danger && t.HeroEffectiveHealth > 0 && dmg * 10 >= t.HeroEffectiveHealth * 6;
            return t;
        }
    }
}
