using System;
using System.IO;
using System.Linq;
using Taskmaster.Models;
using Taskmaster.Services;
using Xunit;

namespace Taskmaster.Tests.Services
{
    public class TaskStoreTests : IDisposable
    {
        private readonly string _dir;

        public TaskStoreTests()
        {
            _dir = Path.Combine(Path.GetTempPath(), "taskmaster-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_dir);
        }

        public void Dispose() => Directory.Delete(_dir, recursive: true);

        private string TasksPath => Path.Combine(_dir, "tasks.json");

        [Fact]
        public void Load_NoFile_StartsEmpty()
        {
            var store = new TaskStore(_dir);
            var result = store.Load();
            Assert.Empty(store.Tabs);
            Assert.Equal(TaskStoreLoadOutcome.StartedEmpty, result.Outcome);
        }

        [Fact]
        public void SaveThenLoad_RoundTrips()
        {
            var store = new TaskStore(_dir);
            store.Load();
            var tab = new TodoTab { Name = "Dailies" };
            var task = new TodoTask { Name = "PSNA", Schedule = ResetScheduleType.Psna, TargetCount = 3, Notes = "n" };
            task.Subtasks.Add(new TodoTask
            {
                Name = "child",
                ClipboardContent = "[&subtask]",
                Order = 4
            });
            tab.Tasks.Add(task);
            store.Tabs.Add(tab);
            store.Save();

            var store2 = new TaskStore(_dir);
            var result = store2.Load();
            Assert.Equal(TaskStoreLoadOutcome.LoadedPrimary, result.Outcome);
            Assert.Single(store2.Tabs);
            var loaded = store2.Tabs[0].Tasks[0];
            Assert.Equal("PSNA", loaded.Name);
            Assert.Equal(ResetScheduleType.Psna, loaded.Schedule);
            Assert.Equal(3, loaded.TargetCount);
            Assert.Single(loaded.Subtasks);
            Assert.Equal("[&subtask]", loaded.Subtasks[0].ClipboardContent);
            Assert.Equal(4, loaded.Subtasks[0].Order);
            Assert.Equal(task.Id, loaded.Id);
        }

        [Fact]
        public void Save_KeepsBackupOfPreviousFile()
        {
            var store = new TaskStore(_dir);
            store.Load();
            store.Tabs.Add(new TodoTab { Name = "v1" });
            store.Save();
            store.Tabs[0].Name = "v2";
            store.Save();
            Assert.True(File.Exists(TasksPath + ".bak"));
            Assert.Contains("v1", File.ReadAllText(TasksPath + ".bak"));
            Assert.Contains("v2", File.ReadAllText(TasksPath));
        }

        [Fact]
        public void Load_CorruptPrimary_QuarantinesAndUsesBackup()
        {
            var store = new TaskStore(_dir);
            store.Load();
            store.Tabs.Add(new TodoTab { Name = "good" });
            store.Save();
            store.Tabs[0].Name = "newer";
            store.Save(); // bak now holds "good"
            File.WriteAllText(TasksPath, "{ this is not json");

            var store2 = new TaskStore(_dir);
            var result = store2.Load();
            Assert.Equal(TaskStoreLoadOutcome.LoadedBackup, result.Outcome);
            Assert.Equal("good", store2.Tabs[0].Name);
            Assert.NotNull(result.QuarantinedPath);
            Assert.True(File.Exists(result.QuarantinedPath));
            Assert.Single(Directory.GetFiles(_dir, "tasks.corrupt-*.json"));
        }

        [Fact]
        public void Load_CorruptPrimaryAndBackup_StartsEmptyWithQuarantine()
        {
            File.WriteAllText(TasksPath, "not json");
            File.WriteAllText(TasksPath + ".bak", "also not json");
            var store = new TaskStore(_dir);
            var result = store.Load();
            Assert.Equal(TaskStoreLoadOutcome.StartedEmptyAfterCorruption, result.Outcome);
            Assert.Empty(store.Tabs);
        }

        [Fact]
        public void Load_NewerVersion_RefusesAndGoesReadOnly()
        {
            File.WriteAllText(TasksPath, "{\"Version\": 999, \"Tabs\": []}");
            var store = new TaskStore(_dir);
            var result = store.Load();
            Assert.Equal(TaskStoreLoadOutcome.VersionTooNew, result.Outcome);
            Assert.True(store.ReadOnly);
            store.Tabs.Add(new TodoTab { Name = "x" });
            store.Save(); // must NOT overwrite the newer file
            Assert.Contains("999", File.ReadAllText(TasksPath));
        }

        [Fact]
        public void FlushIfDue_DebouncesTwoSeconds()
        {
            var store = new TaskStore(_dir);
            store.Load();
            store.Tabs.Add(new TodoTab { Name = "x" });
            var t0 = new DateTime(2026, 7, 14, 12, 0, 0, DateTimeKind.Utc);
            store.MarkDirty(t0);
            store.FlushIfDue(t0.AddSeconds(1));
            Assert.False(File.Exists(TasksPath));
            store.FlushIfDue(t0.AddSeconds(2.5));
            Assert.True(File.Exists(TasksPath));
        }
    }
}
