using System;
using System.Linq;
using System.Text;
using HearthDb;
using HearthDb.Deckstrings;
using HearthDb.Enums;

// Usage:
//   DeckDump <deckcode> [outfile.md]      card table for a deck code (real text from HearthDb)
//   DeckDump --find "<name>" [...]        look up cards by exact English name (tokens included)
internal static class Program
{
    private static string Row(string count, Card c)
    {
        var text = (c.GetLocText(Locale.enUS) ?? "").Replace("\n", " ").Replace("|", "/");
        var mech = string.Join(",", c.Mechanics ?? new string[0]);
        return $"| {count} | {c.Id} | {c.Cost} | {c.GetLocName(Locale.enUS)} | {c.Type} | {c.Attack}/{c.Health} | {mech} | {text} |";
    }

    private static int Main(string[] args)
    {
        if (args.Length < 1) { Console.Error.WriteLine("usage: DeckDump <deckcode> [out.md] | --find <name>..."); return 1; }
        var sb = new StringBuilder();
        sb.AppendLine("| # | CardId | Cost | Name | Type | A/H | Mechanics | Text |");
        sb.AppendLine("|---|--------|------|------|------|-----|-----------|------|");

        if (args[0] == "--find")
        {
            foreach (var name in args.Skip(1))
            {
                var hits = Cards.All.Values.Where(c => string.Equals(c.GetLocName(Locale.enUS), name, StringComparison.OrdinalIgnoreCase)).ToList();
                if (hits.Count == 0) sb.AppendLine($"| - | - | - | {name} | NOT FOUND | | | |");
                foreach (var c in hits) sb.AppendLine(Row("-", c));
            }
            Console.WriteLine(sb.ToString());
            return 0;
        }

        var deck = DeckSerializer.Deserialize(args[0]);
        sb.Insert(0, "# Deck dump\nFormat: " + deck.Format + " | Hero: " + (Cards.GetFromDbfId(deck.HeroDbfId, false)?.Name ?? deck.HeroDbfId.ToString()) + "\n\n");
        foreach (var kv in deck.GetCards().OrderBy(k => k.Key.Cost).ThenBy(k => k.Key.Name))
            sb.AppendLine(Row(kv.Value.ToString(), kv.Key));
        Console.WriteLine(sb.ToString());
        if (args.Length > 1) System.IO.File.WriteAllText(args[1], sb.ToString());
        return 0;
    }
}
