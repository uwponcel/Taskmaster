using System;
using System.Linq;
using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using Taskmaster.Models;
using Taskmaster.Services;
using Taskmaster.Settings;
using Taskmaster.UI.Controls;

namespace Taskmaster.UI
{
    public class TaskmasterWindow : StandardWindow
    {
        private const int WindowWidth = 550;
        private const int WindowHeight = 560;
        private const int MinWindowWidth = 550;
        private const int ListTop = 66;

        private readonly TaskStore _store;
        private readonly ModuleSettings _settings;
        private readonly TabStrip _tabStrip;
        private readonly TaskListPanel _listPanel;
        private readonly IconButton _hideDoneBtn;
        private readonly IconButton _lockBtn;
        private readonly StandardButton _addTaskBtn;
        private readonly Label _emptyLabel;
        private TextBox _renameBox;
        private Guid? _activeTabId;
        // WindowBase2's base ctor sets Size, which synchronously fires OnResized before
        // this subclass's own ctor body (and therefore the fields above) has run - guard
        // against that half-constructed callback, same lesson as Maestro's PracticeWindow.
        private bool _isConstructed;

        public TaskmasterWindow(TaskStore store, ModuleSettings settings)
            : base(TaskmasterTheme.CreateWindowBackground(WindowWidth, WindowHeight),
                   new Rectangle(0, 0, WindowWidth, WindowHeight),
                   new Rectangle(10, 30, WindowWidth - 20, WindowHeight - 40))
        {
            _store = store;
            _settings = settings;

            Parent = GameService.Graphics.SpriteScreen;
            Title = "Taskmaster";
            Emblem = Module.Instance.ContentsManager.GetTexture("taskmaster-emblem.png");
            Id = "Taskmaster_MainWindow";
            SavesPosition = true;
            SavesSize = true;
            CanResize = true;
            CanCloseWithEscape = false;

            _hideDoneBtn = new IconButton(TaskmasterIcons.Eye, TaskmasterTheme.IconGlyph)
            {
                Parent = this, Width = 30, Height = 26,
                Selected = _settings.HideDone.Value,
                BasicTooltipText = "Hide completed tasks and fully completed tabs"
            };
            _lockBtn = new IconButton(TaskmasterIcons.Lock, TaskmasterTheme.IconGlyph)
            {
                Parent = this, Width = 30, Height = 26,
                Selected = _settings.LockTasks.Value,
                BasicTooltipText = "Lock tasks (checking still works)"
            };

            // Top-left corner, opposite the eye/lock icons - a persistent action for the
            // active tab rather than a scrollable list item, so it's never pushed off
            // by task rows and never affected by the tab strip's own horizontal scroll.
            _addTaskBtn = new StandardButton
            {
                Parent = this,
                Location = new Point(0, 0),
                Height = 26,
                Text = "+  Add task"
            };
            _addTaskBtn.Width = (int)GameService.Content.DefaultFont14.MeasureString(_addTaskBtn.Text).Width + 24;
            UpdateAddTaskButtonState();

            _tabStrip = new TabStrip { Parent = this, Locked = _settings.LockTasks.Value };

            _listPanel = new TaskListPanel
            {
                Parent = this,
                HideDone = _settings.HideDone.Value,
                Locked = _settings.LockTasks.Value
            };

            _emptyLabel = new Label
            {
                Parent = this,
                Height = 28,
                Font = GameService.Content.DefaultFont16,
                Text = "Create your first tab with the + in the top right corner",
                TextColor = TaskmasterTheme.DimText,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Middle,
                Visible = false
            };

            RelayoutChildren();
            WireEvents();
            SelectInitialTab();
            RefreshAll();
            _isConstructed = true;
        }

