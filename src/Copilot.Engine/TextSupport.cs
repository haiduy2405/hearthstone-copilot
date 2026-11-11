using System;
using System.Text.RegularExpressions;
using Copilot.Engine.Model;

namespace Copilot.Engine
{
    /// <summary>
    /// Classifies a card's rules text for the attack-only solver. Conservative: anything that is not
    /// empty, a list of modelled keywords, or provably inert for a minion already on the board is Unknown.
    /// </summary>
    public static class TextSupport
    {
        private static readonly string[] Keywords =
        {
            "taunt", "divine shield", "charge", "rush", "windfury", "mega-windfury",
            "poisonous", "stealth", "lifesteal", "reborn", "elusive"
        };

        private static readonly Regex Markup = new Regex("<[^>]*>|\\[x\\]|\\$|#", RegexOptions.Compiled);

        // A Battlecry has already resolved for any minion on the board; it cannot fire again by attacking.
        // Reject when the text also creates persistent triggers or a Deathrattle.
        private static readonly Regex BattlecryOnly = new Regex(
            "^(?:(?:taunt|divine shield|charge|rush|windfury|poisonous|stealth|lifesteal|reborn|elusive)[,. ]+)*battlecry:(?!.*(?:deathrattle|whenever|at the start|at the end|after |when |each turn|has |have ))",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        // Static cost auras only matter when cards are played, which the attack-only solver never does.
        private static readonly Regex SpellCostAura = new Regex(
            "^(?:all |your |your opponent's |both players' )?spells cost \\(\\d+\\) (?:more|less)\\.?$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public static Support Classify(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Support.Supported;
            var cleaned = Markup.Replace(text, string.Empty).Replace("\r", string.Empty);
            var flat = cleaned.Replace("\n", " ").Trim();

            if (BattlecryOnly.IsMatch(flat) || SpellCostAura.IsMatch(flat)) return Support.InertOnBoard;

            bool any = false;
            foreach (var raw in cleaned.Replace("\n", ",").Split(new[] { ',', '.' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var token = raw.Trim().ToLowerInvariant();
                if (token.Length == 0) continue;
                if (Array.IndexOf(Keywords, token) < 0) return Support.Unknown;
                any = true;
            }
            return any ? Support.KeywordOnly : Support.Supported;
        }
    }
}
