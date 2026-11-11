using System;
using System.Text.RegularExpressions;
using Copilot.Engine.Model;

namespace Copilot.Engine
{
    /// <summary>
    /// Classifies a card's rules text for the solver. Conservative: anything that is not empty, a list of
    /// modelled keywords, a modelled pattern, or provably inert for a minion already on the board is Unknown.
    /// </summary>
    public static class TextSupport
    {
        private const string Kw = "(?:taunt|divine shield|charge|rush|windfury|mega-windfury|poisonous|stealth|lifesteal|reborn|elusive)";

        private static readonly string[] Keywords =
        {
            "taunt", "divine shield", "charge", "rush", "windfury", "mega-windfury",
            "poisonous", "stealth", "lifesteal", "reborn", "elusive"
        };

        private static readonly Regex Markup = new Regex("<[^>]*>|\\[x\\]|\\$|#", RegexOptions.Compiled);
        private static readonly Regex Spaces = new Regex("[\\s\\u00A0&]+", RegexOptions.Compiled);

        // A Battlecry has already resolved for any minion on the board; it cannot fire again by attacking.
        // Reject when the text also creates persistent triggers or a Deathrattle.
        private static readonly Regex BattlecryOnly = new Regex(
            "^(?:" + Kw + "[,. ]+)*battlecry:(?!.*(?:deathrattle|whenever|at the start|at the end|after |when |each turn|has |have ))",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Static cost auras only matter when cards are played.
        private static readonly Regex SpellCostAura = new Regex(
            "^(?:all |your |your opponent's |both players' )?spells cost \\(\\d+\\) (?:more|less)\\.?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // "Deathrattle: Gain N Armor" (optionally "Battlecry and Deathrattle"): modelled as extra enemy armor on death.
        private static readonly Regex DeathrattleArmorRx = new Regex(
            "^(?:" + Kw + "[,. ]+)*(?:battlecry and )?deathrattle:\\s*gain (\\d+) armor\\.?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Own Deathrattle that only summons minions can add to our board but never reduces our lethal.
        private static readonly Regex OwnSummonDeathrattle = new Regex(
            "^(?:" + Kw + "[,. ]+)*deathrattle:\\s*summon [^.]*\\.?(?:\\s*\\(.*\\))?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static string Normalize(string text) =>
            Spaces.Replace(Markup.Replace(text, " "), " ").Trim();

        public static Support Classify(string text, bool ownSide = false)
        {
            if (string.IsNullOrWhiteSpace(text)) return Support.Supported;
            var flat = Normalize(text);

            if (DeathrattleArmorRx.IsMatch(flat)) return Support.KeywordOnly;
            if (BattlecryOnly.IsMatch(flat) || SpellCostAura.IsMatch(flat)) return Support.InertOnBoard;
            if (ownSide && OwnSummonDeathrattle.IsMatch(flat)) return Support.InertOnBoard;

            bool any = false;
            var cleaned = Markup.Replace(text, string.Empty).Replace("\r", string.Empty).Replace("\n", ",");
            foreach (var raw in cleaned.Split(new[] { ',', '.' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var token = raw.Trim().ToLowerInvariant();
                if (token.Length == 0) continue;
                if (Array.IndexOf(Keywords, token) < 0) return Support.Unknown;
                any = true;
            }
            return any ? Support.KeywordOnly : Support.Supported;
        }

        /// <summary>
        /// A hand minion whose entire text is keywords, including Charge or Rush. Such a card is an attacker the
        /// moment it is played; nothing else about it can change the outcome. Returns false for any other text.
        /// </summary>
        public static bool TryAttackBody(string text, out bool charge, out int swings, out bool shield, out bool poison)
        {
            charge = false; swings = 1; shield = false; poison = false;
            if (string.IsNullOrWhiteSpace(text)) return false;
            bool hasCharge = false, hasRush = false;
            var cleaned = Markup.Replace(text, string.Empty).Replace("\r", string.Empty).Replace("\n", ",");
            foreach (var raw in cleaned.Split(new[] { ',', '.' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var token = raw.Trim().ToLowerInvariant();
                if (token.Length == 0) continue;
                if (Array.IndexOf(Keywords, token) < 0) return false;
                switch (token)
                {
                    case "charge": hasCharge = true; break;
                    case "rush": hasRush = true; break;
                    case "windfury": swings = Math.Max(swings, 2); break;
                    case "mega-windfury": swings = Math.Max(swings, 4); break;
                    case "divine shield": shield = true; break;
                    case "poisonous": poison = true; break;
                }
            }
            if (!hasCharge && !hasRush) return false;
            charge = hasCharge;
            return true;
        }

        /// <summary>Armor gained by the minion's owner when it dies, or 0 when the text has no such Deathrattle.</summary>
        public static int DeathrattleArmor(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return 0;
            var m = DeathrattleArmorRx.Match(Normalize(text));
            return m.Success && int.TryParse(m.Groups[1].Value, out var n) ? n : 0;
        }
    }
}
