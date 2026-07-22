using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Blish_HUD;
using Newtonsoft.Json;
using Taskmaster.Models;

namespace Taskmaster.Services
{
    public enum TaskStoreLoadOutcome
    {
        LoadedPrimary,
        LoadedBackup,
        StartedEmpty,
        StartedEmptyAfterCorruption,
        VersionTooNew
    }

    public class TaskStoreLoadResult
    {
        public TaskStoreLoadOutcome Outcome;
        public string QuarantinedPath;
    }

    /// <summary>
    /// JSON persistence for the whole task list. Atomic writes (tmp -> replace, previous
    /// kept as .bak), corruption quarantine, and a 2-second debounce driven by the caller's
    /// clock (testable, no timers).
    /// </summary>
    public class TaskStore
    {
        private static readonly Logger Logger = Logger.GetLogger<TaskStore>();

        public const int CurrentVersion = 2;
        private static readonly TimeSpan DebounceDelay = TimeSpan.FromSeconds(2);

        private readonly string _filePath;
        private bool _dirty;
        private DateTime _dirtySinceUtc;

        public List<TodoTab> Tabs { get; private set; } = new List<TodoTab>();

        /// <summary>True when the on-disk file came from a newer module version; Save() is a no-op.</summary>
        public bool ReadOnly { get; private set; }

        public TaskStore(string directory)
        {
            _filePath = Path.Combine(directory, "tasks.json");
        }

        private class Envelope
        {
            public int Version = CurrentVersion;
            public List<TodoTab> Tabs = new List<TodoTab>();
        }

        public TaskStoreLoadResult Load()
        {
            var result = new TaskStoreLoadResult();

            if (!File.Exists(_filePath))
            {
                result.Outcome = TaskStoreLoadOutcome.StartedEmpty;
                return result;
            }

            var primary = TryParse(_filePath);
            if (primary != null)
            {
                if (primary.Version > CurrentVersion)
                {
                    Logger.Warn($"tasks.json is version {primary.Version}, newer than supported {CurrentVersion}; read-only mode");
                    ReadOnly = true;
                    result.Outcome = TaskStoreLoadOutcome.VersionTooNew;
                    return result;
                }
                Tabs = primary.Tabs ?? new List<TodoTab>();
                result.Outcome = TaskStoreLoadOutcome.LoadedPrimary;
                return result;
            }

            // Primary corrupt: quarantine it, try backup.
            result.QuarantinedPath = Path.Combine(
                Path.GetDirectoryName(_filePath),
                $"tasks.corrupt-{DateTime.UtcNow:yyyyMMdd-HHmmss}.json");
            File.Move(_filePath, result.QuarantinedPath);
            Logger.Warn($"tasks.json was corrupt; quarantined to {result.QuarantinedPath}");

            var bakPath = _filePath + ".bak";
            if (File.Exists(bakPath))
            {
                var backup = TryParse(bakPath);
                if (backup != null && backup.Version <= CurrentVersion)
                {
                    Tabs = backup.Tabs ?? new List<TodoTab>();
                    result.Outcome = TaskStoreLoadOutcome.LoadedBackup;
                    return result;
                }
            }

            result.Outcome = TaskStoreLoadOutcome.StartedEmptyAfterCorruption;
            return result;
        }

        private static Envelope TryParse(string path)
        {
            try
            {
                var envelope = JsonConvert.DeserializeObject<Envelope>(File.ReadAllText(path));
                if (envelope?.Version <= CurrentVersion &&
                    envelope.Tabs != null &&
                    !envelope.Tabs.All(TaskPresetService.ValidateTab))
                    throw new JsonSerializationException(
                        "Task data contains invalid managed preset structure.");
                return envelope;
            }
            catch (Exception ex)
            {
                Logger.Warn(ex, $"Failed to parse {path}");
                return null;
            }
        }

        public void MarkDirty(DateTime nowUtc)
        {
            if (!_dirty) _dirtySinceUtc = nowUtc;
            _dirty = true;
        }

        public void FlushIfDue(DateTime nowUtc)
        {
            if (_dirty && nowUtc - _dirtySinceUtc >= DebounceDelay) Save();
        }

        public void Save()
        {
            _dirty = false;
            if (ReadOnly) return;

            var json = JsonConvert.SerializeObject(
                new Envelope { Version = CurrentVersion, Tabs = Tabs }, Formatting.Indented);
            var tmp = _filePath + ".tmp";
            File.WriteAllText(tmp, json);
            if (File.Exists(_filePath))
                File.Replace(tmp, _filePath, _filePath + ".bak");
            else
                File.Move(tmp, _filePath);
        }
    }
}
