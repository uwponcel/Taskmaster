using System;
using System.Collections.Generic;
using Taskmaster.Models;

namespace Taskmaster.Services
{
    public sealed class PsnaLocation
    {
        public TaskPresetSlot Slot { get; }
        public string Region { get; }
        public string Npc { get; }
        public string Location { get; }
        public int MapId { get; }
        public string MapLink => PsnaRotation.EncodeMapLink(MapId);

        public PsnaLocation(
            TaskPresetSlot slot,
            string region,
            string npc,
            string location,
            int mapId)
        {
            Slot = slot;
            Region = region;
            Npc = npc;
            Location = location;
            MapId = mapId;
        }
    }

    /// <summary>
    /// Offline PSNA rotation from the Guild Wars 2 Wiki template
    /// "Pact Supply Network Agent row", revision 2544046.
    /// </summary>
    public static class PsnaRotation
    {
        public static readonly DateTime AnchorUtc =
            new DateTime(2015, 10, 29, 0, 0, 0, DateTimeKind.Utc) +
            ResetEngine.PsnaResetAtUtc;

        public static readonly IReadOnlyList<TaskPresetSlot> Slots =
            new[]
            {
                TaskPresetSlot.PsnaMaguumaWastes,
                TaskPresetSlot.PsnaMaguumaJungle,
                TaskPresetSlot.PsnaRuinsOfOrr,
                TaskPresetSlot.PsnaKryta,
                TaskPresetSlot.PsnaShiverpeaks,
                TaskPresetSlot.PsnaAscalon
            };

        private static readonly Dictionary<TaskPresetSlot, string> Regions =
            new Dictionary<TaskPresetSlot, string>
            {
                { TaskPresetSlot.PsnaMaguumaWastes, "Maguuma Wastes" },
                { TaskPresetSlot.PsnaMaguumaJungle, "Maguuma Jungle" },
                { TaskPresetSlot.PsnaRuinsOfOrr, "Ruins of Orr" },
                { TaskPresetSlot.PsnaKryta, "Kryta" },
                { TaskPresetSlot.PsnaShiverpeaks, "Shiverpeaks" },
                { TaskPresetSlot.PsnaAscalon, "Ascalon" }
            };

        private static readonly Dictionary<TaskPresetSlot, string> Npcs =
            new Dictionary<TaskPresetSlot, string>
            {
                { TaskPresetSlot.PsnaMaguumaWastes, "Mehem the Traveled" },
                { TaskPresetSlot.PsnaMaguumaJungle, "The Fox" },
                { TaskPresetSlot.PsnaRuinsOfOrr, "Specialist Yana" },
                { TaskPresetSlot.PsnaKryta, "Lady Derwena" },
                { TaskPresetSlot.PsnaShiverpeaks, "Despina Katelyn" },
                { TaskPresetSlot.PsnaAscalon, "Verma Giftrender" }
            };

        private static readonly Dictionary<TaskPresetSlot, string[]> LocationNames =
            new Dictionary<TaskPresetSlot, string[]>
            {
                {
                    TaskPresetSlot.PsnaMaguumaWastes,
                    new[]
                    {
                        "Blue Oasis", "Repair Station", "Camp Resolve Waypoint",
                        "Azarr's Arbor", "Restoration Refuge", "Camp Resolve Waypoint",
                        "Town of Prosperity"
                    }
                },
                {
                    TaskPresetSlot.PsnaMaguumaJungle,
                    new[]
                    {
                        "Seraph Protectors", "Breth Ayahusasca", "Gallant's Folly",
                        "Mabon Waypoint", "Lionguard Waystation Waypoint",
                        "Desider Atum Waypoint", "Swampwatch Post"
                    }
                },
                {
                    TaskPresetSlot.PsnaRuinsOfOrr,
                    new[]
                    {
                        "Armada Harbor", "Shelter Docks", "Augur's Torch",
                        "Fort Trinity Waypoint", "Rally Waypoint",
                        "Waste Hollows Waypoint", "Caer Shadowfain"
                    }
                },
                {
                    TaskPresetSlot.PsnaKryta,
                    new[]
                    {
                        "Altar Brook Trading Post", "Pearl Islet Waypoint",
                        "Vigil Keep Waypoint", "Mudflat Camp",
                        "Marshwatch Haven Waypoint", "Garenhoff",
                        "Shieldbluff Waypoint"
                    }
                },
                {
                    TaskPresetSlot.PsnaShiverpeaks,
                    new[]
                    {
                        "Rocklair", "Dolyak Pass Waypoint", "Balddistead",
                        "Blue Ice Shining Waypoint", "Ridgerock Camp Waypoint",
                        "Travelen's Waypoint", "Mennerheim"
                    }
                },
                {
                    TaskPresetSlot.PsnaAscalon,
                    new[]
                    {
                        "Village of Scalecatch Waypoint", "Hawkgates Waypoint",
                        "Bovarin Estate", "Snow Ridge Camp Waypoint", "Haymal Gore",
                        "Temperus Point Waypoint", "Ferrusatos Village"
                    }
                }
            };

        private static readonly Dictionary<TaskPresetSlot, int[]> MapIds =
            new Dictionary<TaskPresetSlot, int[]>
            {
                {
                    TaskPresetSlot.PsnaMaguumaWastes,
                    new[] { 1963, 1940, 1919, 1929, 1927, 1919, 1918 }
                },
                {
                    TaskPresetSlot.PsnaMaguumaJungle,
                    new[] { 79, 707, 697, 314, 844, 72, 450 }
                },
                {
                    TaskPresetSlot.PsnaRuinsOfOrr,
                    new[] { 1021, 667, 785, 750, 1234, 680, 765 }
                },
                {
                    TaskPresetSlot.PsnaKryta,
                    new[] { 131, 1749, 402, 45, 422, 25, 166 }
                },
                {
                    TaskPresetSlot.PsnaShiverpeaks,
                    new[] { 1629, 379, 578, 645, 643, 612, 824 }
                },
                {
                    TaskPresetSlot.PsnaAscalon,
                    new[] { 487, 211, 272, 545, 527, 387, 497 }
                }
            };

        public static int GetCycleIndex(DateTime nowUtc)
        {
            long ticks = (nowUtc - AnchorUtc).Ticks;
            long days = ticks / TimeSpan.TicksPerDay;
            if (ticks < 0 && ticks % TimeSpan.TicksPerDay != 0) days--;
            return (int)((days % 7 + 7) % 7);
        }

        public static PsnaLocation GetLocation(TaskPresetSlot slot, DateTime nowUtc)
        {
            if (!LocationNames.TryGetValue(slot, out var names) ||
                !MapIds.TryGetValue(slot, out var ids))
                return null;

            int index = GetCycleIndex(nowUtc);
            return new PsnaLocation(
                slot,
                Regions[slot],
                Npcs[slot],
                names[index],
                ids[index]);
        }

        public static string EncodeMapLink(int mapId)
        {
            if (mapId <= 0) return null;
            var bytes = new[]
            {
                (byte)4,
                (byte)(mapId & 0xFF),
                (byte)((mapId >> 8) & 0xFF),
                (byte)((mapId >> 16) & 0xFF),
                (byte)((mapId >> 24) & 0xFF)
            };
            return "[&" + Convert.ToBase64String(bytes) + "]";
        }
    }
}
