using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace Taskmaster.Models
{
    public class TodoTask
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "";
        public int Order { get; set; }
        public ResetScheduleType Schedule { get; set; } = ResetScheduleType.DailyServer;

        /// <summary>LocalTime schedule only: local wall-clock time of day the task resets.</summary>
        public TimeSpan? LocalResetTime { get; set; }

        /// <summary>Duration schedule only: cooldown since the last progress (any increment,
        /// not just full completion) before the task resets.</summary>
        public TimeSpan? ResetDuration { get; set; }

        public string ClipboardContent { get; set; }
        public string Notes { get; set; }

        public int TargetCount { get; set; } = 1;
        public int CurrentCount { get; set; }

        /// <summary>Stamped when the task reaches done. The ONLY persisted reset anchor.</summary>
        public DateTime? LastCompletedUtc { get; set; }

        /// <summary>Stamped on any progress; lets boundary resets clear partial counter progress.</summary>
        public DateTime? LastActivityUtc { get; set; }

        public List<TodoTask> Subtasks { get; set; } = new List<TodoTask>();

        [JsonIgnore]
        public bool HasSubtasks => Subtasks != null && Subtasks.Count > 0;

        [JsonIgnore]
        public bool IsDone => HasSubtasks
            ? Subtasks.All(s => s.IsDone)
            : CurrentCount >= TargetCount;

        /// <summary>One click: +1 on a counter, complete on a plain task, complete-all on a parent.</summary>
        public void Increment(DateTime nowUtc)
        {
            if (HasSubtasks) { CompleteAll(nowUtc); return; }
            if (CurrentCount >= TargetCount) return;
            CurrentCount++;
            LastActivityUtc = nowUtc;
            if (CurrentCount >= TargetCount) LastCompletedUtc = nowUtc;
        }

        /// <summary>Right-click: -1 on a counter, uncheck a plain task, uncheck-all on a parent.</summary>
        public void Decrement()
        {
            if (HasSubtasks) { UncheckAll(); return; }
            if (CurrentCount <= 0) return;
            CurrentCount--;
            if (CurrentCount < TargetCount) LastCompletedUtc = null;
        }

        public void CompleteAll(DateTime nowUtc)
        {
            if (HasSubtasks)
            {
                foreach (var s in Subtasks) s.CompleteAll(nowUtc);
                return;
            }
            if (CurrentCount >= TargetCount) return;
            CurrentCount = TargetCount;
            LastActivityUtc = nowUtc;
            LastCompletedUtc = nowUtc;
        }

        public void UncheckAll()
        {
            if (HasSubtasks)
            {
                foreach (var s in Subtasks) s.UncheckAll();
                return;
            }
            CurrentCount = 0;
            LastCompletedUtc = null;
        }

        /// <summary>
        /// A task with subtasks never stamps its own LastCompletedUtc/LastActivityUtc -
        /// Increment/CompleteAll/UncheckAll all cascade straight into the children and
        /// return early. Left alone, that timestamp is whatever it was before subtasks
        /// existed (or never set), so a Duration schedule on the group - which reads
        /// LastCompletedUtc as its one anchor - drifts from reality. Call this after any
        /// subtask mutation to keep the parent's own anchor equal to "the moment this
        /// group's completion state last changed," so the whole group shares one
        /// cooldown instead of each subtask running its own from when it was checked.
        /// </summary>
        public void SyncGroupAnchor(DateTime nowUtc)
        {
            if (!HasSubtasks) return;
            LastActivityUtc = nowUtc;
            LastCompletedUtc = IsDone ? nowUtc : (DateTime?)null;
        }

        /// <summary>
        /// Duration's countdown reads LastCompletedUtc/LastActivityUtc as its anchor;
        /// a task that's never been touched has neither, so the countdown stays blank
        /// until it's separately checked once. Call this whenever a Duration-scheduled
        /// task is (re)materialized with no progress yet - fresh creation, an editor
        /// save, a duplicate, or a tab-share import - so the countdown is visible
        /// immediately instead of silently waiting for first progress.
        /// </summary>
        public void EnsureDurationAnchor(DateTime nowUtc)
        {
            if (Schedule == ResetScheduleType.Duration &&
                !LastCompletedUtc.HasValue && !LastActivityUtc.HasValue)
                LastActivityUtc = nowUtc;
        }
    }
}
