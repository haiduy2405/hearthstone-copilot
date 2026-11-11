using System;
using System.Text.RegularExpressions;
using Copilot.Engine.Model;

namespace Copilot.Engine
{
    /// <summary>
    /// Classifies a card's rules text. Conservative: anything that is not empty or a plain
    /// list of modelled keywords is Unknown.
    /// </summary>
    public static class TextSupport
    {
        private static readonly string[] Keywords =
        {
            "taunt", "divine shield", "charge", "rush", "windfury", "mega-windfury",
            "poisonous", "stealth", "lifesteal", "reborn", "elusive"
        };

        private static readonly Regex Markup = new Regex("<[^>]*>|\\[x\\]|\\$|#", RegexOptions.Compiled);

        public static Support Classify(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return Support.Supported;
            var cleaned = Markup.Replace(text, string.Empty).Replace("\n", ",").Replace("\r", string.Empty);
            bool any = false;
            foreach (var raw in cleaned.Split(new[] { ',', '.' }, StringSplitOptions.RemoveEmptyEntries))
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
