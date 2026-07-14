using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Taskmaster.Models;

namespace Taskmaster.Services
{
    public enum TabShareImportOutcome { Success, NotATabExport, VersionTooNew }

    public class TabShareImportResult
    {
        public TabShareImportOutcome Outcome;
        public TodoTab Tab;
    }

    /// <summary>
    /// Clipboard-shareable tab payloads. Import regenerates every Id and zeroes all
    /// completion state - templates share structure, not progress.
    /// </summary>
    public static class TabShare
    {
        public const int PayloadVersion = 1;

        private class Payload
        {
            public int Taskmaster;
            public string Tab;
            public List<TodoTask> Tasks;
        }

        public static string Export(TodoTab tab)
        {
            return JsonConvert.SerializeObject(new Payload
            {
                Taskmaster = PayloadVersion,
                Tab = tab.Name,
                Tasks = tab.Tasks
            }, Formatting.Indented);
        }

        public static TabShareImportResult TryImport(string json)
        {
            var fail = new TabShareImportResult { Outcome = TabShareImportOutcome.NotATabExport };
            if (string.IsNullOrWhiteSpace(json)) return fail;

            Payload payload;
            try { payload = JsonConvert.DeserializeObject<Payload>(json); }
            catch { return fail; }

            if (payload == null || payload.Taskmaster == 0 || payload.Tasks == null) return fail;
            if (payload.Taskmaster > PayloadVersion)
                return new TabShareImportResult { Outcome = TabShareImportOutcome.VersionTooNew };

            var tab = new TodoTab { Id = Guid.NewGuid(), Name = payload.Tab ?? "Imported" };
            tab.Tasks.AddRange(payload.Tasks);
            foreach (var t in tab.Tasks) Sanitize(t);
            return new TabShareImportResult { Outcome = TabShareImportOutcome.Success, Tab = tab };
        }

        private static void Sanitize(TodoTask task)
        {
            task.Id = Guid.NewGuid();
            task.CurrentCount = 0;
            task.LastCompletedUtc = null;
            task.LastActivityUtc = null;
            // Duplicate (window.cs) round-trips a single task through Export+TryImport
            // as a convenient deep-clone, and a real tab-share import lands here too -
            // both zero progress above, which for a Duration schedule also zeroes its
            // only anchor, leaving the countdown blank until separately checked once.
            task.EnsureDurationAnchor(DateTime.UtcNow);
            if (task.Subtasks == null) task.Subtasks = new List<TodoTask>();
            foreach (var s in task.Subtasks) Sanitize(s);
        }
    }
}
