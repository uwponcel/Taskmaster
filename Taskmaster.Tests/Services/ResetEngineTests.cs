using System;
using Taskmaster.Models;
using Taskmaster.Services;
using Xunit;

namespace Taskmaster.Tests.Services
{
    public class ResetEngineTests
    {
        private static DateTime Utc(int y, int mo, int d, int h, int mi) =>
            new DateTime(y, mo, d, h, mi, 0, DateTimeKind.Utc);

        private static TodoTask Task(ResetScheduleType s) => new TodoTask { Schedule = s };

        // ---- LastBoundary ----

        [Theory]
        // 2026-07-14 is a Tuesday.
        [InlineData(ResetScheduleType.DailyServer, 2026, 7, 14, 12, 0, /*expect*/ 2026, 7, 14, 0, 0)]
        [InlineData(ResetScheduleType.Psna,        2026, 7, 14, 7, 59, /*expect*/ 2026, 7, 13, 8, 0)]
        [InlineData(ResetScheduleType.Psna,        2026, 7, 14, 8, 0,  /*expect*/ 2026, 7, 14, 8, 0)]
        [InlineData(ResetScheduleType.WeeklyServer,2026, 7, 14, 12, 0, /*expect*/ 2026, 7, 13, 7, 30)]
        [InlineData(ResetScheduleType.WeeklyServer,2026, 7, 13, 7, 29, /*expect*/ 2026, 7, 6, 7, 30)]
        [InlineData(ResetScheduleType.MapBonus,    2026, 7, 14, 12, 0, /*expect*/ 2026, 7, 9, 20, 0)]
        [InlineData(ResetScheduleType.WvwEu,       2026, 7, 14, 12, 0, /*expect*/ 2026, 7, 10, 18, 0)]
        [InlineData(ResetScheduleType.WvwNa,       2026, 7, 14, 12, 0, /*expect*/ 2026, 7, 11, 2, 0)]
        public void LastBoundary_ServerSchedules(ResetScheduleType s,
            int y, int mo, int d, int h, int mi,
            int ey, int emo, int ed, int eh, int emi)
        {
            var result = ResetEngine.LastBoundary(Task(s), Utc(y, mo, d, h, mi));
            Assert.Equal(Utc(ey, emo, ed, eh, emi), result);
        }

        [Fact]
        public void LastBoundary_Never_IsNull()
        {
            Assert.Null(ResetEngine.LastBoundary(Task(ResetScheduleType.Never), Utc(2026, 7, 14, 12, 0)));
        }

        [Fact]
        public void LastBoundary_Duration_NullUntouched()
        {
            var t = new TodoTask { Schedule = ResetScheduleType.Duration, ResetDuration = TimeSpan.FromHours(2) };
            Assert.Null(ResetEngine.LastBoundary(t, Utc(2026, 7, 14, 12, 0)));
            t.LastCompletedUtc = Utc(2026, 7, 14, 10, 0);
            Assert.Equal(Utc(2026, 7, 14, 12, 0), ResetEngine.LastBoundary(t, Utc(2026, 7, 14, 13, 0)));
        }

        [Fact]
        public void LastBoundary_Duration_PartialProgress_AnchorsOnLastActivityNotCompletion()
        {
            // A counter that's never been fully completed (LastCompletedUtc still null)
            // shouldn't be stuck with no computable cooldown just because it's not done -
            // the most recent increment (LastActivityUtc) anchors it instead.
            var t = new TodoTask
            {
                Schedule = ResetScheduleType.Duration,
                ResetDuration = TimeSpan.FromHours(2),
                TargetCount = 4,
                CurrentCount = 2,
                LastActivityUtc = Utc(2026, 7, 14, 10, 0)
            };
            Assert.Equal(Utc(2026, 7, 14, 12, 0), ResetEngine.LastBoundary(t, Utc(2026, 7, 14, 13, 0)));
        }

        // ---- LocalTime + DST (Eastern: spring-forward 2026-03-08 02:00, fall-back 2026-11-01 02:00) ----

        private static readonly TimeZoneInfo Eastern =
            TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time");

