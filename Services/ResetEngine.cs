using System;
using System.Collections.Generic;
using Taskmaster.Models;

namespace Taskmaster.Services
{
    /// <summary>
    /// Pure reset-boundary math. The anti-rot rules (see spec):
    /// no stored "next reset", no recurrence expansion, no end dates - every call
    /// derives boundaries from nowUtc alone. Server time == UTC.
    /// Boundaries per GW2 wiki (verified 2026-07-14):
    /// daily 00:00 UTC; weekly Mon 07:30 UTC; map bonus Thu 20:00 UTC;
    /// WvW EU Fri 18:00 UTC (approx); WvW NA Sat 02:00 UTC (approx);
    /// PSNA vendor locations rotate daily 08:00 UTC.
    /// </summary>
    public static class ResetEngine
    {
        private static readonly TimeSpan DailyAt = TimeSpan.Zero;
        public static readonly TimeSpan PsnaResetAtUtc = new TimeSpan(8, 0, 0);
        private static readonly TimeSpan WeeklyAt = new TimeSpan(7, 30, 0);
        private static readonly TimeSpan MapBonusAt = new TimeSpan(20, 0, 0);
        private static readonly TimeSpan WvwEuAt = new TimeSpan(18, 0, 0);
        private static readonly TimeSpan WvwNaAt = new TimeSpan(2, 0, 0);

        /// <summary>Most recent boundary at or before nowUtc. Null = no boundary (Never, uncompleted Duration).</summary>
        public static DateTime? LastBoundary(TodoTask task, DateTime nowUtc, TimeZoneInfo localTz = null)
        {
            switch (task.Schedule)
            {
                case ResetScheduleType.DailyServer:  return LastDaily(nowUtc, DailyAt);
                case ResetScheduleType.Psna:         return LastDaily(nowUtc, PsnaResetAtUtc);
                case ResetScheduleType.WeeklyServer: return LastWeekly(nowUtc, DayOfWeek.Monday, WeeklyAt);
                case ResetScheduleType.MapBonus:     return LastWeekly(nowUtc, DayOfWeek.Thursday, MapBonusAt);
                case ResetScheduleType.WvwEu:        return LastWeekly(nowUtc, DayOfWeek.Friday, WvwEuAt);
                case ResetScheduleType.WvwNa:        return LastWeekly(nowUtc, DayOfWeek.Saturday, WvwNaAt);
                case ResetScheduleType.LocalTime:
                    return LastLocalDaily(nowUtc, task.LocalResetTime ?? TimeSpan.Zero, localTz ?? TimeZoneInfo.Local);
                case ResetScheduleType.Duration:
                {
                    // Anchored on the most recent progress, not full completion - a
                    // counter's own TargetCount shouldn't gate whether its cooldown is
                    // even computable. The tradeoff: pausing longer than the duration
                    // between individual increments now lets the background sweep wipe
                    // that in-progress count (see ApplyResetRecursive), same as it would
                    // once fully completed.
                    var reference = task.LastCompletedUtc ?? task.LastActivityUtc;
                    return reference.HasValue && task.ResetDuration.HasValue
                        ? reference.Value + task.ResetDuration.Value
                        : (DateTime?)null;
                }
                default: return null;
            }
        }

        /// <summary>Next upcoming boundary strictly after nowUtc. Drives countdown display.</summary>
        public static DateTime? NextBoundary(TodoTask task, DateTime nowUtc, TimeZoneInfo localTz = null)
        {
            switch (task.Schedule)
            {
                case ResetScheduleType.DailyServer:  return LastDaily(nowUtc, DailyAt).AddDays(1);
                case ResetScheduleType.Psna:         return LastDaily(nowUtc, PsnaResetAtUtc).AddDays(1);
                case ResetScheduleType.WeeklyServer: return LastWeekly(nowUtc, DayOfWeek.Monday, WeeklyAt).AddDays(7);
                case ResetScheduleType.MapBonus:     return LastWeekly(nowUtc, DayOfWeek.Thursday, MapBonusAt).AddDays(7);
                case ResetScheduleType.WvwEu:        return LastWeekly(nowUtc, DayOfWeek.Friday, WvwEuAt).AddDays(7);
                case ResetScheduleType.WvwNa:        return LastWeekly(nowUtc, DayOfWeek.Saturday, WvwNaAt).AddDays(7);
                case ResetScheduleType.LocalTime:
                    return NextLocalDaily(nowUtc, task.LocalResetTime ?? TimeSpan.Zero, localTz ?? TimeZoneInfo.Local);
                case ResetScheduleType.Duration:
                {
                    var b = LastBoundary(task, nowUtc);
                    return b.HasValue && b.Value > nowUtc ? b : null;
                }
                default: return null;
            }
        }

