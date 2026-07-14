using System;
using System.Collections.Generic;
using System.Linq;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using Taskmaster.Models;

namespace Taskmaster.UI
{
    /// <summary>
    /// The task list for one tab: parent rows, expanded subtask rows, and an inline
    /// edit panel. Rebuilds wholesale on any structural change. The "+ Add task" button
    /// itself lives in TaskmasterWindow (a persistent top-of-window control, not part
    /// of this scrollable list) - see AddNewTask.
    /// </summary>
    public class TaskListPanel : FlowPanel
    {
        private TodoTab _tab;
        private readonly HashSet<Guid> _expanded = new HashSet<Guid>();
        private Guid? _editingTaskId;
        // Tracks a task created via "+ Add task" whose editor is still open, so its
        // name field shows a placeholder instead of literal pre-filled text.
        private Guid? _newTaskId;
        private bool _hideDone;
        private bool _locked;
        private readonly List<TaskRow> _rows = new List<TaskRow>();
        // The editor for _editingTaskId, if one is open - the row's checkmark button
        // has no editor of its own to call Apply() on, so it goes through this.
        private TaskEditPanel _activeEditPanel;

        /// <summary>Any data mutation happened (check, edit, delete...). Window persists + refreshes badges.</summary>
        public event Action DataChanged;
        /// <summary>Row asked for a context menu; window builds it (needs the tab list for move-to).</summary>
        public event Action<TodoTask> TaskContextMenuRequested;
        public event Action<TodoTask> CopyToClipboardRequested;

        public TaskListPanel()
        {
            FlowDirection = ControlFlowDirection.SingleTopToBottom;
            CanScroll = true;
            ControlPadding = new Vector2(0, 2);
        }

        public bool HideDone
        {
            get => _hideDone;
            set { _hideDone = value; Rebuild(); }
        }

        public bool Locked
        {
            get => _locked;
            set { _locked = value; Rebuild(); }
        }

        public void ShowTab(TodoTab tab)
        {
            if (_tab?.Id != tab?.Id) _editingTaskId = null;
            _tab = tab;
            Rebuild();
        }

        public void BeginEdit(TodoTask task)
        {
            _editingTaskId = task.Id;
            _newTaskId = null;
            Rebuild();
        }

        /// <summary>
        /// Registers a task the window just created (via the top "+ Add task" button)
        /// as the one being edited, with placeholder-name treatment in its editor.
        /// The caller is responsible for adding the task to the active tab first.
        /// </summary>
        public void AddNewTask(TodoTask task)
        {
            _editingTaskId = task.Id;
            _newTaskId = task.Id;
            Rebuild();
        }

        public void RefreshCountdowns(DateTime nowUtc)
        {
            foreach (var row in _rows) row.RefreshDisplay(nowUtc);
        }

        public void Rebuild()
        {
            foreach (var c in Children.ToList()) c.Dispose();
            _rows.Clear();
            if (_tab == null) return;

            var nowUtc = DateTime.UtcNow;

            foreach (var task in _tab.Tasks.OrderBy(t => t.Order).ToList())
            {
                if (_hideDone && task.IsDone && task.Id != _editingTaskId) continue;
                AddRow(task, isSubtask: false, nowUtc);

                if (task.HasSubtasks && _expanded.Contains(task.Id))
                    foreach (var sub in task.Subtasks)
                    {
                        if (_hideDone && sub.IsDone) continue;
                        AddRow(sub, isSubtask: true, nowUtc, parent: task);
                    }

                if (task.Id == _editingTaskId)
                    AddEditPanel(task, isNew: task.Id == _newTaskId);
            }
        }

        private void AddRow(TodoTask task, bool isSubtask, DateTime nowUtc, TodoTask parent = null)
        {
            var row = new TaskRow(task, isSubtask)
            {
                Parent = this,
                Width = ContentRegion.Width,
                IsExpanded = _expanded.Contains(task.Id),
                Locked = _locked,
                IsEditing = task.Id == _editingTaskId,
                // A subtask's countdown reflects the parent's shared anchor, not its own -
                // see TodoTask.SyncGroupAnchor for why the parent's anchor is authoritative.
                CountdownAnchor = parent
            };
            row.RefreshDisplay(nowUtc);
            _rows.Add(row);

            // Left-click toggles both directions - check when not done, uncheck when done.
            // Right-click is context-menu only (see TaskRow.OnRightMouseButtonPressed).
            row.ToggleRequested += () =>
            {
                if (task.IsDone) task.UncheckAll(); else task.Increment(DateTime.UtcNow);
                (parent ?? task).SyncGroupAnchor(DateTime.UtcNow);
                AfterMutation();
            };
            row.ExpandToggled += () =>
            {
                if (!_expanded.Remove(task.Id)) _expanded.Add(task.Id);
                Rebuild();
            };
            row.SaveRequested += () => _activeEditPanel?.Apply();
            // Clicking the edit glyph on the task that's already open closes it again.
            row.EditRequested += () =>
            {
                if (_editingTaskId == task.Id) { _editingTaskId = null; _newTaskId = null; Rebuild(); }
                else BeginEdit(task);
            };
            row.ContextMenuRequested += () => TaskContextMenuRequested?.Invoke(task);
            row.CopyRequested += () =>
            {
                CopyToClipboardRequested?.Invoke(task);
                row.FlashCopied();
            };
        }

        private void AddEditPanel(TodoTask task, bool isNew = false)
        {
            var edit = new TaskEditPanel(task, isNew)
            {
                Parent = this,
                Width = ContentRegion.Width
            };
            _activeEditPanel = edit;
            edit.Saved += () => { _editingTaskId = null; _newTaskId = null; AfterMutation(); };
        }

        private void AfterMutation()
        {
            DataChanged?.Invoke();
            Rebuild();
        }
    }
}