        [Fact]
        public void LastBoundary_LocalTime_NormalDay()
        {
            var t = new TodoTask { Schedule = ResetScheduleType.LocalTime, LocalResetTime = new TimeSpan(6, 0, 0) };
            // 2026-07-14 12:00 UTC = 08:00 EDT; last 06:00 EDT = 10:00 UTC same day
            var result = ResetEngine.LastBoundary(t, Utc(2026, 7, 14, 12, 0), Eastern);
            Assert.Equal(Utc(2026, 7, 14, 10, 0), result);
        }

        [Fact]
        public void LastBoundary_LocalTime_SpringForwardGap_ResolvesToValidInstant()
        {
            // 02:30 local does not exist on 2026-03-08 in Eastern. Must not throw.
            var t = new TodoTask { Schedule = ResetScheduleType.LocalTime, LocalResetTime = new TimeSpan(2, 30, 0) };
            var result = ResetEngine.LastBoundary(t, Utc(2026, 3, 8, 12, 0), Eastern);
            Assert.NotNull(result);
            Assert.True(result.Value <= Utc(2026, 3, 8, 12, 0));
        }

        [Fact]
        public void LastBoundary_LocalTime_FallBackAmbiguity_DoesNotThrow()
        {
            // 01:30 local occurs twice on 2026-11-01 in Eastern. Must not throw.
            var t = new TodoTask { Schedule = ResetScheduleType.LocalTime, LocalResetTime = new TimeSpan(1, 30, 0) };
            var result = ResetEngine.LastBoundary(t, Utc(2026, 11, 1, 12, 0), Eastern);
            Assert.NotNull(result);
        }

        // ---- NextBoundary ----

        [Fact]
        public void NextBoundary_Daily_IsTomorrowMidnight()
        {
            var result = ResetEngine.NextBoundary(Task(ResetScheduleType.DailyServer), Utc(2026, 7, 14, 12, 0));
            Assert.Equal(Utc(2026, 7, 15, 0, 0), result);
        }

        [Fact]
        public void NextBoundary_Duration_CompletionPlusCooldown()
        {
            var t = new TodoTask { Schedule = ResetScheduleType.Duration, ResetDuration = TimeSpan.FromHours(3) };
            Assert.Null(ResetEngine.NextBoundary(t, Utc(2026, 7, 14, 12, 0)));
            t.LastCompletedUtc = Utc(2026, 7, 14, 12, 0);
            Assert.Equal(Utc(2026, 7, 14, 15, 0), ResetEngine.NextBoundary(t, Utc(2026, 7, 14, 13, 0)));
        }

        // ---- ApplyResets ----

        private static TodoTab TabWith(params TodoTask[] tasks)
        {
            var tab = new TodoTab { Name = "t" };
            tab.Tasks.AddRange(tasks);
            return tab;
        }

        [Fact]
        public void ApplyResets_CompletedBeforeBoundary_Resets()
        {
            var t = Task(ResetScheduleType.DailyServer);
            t.Increment(Utc(2026, 7, 13, 23, 0));
            var n = ResetEngine.ApplyResets(new[] { TabWith(t) }, Utc(2026, 7, 14, 1, 0));
            Assert.Equal(1, n);
            Assert.False(t.IsDone);
            Assert.Equal(0, t.CurrentCount);
            Assert.Null(t.LastCompletedUtc);
        }

        [Fact]
        public void ApplyResets_CompletedAfterBoundary_Stays()
        {
            var t = Task(ResetScheduleType.DailyServer);
            t.Increment(Utc(2026, 7, 14, 0, 30));
            var n = ResetEngine.ApplyResets(new[] { TabWith(t) }, Utc(2026, 7, 14, 12, 0));
            Assert.Equal(0, n);
            Assert.True(t.IsDone);
        }

        [Fact]
        public void ApplyResets_HibernateGap_ManyBoundariesMissed_SingleReset()
        {
            var t = Task(ResetScheduleType.DailyServer);
            t.Increment(Utc(2026, 7, 1, 12, 0));
            var n = ResetEngine.ApplyResets(new[] { TabWith(t) }, Utc(2026, 7, 14, 12, 0));
            Assert.Equal(1, n);
            Assert.False(t.IsDone);
        }

