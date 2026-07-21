using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
        private readonly List<Guid> _selectableTaskIds = new List<Guid>();
        private readonly TaskSelection _selection = new TaskSelection();
        private Guid? _pendingScrollTaskId;
        private int _scrollApplyFrames;
        private float? _pendingScrollDistance;
        private int _scrollRestoreFrames;
        private Scrollbar _scrollbar;
        private static readonly FieldInfo PanelScrollbarField =
            typeof(Panel).GetField("_panelScrollbar", BindingFlags.NonPublic | BindingFlags.Instance);
        // The editor for _editingTaskId, if one is open - the row's checkmark button
        // has no editor of its own to call Apply() on, so it goes through this.
        private TaskEditPanel _activeEditPanel;

        /// <summary>Any data mutation happened (check, edit, delete...). Window persists + refreshes badges.</summary>
        public event Action DataChanged;
        /// <summary>Row asked for a context menu; window builds it (needs the tab list for move-to).</summary>
        public event Action<TodoTask, TodoTask> TaskContextMenuRequested;
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
            set
            {
                _locked = value;
                if (_locked) _selection.Clear();
                Rebuild();
            }
        }

        public void ShowTab(TodoTab tab)
        {
            if (_tab?.Id != tab?.Id)
            {
                _editingTaskId = null;
                _selection.Clear();
                _pendingScrollTaskId = null;
                _scrollApplyFrames = 0;
                _pendingScrollDistance = null;
                _scrollRestoreFrames = 0;
            }
            _tab = tab;
            Rebuild();
        }

        public IReadOnlyList<TodoTask> GetSelectedTasks()
        {
            if (_tab == null) return Array.Empty<TodoTask>();
            return _tab.Tasks
                .Where(task => _selection.IsSelected(task.Id))
                .OrderBy(task => task.Order)
                .ToList();
        }

        public void ClearSelection()
        {
            _selection.Clear();
            ApplySelectionToRows();
        }

        public void DeleteSubtask(TodoTask parent, TodoTask subtask)
        {
            if (parent?.Subtasks == null || !parent.Subtasks.Contains(subtask)) return;

            PreserveScrollDistance();
            parent.Subtasks.Remove(subtask);

            var nowUtc = DateTime.UtcNow;
            if (parent.HasSubtasks)
            {
                parent.SyncGroupAnchor(nowUtc);
            }
            else
            {
                parent.CurrentCount = 0;
                parent.LastCompletedUtc = null;
                parent.LastActivityUtc = null;
                parent.EnsureDurationAnchor(nowUtc);
            }

            AfterMutation();
        }

        public void BeginEdit(TodoTask task)
        {
            PreserveScrollDistance();
            _editingTaskId = task.Id;
            _newTaskId = null;
            Rebuild();
        }

        /// <summary>
        /// Registers a task the window just created (via the footer "+ Add task" button)
        /// as the one being edited, with placeholder-name treatment in its editor.
        /// The caller is responsible for adding the task to the active tab first.
        /// </summary>
        public void AddNewTask(TodoTask task)
        {
            _editingTaskId = task.Id;
            _newTaskId = task.Id;
            _pendingScrollTaskId = task.Id;
            _scrollApplyFrames = 5;
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
            _selectableTaskIds.Clear();
            _activeEditPanel = null;
            if (_tab == null) return;

            var nowUtc = DateTime.UtcNow;
            var orderedTasks = _tab.Tasks.OrderBy(t => t.Order).ToList();

            foreach (var task in orderedTasks)
            {
                if (_hideDone && task.IsDone && task.Id != _editingTaskId) continue;
                _selectableTaskIds.Add(task.Id);
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

            _selection.Retain(_selectableTaskIds);
            ApplySelectionToRows();
        }

        public override void UpdateContainer(GameTime gameTime)
        {
            base.UpdateContainer(gameTime);

            if (_pendingScrollDistance.HasValue && _scrollRestoreFrames > 0)
            {
                var scrollbar = GetScrollbar();
                if (scrollbar != null)
                    scrollbar.ScrollDistance = _pendingScrollDistance.Value;

                _scrollRestoreFrames--;
                if (_scrollRestoreFrames == 0) _pendingScrollDistance = null;
            }
            else if (_pendingScrollTaskId.HasValue && _scrollApplyFrames > 0)
            {
                _scrollApplyFrames--;
                ScrollPendingTaskIntoView();
                if (_scrollApplyFrames == 0) _pendingScrollTaskId = null;
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
                IsSelected = !isSubtask && _selection.IsSelected(task.Id),
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
                PreserveScrollDistance();
                if (!_expanded.Remove(task.Id)) _expanded.Add(task.Id);
                Rebuild();
            };
            row.SaveRequested += () =>
            {
                PreserveScrollDistance();
                _activeEditPanel?.Apply();
            };
            // Clicking the edit glyph on the task that's already open closes it again.
            row.EditRequested += () =>
            {
                if (_editingTaskId == task.Id)
                {
                    PreserveScrollDistance();
                    _editingTaskId = null;
                    _newTaskId = null;
                    Rebuild();
                }
                else BeginEdit(task);
            };
            row.SelectionRequested += (extendRange, toggle) =>
            {
                _selection.Select(task.Id, _selectableTaskIds, extendRange, toggle);
                ApplySelectionToRows();
            };
            row.ContextMenuRequested += () =>
            {
                if (isSubtask) _selection.Clear();
                else _selection.SelectForContext(task.Id);
                ApplySelectionToRows();
                TaskContextMenuRequested?.Invoke(task, parent);
            };
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
            edit.ContentHeightChanging += PreserveScrollDistance;
            edit.Saved += () => { _editingTaskId = null; _newTaskId = null; AfterMutation(); };
        }

        private void AfterMutation()
        {
            DataChanged?.Invoke();
            Rebuild();
        }

        private void ApplySelectionToRows()
        {
            foreach (var row in _rows)
                row.IsSelected = _selection.IsSelected(row.Task.Id);
        }

        private void PreserveScrollDistance()
        {
            var scrollbar = GetScrollbar();
            if (scrollbar == null) return;

            _pendingScrollDistance = scrollbar.ScrollDistance;
            _scrollRestoreFrames = 5;
        }

        private Scrollbar GetScrollbar()
        {
            _scrollbar = _scrollbar != null && _scrollbar.Parent != null
                ? _scrollbar
                : PanelScrollbarField?.GetValue(this) as Scrollbar;
            return _scrollbar;
        }

        private void ScrollPendingTaskIntoView()
        {
            if (!_pendingScrollTaskId.HasValue) return;
            var row = _rows.FirstOrDefault(candidate => candidate.Task.Id == _pendingScrollTaskId.Value);
            if (row == null) return;

            var target = _activeEditPanel != null && _editingTaskId == _pendingScrollTaskId
                ? (Control)_activeEditPanel
                : row;
            int viewportHeight = ContentRegion.Height;
            int contentHeight = Children
                .Where(child => child.Visible && !(child is Scrollbar))
                .Select(child => child.Bottom)
                .DefaultIfEmpty(viewportHeight)
                .Max();
            int scrollableRange = Math.Max(0, contentHeight - viewportHeight);
            if (scrollableRange == 0) return;

            var scrollbar = GetScrollbar();
            if (scrollbar == null) return;

            const int bottomPadding = 8;
            int targetOffset = Math.Max(0, Math.Min(
                target.Bottom - viewportHeight + bottomPadding,
                scrollableRange));
            scrollbar.ScrollDistance = (float)targetOffset / scrollableRange;
        }
    }
}
