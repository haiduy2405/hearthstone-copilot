using Copilot.Engine;
using Copilot.Engine.Model;
using Xunit;

namespace Copilot.Tests
{
    public class TextSupportTests
    {
        [Theory]
        [InlineData(null, Support.Supported)]
        [InlineData("", Support.Supported)]
        [InlineData("<b>Taunt</b>", Support.KeywordOnly)]
        [InlineData("<b>Taunt</b>, <b>Divine Shield</b>", Support.KeywordOnly)]
        [InlineData("<b>Rush</b>\n<b>Poisonous</b>", Support.KeywordOnly)]
        [InlineData("<b>Battlecry:</b> Deal 3 damage.", Support.InertOnBoard)]
        [InlineData("<b>Battlecry:</b> <b>Freeze</b> an enemy.", Support.InertOnBoard)]
        [InlineData("<b>Battlecry:</b> Plot a custom sham trial! Then choose the trial's length.", Support.InertOnBoard)]
        [InlineData("<b>Taunt</b>\n<b>Battlecry:</b> Gain 2 Armor.", Support.InertOnBoard)]
        [InlineData("ALL spells cost (1) more.", Support.InertOnBoard)]
        [InlineData("<b>Battlecry and Deathrattle:</b> Give adjacent minions +2/+2.", Support.Unknown)]
        [InlineData("<b>Battlecry:</b> Draw a card. Whenever you cast a spell, deal 1 damage.", Support.Unknown)]
        [InlineData("<b>Deathrattle:</b> Summon a 1/1 Whelp.", Support.Unknown)]
        [InlineData("Your other minions have +1 Attack.", Support.Unknown)]
        [InlineData("<b>Taunt</b>. <b>Deathrattle:</b> Draw a card.", Support.Unknown)]
        public void Classify(string text, Support expected) => Assert.Equal(expected, TextSupport.Classify(text));
    }
}
