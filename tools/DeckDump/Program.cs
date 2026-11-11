using System;
using System.Linq;
using System.Text;
using HearthDb;
using HearthDb.Deckstrings;
using HearthDb.Enums;

// Usage: DeckDump <deckcode> [outfile.md]
// Prints the real card data (from HearthDb bundled with HDT) for each card in a deck code,
// so deck-specific engine work is based on actual card text instead of memory.
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 1) { Console.Error.WriteLine("usage: DeckDump <deckcode> [outfile.md]"); return 1; }
        var deck = DeckSerializer.Deserialize(args[0]);
        var sb = new StringBuilder();
        sb.AppendLine("# Deck dump");
        sb.AppendLine("Format: " + deck.Format + " | Hero: " + (Cards.GetFromDbfId(deck.HeroDbfId, false)?.Name ?? deck.HeroDbfId.ToString()));
        sb.AppendLine();
        sb.AppendLine("| # | Cost | Name | Type | A/H | Mechanics | Text |");
        sb.AppendLine("|---|------|------|------|-----|-----------|------|");
        foreach (var kv in deck.GetCards().OrderBy(k => k.Key.Cost).ThenBy(k => k.Key.Name))
        {
            var c = kv.Key;
            var text = (c.GetLocText(Locale.enUS) ?? "").Replace("\n", " ").Replace("|", "/");
            var mech = string.Join(",", c.Mechanics ?? new string[0]);
            sb.AppendLine($"| {kv.Value} | {c.Cost} | {c.GetLocName(Locale.enUS)} | {c.Type} | {c.Attack}/{c.Health} | {mech} | {text} |");
        }
        Console.WriteLine(sb.ToString());
        if (args.Length > 1) System.IO.File.WriteAllText(args[1], sb.ToString());
        return 0;
    }
}
