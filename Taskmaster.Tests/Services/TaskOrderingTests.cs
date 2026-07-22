using System.Collections.Generic;
using Taskmaster.Models;
using Taskmaster.Services;
using Xunit;

namespace Taskmaster.Tests.Services
{
    public class TaskOrderingTests
    {
        [Fact]
        public void MoveToIndex_ReordersAndNormalizes()
        {
            var a = new TodoTask { Name = "a", Order = 20 };
            var b = new TodoTask { Name = "b", Order = 10 };
            var c = new TodoTask { Name = "c", Order = 30 };
            var tasks = new List<TodoTask> { a, b, c };

            Assert.True(TaskOrdering.MoveToIndex(tasks, c, 0));

            Assert.Equal(new[] { c, b, a }, tasks);
            Assert.Equal(new[] { 0, 1, 2 }, new[] { c.Order, b.Order, a.Order });
        }

        [Fact]
        public void MoveBy_AtBoundaryDoesNotMove()
        {
            var a = new TodoTask { Name = "a", Order = 0 };
            var b = new TodoTask { Name = "b", Order = 1 };
            var tasks = new List<TodoTask> { a, b };

            Assert.False(TaskOrdering.MoveBy(tasks, a, -1));
            Assert.Equal(new[] { a, b }, tasks);
        }

        [Fact]
        public void MoveToStartAndEnd_WorkForSubtaskLists()
        {
            var a = new TodoTask { Name = "a", Order = 0 };
            var b = new TodoTask { Name = "b", Order = 1 };
            var c = new TodoTask { Name = "c", Order = 2 };
            var subtasks = new List<TodoTask> { a, b, c };

            Assert.True(TaskOrdering.MoveToStart(subtasks, c));
            Assert.True(TaskOrdering.MoveToEnd(subtasks, a));

            Assert.Equal(new[] { c, b, a }, subtasks);
        }

        [Fact]
        public void MoveByVisible_SkipsHiddenNeighbors()
        {
            var a = new TodoTask { Name = "a", Order = 0 };
            var hidden = new TodoTask { Name = "hidden", Order = 1, CurrentCount = 1 };
            var b = new TodoTask { Name = "b", Order = 2 };
            var tasks = new List<TodoTask> { a, hidden, b };

            Assert.True(TaskOrdering.MoveByVisible(tasks, b, -1, task => !task.IsDone));

            Assert.Equal(new[] { b, a, hidden }, tasks);
        }
    }
}