        [Fact]
        public void ApplyResets_PartialCounterProgress_ClearedAtBoundary()
        {
            var t = new TodoTask { Schedule = ResetScheduleType.DailyServer, TargetCount = 5 };
            t.Increment(Utc(2026, 7, 13, 23, 0));
            t.Increment(Utc(2026, 7, 13, 23, 30));
            var n = ResetEngine.ApplyResets(new[] { TabWith(t) }, Utc(2026, 7, 14, 1, 0));
            Assert.Equal(1, n);
            Assert.Equal(0, t.CurrentCount);
        }

        [Fact]
        public void ApplyResets_ClockMovedBackwards_ClampsWithoutReset()
        {
            var t = Task(ResetScheduleType.DailyServer);
            t.Increment(Utc(2026, 7, 14, 12, 0));
            var now = Utc(2026, 7, 14, 6, 0); // clock went back 6h, same reset period
            var n = ResetEngine.ApplyResets(new[] { TabWith(t) }, now);
            Assert.Equal(0, n);
            Assert.Equal(now, t.LastCompletedUtc);
            Assert.True(t.IsDone);
        }

        [Fact]
        public void ApplyResets_Duration_PartialCounter_ResetsAfterCooldownSinceLastIncrement()
        {
            // Accepted tradeoff of anchoring Duration on last activity: a counter that's
            // still in progress is no longer "protected" from the sweep just by being
            // incomplete - pausing longer than the cooldown between increments wipes it.
            var t = new TodoTask { Schedule = ResetScheduleType.Duration, ResetDuration = TimeSpan.FromMinutes(30), TargetCount = 4 };
            t.Increment(Utc(2026, 7, 14, 12, 0));
            Assert.Equal(1, t.CurrentCount);
            Assert.Equal(0, ResetEngine.ApplyResets(new[] { TabWith(t) }, Utc(2026, 7, 14, 12, 29)));
            Assert.Equal(1, ResetEngine.ApplyResets(new[] { TabWith(t) }, Utc(2026, 7, 14, 12, 30)));
            Assert.Equal(0, t.CurrentCount);
        }

        [Fact]
        public void ApplyResets_Duration_ResetsAfterCooldown()
        {
            var t = new TodoTask { Schedule = ResetScheduleType.Duration, ResetDuration = TimeSpan.FromMinutes(30) };
            t.Increment(Utc(2026, 7, 14, 12, 0));
            Assert.Equal(0, ResetEngine.ApplyResets(new[] { TabWith(t) }, Utc(2026, 7, 14, 12, 29)));
            Assert.Equal(1, ResetEngine.ApplyResets(new[] { TabWith(t) }, Utc(2026, 7, 14, 12, 30)));
            Assert.False(t.IsDone);
        }

        [Fact]
        public void ApplyResets_Subtasks_ResetIndividually()
        {
            var parent = Task(ResetScheduleType.DailyServer);
            parent.Subtasks.Add(Task(ResetScheduleType.DailyServer));
            parent.Subtasks.Add(Task(ResetScheduleType.DailyServer));
            parent.CompleteAll(Utc(2026, 7, 13, 23, 0));
            var n = ResetEngine.ApplyResets(new[] { TabWith(parent) }, Utc(2026, 7, 14, 1, 0));
            Assert.Equal(2, n);
            Assert.False(parent.IsDone);
        }

        [Fact]
        public void ApplyResets_NeverTouched_NoReset()
        {
            var t = Task(ResetScheduleType.DailyServer);
            var n = ResetEngine.ApplyResets(new[] { TabWith(t) }, Utc(2026, 7, 14, 12, 0));
            Assert.Equal(0, n);
        }

        [Fact]
        public void ApplyResets_SubtaskReset_ResyncsParentAnchor()
        {
            var parent = Task(ResetScheduleType.DailyServer);
            parent.Subtasks.Add(Task(ResetScheduleType.DailyServer));
            parent.CompleteAll(Utc(2026, 7, 13, 23, 0));
            var nowUtc = Utc(2026, 7, 14, 1, 0);
            ResetEngine.ApplyResets(new[] { TabWith(parent) }, nowUtc);
            // The subtask resetting flips the group back to not-done - the parent's own
            // anchor (what a Duration schedule on the group would read) must follow.
            Assert.Null(parent.LastCompletedUtc);
        }
    }
}
