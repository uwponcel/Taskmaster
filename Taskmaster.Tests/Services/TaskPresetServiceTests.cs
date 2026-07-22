using System;
using System.Linq;
using Newtonsoft.Json;
using Taskmaster.Models;
using Taskmaster.Services;
using Xunit;

namespace Taskmaster.Tests.Services
{
    public class TaskPresetServiceTests
    {
        private static readonly DateTime KnownRotation =
            new DateTime(2026, 7, 22, 8, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void CreatePsna_CreatesManagedGroupWithSixStableSlots()
        {
            var preset = TaskPresetService.CreatePsna(4);

            Assert.True(preset.IsManagedPresetParent);
            Assert.Equal(4, preset.Order);
            Assert.Equal(ResetScheduleType.Psna, preset.Schedule);
            Assert.Equal(6, preset.Subtasks.Count);
            Assert.Equal(PsnaRotation.Slots, preset.Subtasks.Select(child => child.PresetSlot));
            Assert.All(preset.Subtasks, child =>
            {
                Assert.True(child.IsManagedPresetChild);
                Assert.Equal(ResetScheduleType.Psna, child.Schedule);
            });
        }

        [Fact]
        public void ResolveContent_ReturnsIndividualAndCopyAllLinks()
        {
            var preset = TaskPresetService.CreatePsna(0);
            var links = preset.Subtasks
                .Select(child =>
                    TaskPresetService.ResolveClipboardContent(child, KnownRotation))
                .ToArray();
            var copyAll = TaskPresetService.ResolveClipboardContent(preset, KnownRotation);

            Assert.Equal(6, links.Distinct().Count());
            Assert.Equal(string.Join(" ", links), copyAll);
            Assert.Equal(
                "Maguuma Wastes: Town of Prosperity",
                TaskPresetService.ResolveName(preset.Subtasks[0], KnownRotation));
        }

        [Fact]
        public void ResolvingAnotherDayDoesNotMutatePersistedDataOrProgress()
        {
            var preset = TaskPresetService.CreatePsna(0);
            preset.Subtasks[0].Increment(KnownRotation);
            string before = JsonConvert.SerializeObject(preset);

            TaskPresetService.ResolveName(preset.Subtasks[0], KnownRotation.AddDays(1));
            TaskPresetService.ResolveClipboardContent(preset, KnownRotation.AddDays(1));

            Assert.Equal(before, JsonConvert.SerializeObject(preset));
            Assert.True(preset.Subtasks[0].IsDone);
        }

        [Fact]
        public void CanMoveTo_RejectsDestinationContainingSamePreset()
        {
            var preset = TaskPresetService.CreatePsna(0);
            var normal = new TodoTask { Name = "Normal" };
            var occupied = new TodoTab { Name = "Occupied" };
            occupied.Tasks.Add(TaskPresetService.CreatePsna(0));
            var empty = new TodoTab { Name = "Empty" };

            Assert.False(TaskPresetService.CanMoveTo(new[] { normal, preset }, occupied));
            Assert.True(TaskPresetService.CanMoveTo(new[] { normal, preset }, empty));
            Assert.True(TaskPresetService.CanMoveTo(new[] { normal }, occupied));
        }

        [Fact]
        public void ValidateTab_RejectsDuplicateOrMalformedPresets()
        {
            var valid = new TodoTab { Name = "Valid" };
            valid.Tasks.Add(TaskPresetService.CreatePsna(0));
            Assert.True(TaskPresetService.ValidateTab(valid));

            valid.Tasks.Add(TaskPresetService.CreatePsna(1));
            Assert.False(TaskPresetService.ValidateTab(valid));

            var malformed = new TodoTab { Name = "Malformed" };
            var preset = TaskPresetService.CreatePsna(0);
            preset.Subtasks.RemoveAt(0);
            malformed.Tasks.Add(preset);
            Assert.False(TaskPresetService.ValidateTab(malformed));
        }

        [Fact]
        public void ValidateTab_RejectsBehaviorChangesToManagedChildren()
        {
            var wrongSchedule = new TodoTab { Name = "Wrong schedule" };
            var schedulePreset = TaskPresetService.CreatePsna(0);
            schedulePreset.Subtasks[0].Schedule = ResetScheduleType.Never;
            wrongSchedule.Tasks.Add(schedulePreset);
            Assert.False(TaskPresetService.ValidateTab(wrongSchedule));

            var nested = new TodoTab { Name = "Nested" };
            var nestedPreset = TaskPresetService.CreatePsna(0);
            nestedPreset.Subtasks[0].Subtasks.Add(new TodoTask { Name = "Unexpected" });
            nested.Tasks.Add(nestedPreset);
            Assert.False(TaskPresetService.ValidateTab(nested));
        }
    }
}
