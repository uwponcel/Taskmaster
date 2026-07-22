using System;
using System.Linq;
using Taskmaster.Models;
using Taskmaster.Services;
using Xunit;

namespace Taskmaster.Tests.Services
{
    public class PsnaRotationTests
    {
        private static DateTime Utc(int year, int month, int day, int hour, int minute) =>
            new DateTime(year, month, day, hour, minute, 0, DateTimeKind.Utc);

        [Fact]
        public void GetCycleIndex_UsesEightUtcBoundaryAndWraps()
        {
            Assert.Equal(6, PsnaRotation.GetCycleIndex(PsnaRotation.AnchorUtc.AddTicks(-1)));
            Assert.Equal(0, PsnaRotation.GetCycleIndex(PsnaRotation.AnchorUtc));
            Assert.Equal(0, PsnaRotation.GetCycleIndex(PsnaRotation.AnchorUtc.AddDays(7)));
            Assert.Equal(6, PsnaRotation.GetCycleIndex(PsnaRotation.AnchorUtc.AddDays(-1)));
        }

        [Fact]
        public void GetCycleIndex_KnownWikiDateChangesAtEightUtc()
        {
            Assert.Equal(5, PsnaRotation.GetCycleIndex(Utc(2026, 7, 22, 7, 59)));
            Assert.Equal(6, PsnaRotation.GetCycleIndex(Utc(2026, 7, 22, 8, 0)));
        }

        [Fact]
        public void GetLocation_KnownWikiDateMatchesAllRegions()
        {
            var nowUtc = Utc(2026, 7, 22, 8, 0);
            var names = PsnaRotation.Slots
                .Select(slot => PsnaRotation.GetLocation(slot, nowUtc).Location)
                .ToArray();

            Assert.Equal(new[]
            {
                "Town of Prosperity",
                "Swampwatch Post",
                "Caer Shadowfain",
                "Shieldbluff Waypoint",
                "Mennerheim",
                "Ferrusatos Village"
            }, names);
        }

        [Fact]
        public void RotationMapIds_MatchOfficialWikiMatrix()
        {
            var expectedBySlot = new[]
            {
                new[] { 1963, 1940, 1919, 1929, 1927, 1919, 1918 },
                new[] { 79, 707, 697, 314, 844, 72, 450 },
                new[] { 1021, 667, 785, 750, 1234, 680, 765 },
                new[] { 131, 1749, 402, 45, 422, 25, 166 },
                new[] { 1629, 379, 578, 645, 643, 612, 824 },
                new[] { 487, 211, 272, 545, 527, 387, 497 }
            };

            for (int slotIndex = 0; slotIndex < PsnaRotation.Slots.Count; slotIndex++)
                for (int day = 0; day < 7; day++)
                    Assert.Equal(
                        expectedBySlot[slotIndex][day],
                        PsnaRotation.GetLocation(
                            PsnaRotation.Slots[slotIndex],
                            PsnaRotation.AnchorUtc.AddDays(day)).MapId);
        }

        [Fact]
        public void EveryRotationEntryHasAValidMapLink()
        {
            for (int day = 0; day < 7; day++)
            {
                var nowUtc = PsnaRotation.AnchorUtc.AddDays(day);
                foreach (var slot in PsnaRotation.Slots)
                {
                    var location = PsnaRotation.GetLocation(slot, nowUtc);
                    Assert.True(location.MapId > 0, $"{slot}, cycle {day}");
                    Assert.StartsWith("[&", location.MapLink);
                    Assert.EndsWith("]", location.MapLink);
                }
            }
        }

        [Fact]
        public void EncodeMapLink_UsesGw2MapChatLinkFormat()
        {
            Assert.Equal("[&BH4HAAA=]", PsnaRotation.EncodeMapLink(1918));
        }
    }
}
