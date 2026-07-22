using System;
using Taskmaster.Models;
using Taskmaster.Services;
using Xunit;

namespace Taskmaster.Tests.Services
{
    public class TabShareTests
    {
        private static TodoTab SampleTab()
        {
            var tab = new TodoTab { Name = "Dailies" };
            var t = new TodoTask { Name = "PSNA", Schedule = ResetScheduleType.Psna, TargetCount = 3, ClipboardContent = "[&code]" };
            t.Increment(DateTime.UtcNow);
            t.Subtasks.Add(new TodoTask
            {
                Name = "child",
                ClipboardContent = "[&child]",
                Order = 2
            });
            tab.Tasks.Add(t);
            return tab;
        }

        [Fact]
        public void ExportImport_RoundTripsStructure_StripsProgressAndIds()
        {
            var tab = SampleTab();
            var json = TabShare.Export(tab);
            var result = TabShare.TryImport(json);

            Assert.Equal(TabShareImportOutcome.Success, result.Outcome);
            Assert.Equal("Dailies", result.Tab.Name);
            var imported = result.Tab.Tasks[0];
            Assert.Equal("PSNA", imported.Name);
            Assert.Equal(3, imported.TargetCount);
            Assert.Equal("[&code]", imported.ClipboardContent);
            Assert.Single(imported.Subtasks);
            Assert.Equal("[&child]", imported.Subtasks[0].ClipboardContent);
            Assert.Equal(2, imported.Subtasks[0].Order);
            Assert.NotEqual(tab.Id, result.Tab.Id);
            Assert.NotEqual(tab.Tasks[0].Id, imported.Id);
            Assert.Equal(0, imported.CurrentCount);
            Assert.Null(imported.LastCompletedUtc);
            Assert.Null(imported.LastActivityUtc);
        }

        [Fact]
        public void ExportImport_DurationTask_GetsFreshAnchorInsteadOfBlankCountdown()
        {
            // Duplicate (TaskmasterWindow.ShowTaskMenu) round-trips a task through
            // Export+TryImport as a deep-clone, same as a real tab-share import - both
            // zero LastCompletedUtc/LastActivityUtc, which for a Duration schedule
            // would otherwise leave the copy's countdown blank until separately checked.
            var tab = new TodoTab { Name = "Dailies" };
            var t = new TodoTask { Name = "Cooldown thing", Schedule = ResetScheduleType.Duration, ResetDuration = TimeSpan.FromHours(1) };
            t.Increment(DateTime.UtcNow);
            tab.Tasks.Add(t);

            var json = TabShare.Export(tab);
            var imported = TabShare.TryImport(json).Tab.Tasks[0];

            Assert.Null(imported.LastCompletedUtc);
            Assert.NotNull(imported.LastActivityUtc);
        }

        [Fact]
        public void TryImport_NotTaskmasterJson_Fails()
        {
            Assert.Equal(TabShareImportOutcome.NotATabExport, TabShare.TryImport("{\"foo\": 1}").Outcome);
            Assert.Equal(TabShareImportOutcome.NotATabExport, TabShare.TryImport("hello world").Outcome);
            Assert.Equal(TabShareImportOutcome.NotATabExport, TabShare.TryImport("").Outcome);
            Assert.Equal(TabShareImportOutcome.NotATabExport, TabShare.TryImport(null).Outcome);
        }

        [Fact]
        public void TryImport_NewerPayloadVersion_Refuses()
        {
            var json = "{\"Taskmaster\": 999, \"Tab\": \"x\", \"Tasks\": []}";
            Assert.Equal(TabShareImportOutcome.VersionTooNew, TabShare.TryImport(json).Outcome);
        }
    }
}
