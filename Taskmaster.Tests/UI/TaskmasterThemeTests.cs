using Microsoft.Xna.Framework;
using Taskmaster.UI;
using Xunit;

namespace Taskmaster.Tests.UI
{
    public class TaskmasterThemeTests
    {
        [Fact]
        public void ToHex_RoundTripsThroughParseAccentHex()
        {
            var color = new Color(212, 166, 86);
            var hex = TaskmasterTheme.ToHex(color);
            var parsed = TaskmasterTheme.ParseAccentHex(hex);
            Assert.Equal("D4A656", hex);
            Assert.Equal(color, parsed);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("D4A65")]
        [InlineData("D4A6567")]
        [InlineData("ZZZZZZ")]
        public void ParseAccentHex_InvalidInput_ReturnsNull(string hex)
        {
            Assert.Null(TaskmasterTheme.ParseAccentHex(hex));
        }

        [Fact]
        public void TabAccentPresets_AllRoundTripCleanly()
        {
            foreach (var preset in TaskmasterTheme.TabAccentPresets)
            {
                var hex = TaskmasterTheme.ToHex(preset.Value);
                Assert.Equal(preset.Value, TaskmasterTheme.ParseAccentHex(hex));
            }
        }
    }
}