        /// <summary>Repositions/resizes children to fill the current content region - called
        /// once at construction and again whenever the user resizes the window.</summary>
        private void RelayoutChildren()
        {
            _hideDoneBtn.Location = new Point(ContentRegion.Width - 66, 0);
            _lockBtn.Location = new Point(ContentRegion.Width - 32, 0);

            _tabStrip.Location = new Point(0, 30);
            _tabStrip.Width = ContentRegion.Width;

            _listPanel.Location = new Point(0, ListTop);
            _listPanel.Width = ContentRegion.Width;
            _listPanel.Height = Math.Max(0, ContentRegion.Height - ListTop - 4);
            // Existing rows/buttons were built with a fixed Width at the time they were
            // added to the FlowPanel - resizing the panel itself doesn't resize them
            // retroactively, so rebuild to pick up the new width immediately instead of
            // waiting for the next tab switch (which rebuilds anyway).
            _listPanel.Rebuild();

            _emptyLabel.Width = ContentRegion.Width;
            _emptyLabel.Location = new Point(0, ListTop + _listPanel.Height / 2 - 14);
        }

        protected override void OnResized(ResizedEventArgs e)
        {
            base.OnResized(e);
            if (!_isConstructed) return;
            if (Width < MinWindowWidth)
            {
                // Reassigning Size re-triggers OnResized with the corrected width -
                // it'll pass this check on the next call and fall through below.
                Size = new Point(MinWindowWidth, Height);
                return;
            }
            RelayoutChildren();
        }

        private void WireEvents()
        {
            // Apply immediately instead of waiting for the next mouse enter/leave -
            // skip it while the cursor is actively over the window so it doesn't
            // fight the OnMouseEntered override's forced Opacity = 1f.
            _settings.UnfocusedOpacity.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(_settings.UnfocusedOpacity.Value) && !MouseOver)
                    Opacity = _settings.UnfocusedOpacity.Value;
            };

            _hideDoneBtn.Click += (s, e) =>
            {
                _settings.HideDone.Value = !_settings.HideDone.Value;
                _hideDoneBtn.Selected = _settings.HideDone.Value;
                _listPanel.HideDone = _settings.HideDone.Value;
                // Tab-level hiding (ShouldHideTab) is only recomputed inside RefreshAll -
                // without this, a fully-completed tab wouldn't disappear from the strip
                // until the next unrelated event happened to call RefreshAll (e.g.
                // switching tabs), so toggling this button looked like it did nothing.
                RefreshAll();
            };

            _lockBtn.Click += (s, e) =>
            {
                _settings.LockTasks.Value = !_settings.LockTasks.Value;
                _lockBtn.Selected = _settings.LockTasks.Value;
                _listPanel.Locked = _settings.LockTasks.Value;
                _tabStrip.Locked = _settings.LockTasks.Value;
                UpdateAddTaskButtonState();
            };

            _addTaskBtn.Click += (s, e) => AddTaskToActiveTab();

            _tabStrip.TabClicked += tab => { _activeTabId = tab.Id; RefreshAll(); };
            _tabStrip.AddClicked += AddTab;
            _tabStrip.TabRightClicked += ShowTabMenu;
            _tabStrip.TabReordered += (tab, newIndex) =>
            {
                _store.Tabs.Remove(tab);
                _store.Tabs.Insert(Math.Min(newIndex, _store.Tabs.Count), tab);
                for (int i = 0; i < _store.Tabs.Count; i++) _store.Tabs[i].Order = i;
                MarkDirtyAndRefresh();
            };

