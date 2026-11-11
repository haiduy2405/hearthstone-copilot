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
        [InlineData("<b>Battlecry:</b> Deal 3 damage.", Support.Unknown)]
        [InlineData("<b>Taunt</b>. <b>Deathrattle:</b> Draw a card.", Support.Unknown)]
        public void Classify(string text, Support expected) => Assert.Equal(expected, TextSupport.Classify(text));
    }
}
