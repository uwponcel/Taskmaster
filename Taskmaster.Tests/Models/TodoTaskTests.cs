using System;
using Taskmaster.Models;
using Xunit;

namespace Taskmaster.Tests.Models
{
    public class TodoTaskTests
    {
        private static readonly DateTime Now = new DateTime(2026, 7, 14, 12, 0, 0, DateTimeKind.Utc);

        [Fact]
        public void PlainTask_Increment_CompletesAndStampsTime()
        {
            var t = new TodoTask { Name = "a" };
            t.Increment(Now);
            Assert.True(t.IsDone);
            Assert.Equal(Now, t.LastCompletedUtc);
            Assert.Equal(Now, t.LastActivityUtc);
        }

        [Fact]
        public void CounterTask_NeedsTargetCountIncrements()
        {
            var t = new TodoTask { Name = "a", TargetCount = 3 };
            t.Increment(Now);
            t.Increment(Now);
            Assert.False(t.IsDone);
            Assert.Null(t.LastCompletedUtc);
            t.Increment(Now);
            Assert.True(t.IsDone);
            Assert.Equal(Now, t.LastCompletedUtc);
        }

        [Fact]
        public void CounterTask_IncrementPastTarget_Clamps()
        {
            var t = new TodoTask { TargetCount = 2 };
            t.Increment(Now); t.Increment(Now); t.Increment(Now);
            Assert.Equal(2, t.CurrentCount);
        }

        [Fact]
        public void Decrement_BelowTarget_ClearsCompletion()
        {
            var t = new TodoTask { TargetCount = 2 };
            t.Increment(Now); t.Increment(Now);
            Assert.True(t.IsDone);
            t.Decrement();
            Assert.False(t.IsDone);
            Assert.Null(t.LastCompletedUtc);
            Assert.Equal(1, t.CurrentCount);
        }

        [Fact]
        public void Decrement_WhenCountExceedsTarget_KeepsCompletionWhileStillDone()
        {
            // Invariant broken externally (e.g. TargetCount edited down after completion)
            var t = new TodoTask { TargetCount = 3, CurrentCount = 5, LastCompletedUtc = Now };
            t.Decrement();
            Assert.True(t.IsDone);              // 4 >= 3
            Assert.Equal(Now, t.LastCompletedUtc); // still done => stamp survives
            t.Decrement(); t.Decrement();
            Assert.False(t.IsDone);             // 2 < 3
            Assert.Null(t.LastCompletedUtc);    // crossed to not-done => cleared
        }

        [Fact]
        public void ParentTask_DoneIffAllSubtasksDone()
        {
            var parent = new TodoTask { Name = "raid" };
            parent.Subtasks.Add(new TodoTask { Name = "w1" });
            parent.Subtasks.Add(new TodoTask { Name = "w2" });
            Assert.False(parent.IsDone);
            parent.Subtasks[0].Increment(Now);
            Assert.False(parent.IsDone);
            parent.Subtasks[1].Increment(Now);
            Assert.True(parent.IsDone);
        }

        [Fact]
        public void ParentTask_CompleteAll_ChecksEveryChild()
        {
            var parent = new TodoTask();
            parent.Subtasks.Add(new TodoTask { TargetCount = 3 });
            parent.Subtasks.Add(new TodoTask());
            parent.CompleteAll(Now);
            Assert.True(parent.IsDone);
            Assert.Equal(3, parent.Subtasks[0].CurrentCount);
        }

        [Fact]
        public void ParentTask_UncheckAll_ClearsEveryChild()
        {
            var parent = new TodoTask();
            parent.Subtasks.Add(new TodoTask());
            parent.CompleteAll(Now);
            parent.UncheckAll();
            Assert.False(parent.IsDone);
            Assert.Null(parent.Subtasks[0].LastCompletedUtc);
            Assert.Equal(0, parent.Subtasks[0].CurrentCount);
        }

        [Fact]
        public void SyncGroupAnchor_AllSubtasksDone_StampsParentCompletion()
        {
            var parent = new TodoTask();
            parent.Subtasks.Add(new TodoTask());
            parent.Subtasks[0].Increment(Now);
            parent.SyncGroupAnchor(Now);
            Assert.Equal(Now, parent.LastCompletedUtc);
            Assert.Equal(Now, parent.LastActivityUtc);
        }

        [Fact]
        public void SyncGroupAnchor_NotAllSubtasksDone_ClearsParentCompletion()
        {
            var parent = new TodoTask { LastCompletedUtc = Now };
            parent.Subtasks.Add(new TodoTask());
            parent.Subtasks.Add(new TodoTask());
            parent.Subtasks[0].Increment(Now);
            var later = Now.AddMinutes(5);
            parent.SyncGroupAnchor(later);
            Assert.Null(parent.LastCompletedUtc);
            Assert.Equal(later, parent.LastActivityUtc);
        }

        [Fact]
        public void SyncGroupAnchor_LeafTask_IsNoOp()
        {
            var t = new TodoTask { LastCompletedUtc = Now };
            t.SyncGroupAnchor(Now.AddMinutes(5));
            Assert.Equal(Now, t.LastCompletedUtc);
            Assert.Null(t.LastActivityUtc);
        }

        [Fact]
        public void EnsureDurationAnchor_NeverTouched_StampsLastActivity()
        {
            var t = new TodoTask { Schedule = ResetScheduleType.Duration };
            t.EnsureDurationAnchor(Now);
            Assert.Equal(Now, t.LastActivityUtc);
            Assert.Null(t.LastCompletedUtc);
        }

        [Fact]
        public void EnsureDurationAnchor_AlreadyHasAnchor_LeavesItAlone()
        {
            var earlier = Now.AddHours(-1);
            var t = new TodoTask { Schedule = ResetScheduleType.Duration, LastActivityUtc = earlier };
            t.EnsureDurationAnchor(Now);
            Assert.Equal(earlier, t.LastActivityUtc);
        }

        [Fact]
        public void EnsureDurationAnchor_NonDurationSchedule_IsNoOp()
        {
            var t = new TodoTask { Schedule = ResetScheduleType.DailyServer };
            t.EnsureDurationAnchor(Now);
            Assert.Null(t.LastActivityUtc);
        }
    }
}