        /// <summary>
        /// Evaluate every task (and subtask) and zero the ones whose completion/progress
        /// predates the last boundary. Idempotent; a 2-week hibernate gap applies exactly
        /// one reset per task. Returns the number of tasks reset.
        /// </summary>
        public static int ApplyResets(IEnumerable<TodoTab> tabs, DateTime nowUtc, TimeZoneInfo localTz = null)
        {
            int applied = 0;
            foreach (var tab in tabs)
                foreach (var task in tab.Tasks)
                    applied += ApplyResetRecursive(task, nowUtc, localTz);
            return applied;
        }

        private static int ApplyResetRecursive(TodoTask task, DateTime nowUtc, TimeZoneInfo localTz)
        {
            // Clock moved backwards (VM restore, manual change): clamp future stamps to now.
            if (task.LastCompletedUtc.HasValue && task.LastCompletedUtc.Value > nowUtc)
                task.LastCompletedUtc = nowUtc;
            if (task.LastActivityUtc.HasValue && task.LastActivityUtc.Value > nowUtc)
                task.LastActivityUtc = nowUtc;

            if (task.HasSubtasks)
            {
                int applied = 0;
                foreach (var s in task.Subtasks) applied += ApplyResetRecursive(s, nowUtc, localTz);
                // A subtask resetting can flip the group's IsDone, which is what the
                // parent's own anchor (used for a Duration schedule on the group) needs
                // to track - keep it in sync even when no user click triggered this.
                if (applied > 0) task.SyncGroupAnchor(nowUtc);
                return applied;
            }

            var boundary = LastBoundary(task, nowUtc, localTz);
            if (!boundary.HasValue) return 0;

            var reference = task.LastCompletedUtc ?? task.LastActivityUtc;
            if (!reference.HasValue) return 0;

            // Duration boundary is completion+cooldown (a future-facing stamp), so "due"
            // means now has passed it; calendar schedules compare activity vs boundary.
            bool due = task.Schedule == ResetScheduleType.Duration
                ? nowUtc >= boundary.Value
                : reference.Value < boundary.Value;

            if (!due || (task.CurrentCount == 0 && !task.LastCompletedUtc.HasValue)) return 0;

            task.CurrentCount = 0;
            task.LastCompletedUtc = null;
            task.LastActivityUtc = null;
            return 1;
        }

        private static DateTime LastDaily(DateTime nowUtc, TimeSpan at)
        {
            var candidate = nowUtc.Date + at;
            return candidate <= nowUtc ? candidate : candidate.AddDays(-1);
        }

        private static DateTime LastWeekly(DateTime nowUtc, DayOfWeek day, TimeSpan at)
        {
            int diff = ((int)nowUtc.DayOfWeek - (int)day + 7) % 7;
            var candidate = nowUtc.Date.AddDays(-diff) + at;
            return candidate <= nowUtc ? candidate : candidate.AddDays(-7);
        }

        private static DateTime LastLocalDaily(DateTime nowUtc, TimeSpan localAt, TimeZoneInfo tz)
        {
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
            var candidateLocal = nowLocal.Date + localAt;
            if (candidateLocal > nowLocal) candidateLocal = candidateLocal.AddDays(-1);
            var utc = SafeLocalToUtc(candidateLocal, tz);
            // A DST-shifted candidate can land after now; step back a day if so.
            return utc <= nowUtc ? utc : SafeLocalToUtc(candidateLocal.AddDays(-1), tz);
        }

        private static DateTime NextLocalDaily(DateTime nowUtc, TimeSpan localAt, TimeZoneInfo tz)
        {
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, tz);
            var candidateLocal = nowLocal.Date + localAt;
            if (candidateLocal <= nowLocal) candidateLocal = candidateLocal.AddDays(1);
            var utc = SafeLocalToUtc(candidateLocal, tz);
            return utc > nowUtc ? utc : SafeLocalToUtc(candidateLocal.AddDays(1), tz);
        }

        private static DateTime SafeLocalToUtc(DateTime local, TimeZoneInfo tz)
        {
            local = DateTime.SpecifyKind(local, DateTimeKind.Unspecified);
            if (tz.IsInvalidTime(local))
            {
                // Spring-forward gap: resolve to one hour later (first valid instant region).
                local = local.AddHours(1);
            }
            if (tz.IsAmbiguousTime(local))
            {
                // Fall-back overlap: pick the earliest instant (largest offset = first pass).
                var offsets = tz.GetAmbiguousTimeOffsets(local);
                var max = offsets[0];
                foreach (var o in offsets) if (o > max) max = o;
                return DateTime.SpecifyKind(local - max, DateTimeKind.Utc);
            }
            return TimeZoneInfo.ConvertTimeToUtc(local, tz);
        }
    }
}
