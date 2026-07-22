using System;
using System.Collections.Generic;
using System.Linq;
using Taskmaster.Models;

namespace Taskmaster.Services
{
    public static class TaskOrdering
    {
        public static int OrderedIndexOf(IList<TodoTask> tasks, TodoTask task) =>
            Ordered(tasks).IndexOf(task);

        public static bool MoveBy(IList<TodoTask> tasks, TodoTask task, int delta)
        {
            int currentIndex = OrderedIndexOf(tasks, task);
            if (currentIndex < 0) return false;
            return MoveToIndex(tasks, task, currentIndex + delta);
        }

        public static bool MoveByVisible(
            IList<TodoTask> tasks,
            TodoTask task,
            int delta,
            Func<TodoTask, bool> isVisible)
        {
            if (tasks == null || task == null || isVisible == null) return false;

            var ordered = Ordered(tasks);
            var visible = ordered.Where(candidate => candidate == task || isVisible(candidate)).ToList();
            int visibleIndex = visible.IndexOf(task);
            int targetVisibleIndex = visibleIndex + delta;
            if (visibleIndex < 0 || targetVisibleIndex < 0 || targetVisibleIndex >= visible.Count)
                return false;

            var target = visible[targetVisibleIndex];
            int sourceIndex = ordered.IndexOf(task);
            int targetIndex = ordered.IndexOf(target);
            if (delta > 0) targetIndex++;
            if (sourceIndex < targetIndex) targetIndex--;
            return MoveToIndex(tasks, task, targetIndex);
        }

        public static bool MoveToStart(IList<TodoTask> tasks, TodoTask task) =>
            MoveToIndex(tasks, task, 0);

        public static bool MoveToEnd(IList<TodoTask> tasks, TodoTask task) =>
            MoveToIndex(tasks, task, Math.Max(0, tasks.Count - 1));

        public static bool MoveToIndex(IList<TodoTask> tasks, TodoTask task, int targetIndex)
        {
            if (tasks == null || task == null) return false;

            var ordered = Ordered(tasks);
            int currentIndex = ordered.IndexOf(task);
            if (currentIndex < 0) return false;

            ordered.RemoveAt(currentIndex);
            int clampedTarget = Math.Max(0, Math.Min(targetIndex, ordered.Count));
            ordered.Insert(clampedTarget, task);
            if (currentIndex == clampedTarget)
            {
                Normalize(tasks);
                return false;
            }

            Replace(tasks, ordered);
            return true;
        }

        public static void Normalize(IList<TodoTask> tasks)
        {
            if (tasks == null) return;
            Replace(tasks, Ordered(tasks));
        }

        private static List<TodoTask> Ordered(IList<TodoTask> tasks) =>
            tasks
                .Select((task, index) => new { task, index })
                .OrderBy(item => item.task.Order)
                .ThenBy(item => item.index)
                .Select(item => item.task)
                .ToList();

        private static void Replace(IList<TodoTask> tasks, IList<TodoTask> ordered)
        {
            tasks.Clear();
            for (int i = 0; i < ordered.Count; i++)
            {
                ordered[i].Order = i;
                tasks.Add(ordered[i]);
            }
        }
    }
}
