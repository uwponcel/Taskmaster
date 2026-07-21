using System;
using System.Collections.Generic;

namespace Taskmaster.UI
{
    public sealed class TaskSelection
    {
        private readonly HashSet<Guid> _selectedIds = new HashSet<Guid>();
        private Guid? _anchorId;

        public IReadOnlyCollection<Guid> SelectedIds => _selectedIds;

        public bool IsSelected(Guid taskId) => _selectedIds.Contains(taskId);

        public void Select(Guid taskId, IReadOnlyList<Guid> orderedTaskIds, bool extendRange, bool toggle)
        {
            int taskIndex = IndexOf(orderedTaskIds, taskId);
            if (taskIndex < 0) return;

            if (extendRange && _anchorId.HasValue)
            {
                int anchorIndex = IndexOf(orderedTaskIds, _anchorId.Value);
                if (anchorIndex >= 0)
                {
                    if (!toggle) _selectedIds.Clear();

                    int from = Math.Min(anchorIndex, taskIndex);
                    int to = Math.Max(anchorIndex, taskIndex);
                    for (int i = from; i <= to; i++) _selectedIds.Add(orderedTaskIds[i]);
                    return;
                }
            }

            if (toggle)
            {
                if (!_selectedIds.Add(taskId)) _selectedIds.Remove(taskId);
            }
            else
            {
                _selectedIds.Clear();
                _selectedIds.Add(taskId);
            }

            _anchorId = taskId;
        }

        public void SelectForContext(Guid taskId)
        {
            if (_selectedIds.Contains(taskId)) return;
            _selectedIds.Clear();
            _selectedIds.Add(taskId);
            _anchorId = taskId;
        }

        public void Retain(IReadOnlyList<Guid> orderedTaskIds)
        {
            _selectedIds.RemoveWhere(id => IndexOf(orderedTaskIds, id) < 0);
            if (_anchorId.HasValue && IndexOf(orderedTaskIds, _anchorId.Value) < 0)
                _anchorId = null;
        }

        public void Clear()
        {
            _selectedIds.Clear();
            _anchorId = null;
        }

        private static int IndexOf(IReadOnlyList<Guid> taskIds, Guid taskId)
        {
            for (int i = 0; i < taskIds.Count; i++)
                if (taskIds[i] == taskId) return i;
            return -1;
        }
    }
}