            _listPanel.DataChanged += MarkDirtyAndRefresh;
            _listPanel.TaskContextMenuRequested += ShowTaskMenu;
            _listPanel.CopyToClipboardRequested += task =>
                ClipboardUtil.WindowsClipboardService.SetTextAsync(task.ClipboardContent);
        }

        /// <summary>A fully-completed tab (all tasks done, at least one task) is hidden
        /// alongside individual completed tasks when HideDone is on - an empty tab is
        /// never hidden this way, only one whose tasks are all checked off.</summary>
        private bool ShouldHideTab(TodoTab tab) =>
            _settings.HideDone.Value && tab.TotalCount > 0 && tab.DoneCount == tab.TotalCount;

        private TodoTab ActiveTab
        {
            get
            {
                var visible = _store.Tabs.Where(t => !ShouldHideTab(t)).OrderBy(t => t.Order).ToList();
                var match = visible.FirstOrDefault(t => t.Id == _activeTabId);
                if (match != null) return match;

                // The active tab just got hidden (its last task was completed, or
                // hide-done was toggled on) - prefer the next visible tab after where
                // it was, or the previous one if it was last, same convention as
                // deleting the active tab, before falling back to the first visible tab.
                var wasActive = _store.Tabs.FirstOrDefault(t => t.Id == _activeTabId);
                if (wasActive != null)
                {
                    var allOrdered = _store.Tabs.OrderBy(t => t.Order).ToList();
                    int idx = allOrdered.IndexOf(wasActive);
                    for (int i = idx + 1; i < allOrdered.Count; i++)
                        if (!ShouldHideTab(allOrdered[i])) return allOrdered[i];
                    for (int i = idx - 1; i >= 0; i--)
                        if (!ShouldHideTab(allOrdered[i])) return allOrdered[i];
                }
                return visible.FirstOrDefault();
            }
        }

        private void SelectInitialTab()
        {
            _activeTabId = _store.Tabs.OrderBy(t => t.Order).FirstOrDefault()?.Id;
        }

        private void MarkDirtyAndRefresh()
        {
            _store.MarkDirty(DateTime.UtcNow);
            RefreshAll();
        }

        private void RefreshAll()
        {
            var visibleTabs = _store.Tabs.Where(t => !ShouldHideTab(t)).OrderBy(t => t.Order).ToList();
            _tabStrip.SetTabs(visibleTabs);
            var active = ActiveTab;
            _activeTabId = active?.Id;
            _tabStrip.ActiveTabId = _activeTabId;
            _listPanel.ShowTab(active);
            _emptyLabel.Visible = visibleTabs.Count == 0;
            _emptyLabel.Text = _store.Tabs.Count == 0
                ? "Create your first tab with the + in the top right corner"
                : "All tabs are complete - toggle the eye to show them";
            _listPanel.Visible = active != null;
        }

        /// <summary>Called by Module once per minute and right after Show(): reset pass + countdowns.</summary>
        public void OnMinuteTick(DateTime nowUtc)
        {
            int applied = ResetEngine.ApplyResets(_store.Tabs, nowUtc);
            if (applied > 0)
            {
                _store.MarkDirty(nowUtc);
                RefreshAll();
            }
            else
            {
                _listPanel.RefreshCountdowns(nowUtc);
                _tabStrip.Invalidate();
            }
        }

        private void AddTab()
        {
            if (_settings.LockTasks.Value) return;
            var tab = new TodoTab { Name = "New tab", Order = _store.Tabs.Count };
            _store.Tabs.Add(tab);
            _activeTabId = tab.Id;
            MarkDirtyAndRefresh();
            BeginRenameTab(tab, isNew: true);
        }

        private void UpdateAddTaskButtonState()
        {
            bool locked = _settings.LockTasks.Value;
            _addTaskBtn.Enabled = !locked;
            _addTaskBtn.BasicTooltipText = locked ? "Locked - unlock to add tasks" : null;
        }

        private void AddTaskToActiveTab()
        {
            if (_settings.LockTasks.Value) return;
            var tab = ActiveTab;
            if (tab == null)
            {
                ScreenNotification.ShowNotification("Please add a tab first",
                    ScreenNotification.NotificationType.Info);
                return;
            }
            var t = new TodoTask { Name = "New task", Order = tab.Tasks.Count };
            tab.Tasks.Add(t);
            _listPanel.AddNewTask(t);
            MarkDirtyAndRefresh();
        }

        private void BeginRenameTab(TodoTab tab, bool isNew = false)
        {
            _renameBox?.Dispose();
            // Both the rename box and the add-task button live in this same top-left
            // corner (away from the tab strip and task list) - hide the button while
            // renaming so the two never overlap.
            _addTaskBtn.Visible = false;
            _renameBox = new TextBox
            {
                Parent = this,
                Location = new Point(0, 0),
                Width = 220,
                Height = 26,
                // A brand new tab starts empty with its default name as a placeholder,
                // so the first keystroke doesn't need to clear "New tab" first.
                Text = isNew ? "" : tab.Name,
                PlaceholderText = isNew ? tab.Name : null
            };
            void Commit()
            {
                if (_renameBox == null) return;
                if (!string.IsNullOrWhiteSpace(_renameBox.Text)) tab.Name = _renameBox.Text.Trim();
                _renameBox.Dispose();
                _renameBox = null;
                _addTaskBtn.Visible = true;
                UpdateAddTaskButtonState();
                MarkDirtyAndRefresh();
            }
            _renameBox.EnterPressed += (s, e) => Commit();
            _renameBox.InputFocusChanged += (s, e) => { if (!_renameBox.Focused) Commit(); };
            _renameBox.Focused = true;
        }

        private void ShowTabMenu(TodoTab tab)
        {
            var menu = new ContextMenuStrip();

            var rename = menu.AddMenuItem("Rename");
            rename.Click += (s, e) => BeginRenameTab(tab);

            var changeColor = menu.AddMenuItem("Change color");
            var colorMenu = new ContextMenuStrip();
            foreach (var preset in TaskmasterTheme.TabAccentPresets)
            {
                var hex = TaskmasterTheme.ToHex(preset.Value);
                var colorItem = colorMenu.AddMenuItem(preset.Name);
                colorItem.CanCheck = true;
                colorItem.Checked = tab.AccentColorHex == hex;
                colorItem.Click += (s, e) =>
                {
                    tab.AccentColorHex = hex;
                    MarkDirtyAndRefresh();
                };
            }
            var defaultColorItem = colorMenu.AddMenuItem("Default");
            defaultColorItem.CanCheck = true;
            defaultColorItem.Checked = string.IsNullOrEmpty(tab.AccentColorHex);
            defaultColorItem.Click += (s, e) =>
            {
                tab.AccentColorHex = null;
                MarkDirtyAndRefresh();
            };
            changeColor.Submenu = colorMenu;

            var export = menu.AddMenuItem("Export to clipboard");
            export.Click += (s, e) =>
            {
                ClipboardUtil.WindowsClipboardService.SetTextAsync(TabShare.Export(tab));
                ScreenNotification.ShowNotification("Tab copied to clipboard");
            };

            var import = menu.AddMenuItem("Import tab from clipboard");
            import.Click += async (s, e) =>
            {
                var text = await ClipboardUtil.WindowsClipboardService.GetTextAsync();
                var result = TabShare.TryImport(text);
                switch (result.Outcome)
                {
                    case TabShareImportOutcome.Success:
                        result.Tab.Order = _store.Tabs.Count;
                        _store.Tabs.Add(result.Tab);
                        _activeTabId = result.Tab.Id;
                        MarkDirtyAndRefresh();
                        ScreenNotification.ShowNotification($"Imported tab \"{result.Tab.Name}\"");
                        break;
                    case TabShareImportOutcome.VersionTooNew:
                        ScreenNotification.ShowNotification("Tab export is from a newer Taskmaster version",
                            ScreenNotification.NotificationType.Error);
                        break;
                    default:
                        ScreenNotification.ShowNotification("Not a Taskmaster tab export",
                            ScreenNotification.NotificationType.Error);
                        break;
                }
            };

            var delete = menu.AddMenuItem("Delete tab");
            delete.Click += (s, e) =>
            {
                var confirm = new ContextMenuStrip();
                var yes = confirm.AddMenuItem($"Really delete \"{tab.Name}\" and its {tab.TotalCount} task(s)?");
                yes.Click += (s2, e2) =>
                {
                    if (_activeTabId == tab.Id)
                    {
                        // Select the tab that follows the deleted one (or the one before,
                        // if it was last) instead of always jumping back to the first tab.
                        var ordered = _store.Tabs.OrderBy(t => t.Order).ToList();
                        int idx = ordered.IndexOf(tab);
                        var next = idx + 1 < ordered.Count ? ordered[idx + 1]
                            : idx > 0 ? ordered[idx - 1]
                            : null;
                        _activeTabId = next?.Id;
                    }
                    _store.Tabs.Remove(tab);
                    MarkDirtyAndRefresh();
                };
                confirm.Show(GameService.Input.Mouse.Position);
            };

            var deleteOthers = menu.AddMenuItem("Delete other tabs");
            deleteOthers.Click += (s, e) =>
            {
                int othersCount = _store.Tabs.Count - 1;
                if (othersCount <= 0) return;
                var confirm = new ContextMenuStrip();
                var yes = confirm.AddMenuItem($"Really delete the other {othersCount} tab(s)?");
                yes.Click += (s2, e2) =>
                {
                    _store.Tabs.RemoveAll(t => t.Id != tab.Id);
                    _activeTabId = tab.Id;
                    MarkDirtyAndRefresh();
                };
                confirm.Show(GameService.Input.Mouse.Position);
            };

            menu.Show(GameService.Input.Mouse.Position);
        }

        private void ShowTaskMenu(TodoTask task)
        {
            var tab = ActiveTab;
            if (tab == null) return;
            var menu = new ContextMenuStrip();

            var edit = menu.AddMenuItem("Edit");
            edit.Click += (s, e) => _listPanel.BeginEdit(task);

            var duplicate = menu.AddMenuItem("Duplicate");
            duplicate.Click += (s, e) =>
            {
                var json = TabShare.Export(new TodoTab { Name = "x", Tasks = { task } });
                var copy = TabShare.TryImport(json).Tab.Tasks[0];
                copy.Name = task.Name + " (copy)";
                copy.Order = task.Order + 1;
                tab.Tasks.Insert(Math.Min(tab.Tasks.IndexOf(task) + 1, tab.Tasks.Count), copy);
                MarkDirtyAndRefresh();
            };

            var moveUp = menu.AddMenuItem("Move up");
            moveUp.Click += (s, e) => MoveTask(tab, task, -1);
            var moveDown = menu.AddMenuItem("Move down");
            moveDown.Click += (s, e) => MoveTask(tab, task, +1);

            foreach (var other in _store.Tabs.Where(t => t.Id != tab.Id))
            {
                var captured = other;
                var move = menu.AddMenuItem($"Move to \"{other.Name}\"");
                move.Click += (s, e) =>
                {
                    tab.Tasks.Remove(task);
                    task.Order = captured.Tasks.Count;
                    captured.Tasks.Add(task);
                    MarkDirtyAndRefresh();
                };
            }

            var delete = menu.AddMenuItem("Delete");
            delete.Click += (s, e) =>
            {
                tab.Tasks.Remove(task);
                MarkDirtyAndRefresh();
            };

            menu.Show(GameService.Input.Mouse.Position);
        }

        private void MoveTask(TodoTab tab, TodoTask task, int delta)
        {
            var ordered = tab.Tasks.OrderBy(t => t.Order).ToList();
            int i = ordered.IndexOf(task);
            int j = i + delta;
            if (i < 0 || j < 0 || j >= ordered.Count) return;
            var tmp = ordered[i]; ordered[i] = ordered[j]; ordered[j] = tmp;
            for (int k = 0; k < ordered.Count; k++) ordered[k].Order = k;
            MarkDirtyAndRefresh();
        }

        protected override void OnMouseEntered(Blish_HUD.Input.MouseEventArgs e)
        {
            base.OnMouseEntered(e);
            Opacity = 1f;
        }

        protected override void OnMouseLeft(Blish_HUD.Input.MouseEventArgs e)
        {
            base.OnMouseLeft(e);
            Opacity = _settings.UnfocusedOpacity.Value;
        }

        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            OnMinuteTick(DateTime.UtcNow);
        }

        protected override void DisposeControl()
        {
            _renameBox?.Dispose();
            base.DisposeControl();
        }
    }
}
