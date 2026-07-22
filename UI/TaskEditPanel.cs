using System;
using System.Collections.Generic;
using System.Linq;
using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using Taskmaster.Models;
using Taskmaster.Services;
using Taskmaster.UI.Controls;

namespace Taskmaster.UI
{
    /// <summary>Inline task editor. Edits apply to the task on Save (checkmark); Cancel discards field edits.</summary>
    public class TaskEditPanel : Panel
    {
        public sealed class Draft
        {
            public string Name;
            public ResetScheduleType Schedule;
            public string LocalTime;
            public string Duration;
            public string Count;
            public string Clipboard;
            public string Notes;
            public string PendingSubtask;
            public List<TodoTask> Subtasks;
        }

        private static readonly Dictionary<ResetScheduleType, string> ScheduleNames =
            new Dictionary<ResetScheduleType, string>
            {
                { ResetScheduleType.Never, "Never resets" },
                { ResetScheduleType.DailyServer, "Daily server reset" },
                { ResetScheduleType.WeeklyServer, "Weekly server reset" },
                { ResetScheduleType.MapBonus, "Map bonus rewards reset" },
                { ResetScheduleType.WvwEu, "EU WvW reset" },
                { ResetScheduleType.WvwNa, "NA WvW reset" },
                { ResetScheduleType.Psna, "Pact Supply Network Agent" },
                { ResetScheduleType.LocalTime, "Local time" },
                { ResetScheduleType.Duration, "Duration" }
            };

        private readonly TodoTask _task;
        private readonly TaskmasterSizing _sizing;
        private readonly TextBox _nameBox;
        private readonly Dropdown _scheduleDropdown;
        private readonly TextBox _localTimeBox;    // "HH:mm" - shown only for the Local time schedule
        private readonly TextBox _durationBox;     // "hh:mm" or "d.hh:mm:ss" - shown only for Duration
        private readonly TextBox _countBox;
        private readonly TextBox _clipboardBox;
        private readonly TextBox _notesBox;
        private readonly Label _subtasksLabel;
        private readonly Panel _subtaskPanel;
        private readonly TextBox _newSubtaskBox;
        private readonly List<TodoTask> _workingSubtasks;

        // Rows between the Resets dropdown and the Subtasks label. Local time/Cooldown
        // are each relevant to exactly one schedule, so hiding the irrelevant one must
        // close the row entirely (not just blank its text) or a dead gap is left behind.
        private readonly List<(Label Label, Control Field, Func<bool> ShouldShow)> _dynamicRows =
            new List<(Label, Control, Func<bool>)>();
        private int _dynamicRowsStartY;

        public event Action Saved;
        public event Action ContentHeightChanging;
        public Guid TaskId => _task.Id;

        public TaskEditPanel(TodoTask task, bool isNew, TaskmasterSizing sizing, Draft draft = null)
        {
            _task = task;
            _sizing = sizing ?? new TaskmasterSizing(1f, 1f);
            _workingSubtasks = (draft?.Subtasks ?? task.Subtasks)
                .OrderBy(subtask => subtask.Order)
                .Select(CloneForEditing)
                .ToList();
            TaskOrdering.Normalize(_workingSubtasks);
            // Height is computed and set manually in LayoutTrailing() instead of
            // AutoSize - an AutoSize panel's height doesn't resolve synchronously
            // within the same call that just changed its children (bit us twice
            // already for the subtask textbox and the settings-panel rows), and it
            // gave us no way to reserve bottom padding after the last control anyway.
            BackgroundColor = new Color(26, 26, 31, 220);

            // Same top padding as the bottom padding reserved after the action buttons.
            int y = BottomPadding;
            // A freshly created task starts with its name empty and the placeholder
            // showing its fallback ("New task"), so the first keystroke replaces it
            // instead of requiring the user to clear it first.
            _nameBox = AddField("Name", ref y, draft?.Name ?? (isNew ? "" : task.Name));
            if (isNew) _nameBox.PlaceholderText = task.Name;

            AddLabel("Resets", y);
            _scheduleDropdown = new Dropdown
            {
                Parent = this,
                Left = FieldX,
                Top = y,
                Width = _sizing.Px(220),
                Height = _sizing.Px(27)
            };
            foreach (var name in ScheduleNames.Values) _scheduleDropdown.Items.Add(name);
            _scheduleDropdown.SelectedItem = ScheduleNames[draft?.Schedule ?? task.Schedule];
            y += RowH;
            _dynamicRowsStartY = y;

            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, TimeZoneInfo.Local);
            _localTimeBox = AddDynamicField("Local time",
                draft?.LocalTime ?? (task.LocalResetTime.HasValue
                    ? $"{task.LocalResetTime.Value.Hours:00}:{task.LocalResetTime.Value.Minutes:00}"
                    : $"{nowLocal.Hour:00}:{nowLocal.Minute:00}"),
                () => SelectedSchedule == ResetScheduleType.LocalTime);
            _durationBox = AddDynamicField("Cooldown",
                draft?.Duration ??
                (task.ResetDuration.HasValue ? task.ResetDuration.Value.ToString() : "1:00:00"),
                () => SelectedSchedule == ResetScheduleType.Duration);
            // A task with subtasks derives IsDone from its children and Apply() forces
            // TargetCount back to 1 regardless of what's typed here, so showing an
            // editable Count field alongside subtasks let you "set" a number that was
            // always going to be silently discarded on save.
            _countBox = AddDynamicField(
                "Count",
                draft?.Count ?? task.TargetCount.ToString(),
                () => _workingSubtasks.Count == 0);
            _clipboardBox = AddDynamicField("Clipboard", draft?.Clipboard ?? task.ClipboardContent ?? "");
            _notesBox = AddDynamicField("Notes", draft?.Notes ?? task.Notes ?? "");

            _subtasksLabel = AddLabel("Subtasks", y);
            _subtaskPanel = new Panel
            {
                Parent = this, Left = FieldX, Width = _sizing.Px(300),
                HeightSizingMode = SizingMode.AutoSize
            };
            _newSubtaskBox = new TextBox
            {
                Parent = this,
                Left = FieldX,
                Width = _sizing.Px(220),
                Height = _sizing.Px(24),
                Font = _sizing.BodyFont,
                PlaceholderText = "add subtask, press Enter",
                Text = draft?.PendingSubtask ?? ""
            };
            _newSubtaskBox.EnterPressed += (s, e) =>
            {
                if (string.IsNullOrWhiteSpace(_newSubtaskBox.Text)) return;
                ContentHeightChanging?.Invoke();
                _workingSubtasks.Add(new TodoTask
                {
                    Name = _newSubtaskBox.Text.Trim(),
                    Order = _workingSubtasks.Count,
                    Schedule = _task.Schedule
                });
                _newSubtaskBox.Text = "";
                RebuildSubtaskList();
            };

            _scheduleDropdown.ValueChanged += (s, e) => UpdateConditionalVisibility();

            RebuildSubtaskList();

            if (isNew) _nameBox.Focused = true;
        }

        public Draft CaptureDraft() =>
            new Draft
            {
                Name = _nameBox.Text,
                Schedule = SelectedSchedule,
                LocalTime = _localTimeBox.Text,
                Duration = _durationBox.Text,
                Count = _countBox.Text,
                Clipboard = _clipboardBox.Text,
                Notes = _notesBox.Text,
                PendingSubtask = _newSubtaskBox.Text,
                Subtasks = _workingSubtasks.Select(CloneForEditing).ToList()
            };

        private int LabelWidth => _sizing.Px(88);
        private int RowH => _sizing.Px(30);
        private int FieldX => LabelWidth + _sizing.Px(12);
        private int SubtaskRowHeight => _sizing.Px(28);
        private int BottomPadding => _sizing.Px(16);

        private void LayoutTrailing()
        {
            // Computed directly from the subtask count rather than read from
            // _subtaskPanel.Bottom - an AutoSize panel's Height doesn't update
            // synchronously within the same call that just added its children, so
            // reading it here returned the PREVIOUS height and the textbox below
            // ended up overlapping the last subtask row.
            int subtaskAreaHeight = _workingSubtasks.Count * SubtaskRowHeight;
            int y = _subtaskPanel.Top + subtaskAreaHeight + _sizing.Px(4);
            _newSubtaskBox.Top = y;

            int newHeight = _newSubtaskBox.Bottom + BottomPadding;
            if (Height != newHeight) Height = newHeight;
        }

        private Label AddLabel(string text, int y)
        {
            return new Label
            {
                Parent = this,
                Left = _sizing.Px(8),
                Top = y + _sizing.Px(4),
                Width = LabelWidth,
                Height = _sizing.Px(20),
                Text = text,
                TextColor = TaskmasterTheme.MutedCream,
                Font = _sizing.BodyFont
            };
        }

        private TextBox AddField(string label, ref int y, string value)
        {
            AddLabel(label, y);
            var box = new TextBox
            {
                Parent = this,
                Left = FieldX,
                Top = y,
                Width = _sizing.Px(300),
                Height = _sizing.Px(24),
                Text = value,
                Font = _sizing.BodyFont
            };
            y += RowH;
            return box;
        }

        /// <summary>Adds a label+box pair to the dynamic zone below the Resets dropdown.
        /// Top/visibility are assigned later by UpdateConditionalVisibility, not here -
        /// rows whose shouldShow returns false are hidden and skip their row entirely so
        /// following rows close the gap instead of leaving it blank.</summary>
        private TextBox AddDynamicField(string label, string value, Func<bool> shouldShow = null)
        {
            var lbl = new Label
            {
                Parent = this,
                Left = _sizing.Px(8),
                Width = LabelWidth,
                Height = _sizing.Px(20),
                Text = label,
                TextColor = TaskmasterTheme.MutedCream,
                Font = _sizing.BodyFont
            };
            var box = new TextBox
            {
                Parent = this,
                Left = FieldX,
                Width = _sizing.Px(300),
                Height = _sizing.Px(24),
                Text = value,
                Font = _sizing.BodyFont
            };
            _dynamicRows.Add((lbl, box, shouldShow));
            return box;
        }

        private ResetScheduleType SelectedSchedule =>
            ScheduleNames.First(kv => kv.Value == _scheduleDropdown.SelectedItem).Key;

        private void UpdateConditionalVisibility()
        {
            int y = _dynamicRowsStartY;
            foreach (var row in _dynamicRows)
            {
                bool show = row.ShouldShow?.Invoke() ?? true;
                row.Label.Visible = show;
                row.Field.Visible = show;
                if (show)
                {
                    row.Label.Top = y + _sizing.Px(4);
                    row.Field.Top = y;
                    y += RowH;
                }
            }

            _subtasksLabel.Top = y + 4;
            _subtaskPanel.Top = y;

            LayoutTrailing();
            Invalidate();
        }

        private void RebuildSubtaskList()
        {
            foreach (var c in _subtaskPanel.Children.ToList()) c.Dispose();
            int y = 0;
            foreach (var sub in _workingSubtasks.ToList())
            {
                var nameBox = new TextBox
                {
                    Parent = _subtaskPanel,
                    Left = 0,
                    Top = y,
                    Width = _sizing.Px(118),
                    Height = _sizing.Px(26),
                    Text = sub.Name,
                    Font = _sizing.BodyFont,
                    PlaceholderText = "Subtask name"
                };
                var clipboardBox = new TextBox
                {
                    Parent = _subtaskPanel,
                    Left = _sizing.Px(122),
                    Top = y,
                    Width = _sizing.Px(126),
                    Height = _sizing.Px(26),
                    Text = sub.ClipboardContent ?? "",
                    Font = _sizing.BodyFont,
                    PlaceholderText = "waypoint / chat code"
                };
                var remove = new IconButton(TaskmasterIcons.Cancel, TaskmasterTheme.IconGlyph)
                {
                    Parent = _subtaskPanel,
                    Left = _sizing.Px(252),
                    Top = y,
                    Width = _sizing.Px(26),
                    Height = _sizing.Px(26),
                    GlyphSize = _sizing.Px(18)
                };
                var captured = sub;
                nameBox.TextChanged += (s, e) => captured.Name = nameBox.Text;
                clipboardBox.TextChanged += (s, e) => captured.ClipboardContent = clipboardBox.Text;
                remove.Click += (s, e) =>
                {
                    ContentHeightChanging?.Invoke();
                    _workingSubtasks.Remove(captured);
                    RebuildSubtaskList();
                };
                y += SubtaskRowHeight;
            }
            UpdateConditionalVisibility();
        }

        /// <summary>Commits the field values to the task. Called by the row's checkmark
        /// button - this panel no longer has its own Save control.</summary>
        public void Apply()
        {
            _task.Name = string.IsNullOrWhiteSpace(_nameBox.Text) ? _task.Name : _nameBox.Text.Trim();
            _task.Schedule = SelectedSchedule;

            if (_task.Schedule == ResetScheduleType.LocalTime &&
                TimeSpan.TryParse(_localTimeBox.Text, out var localAt) &&
                localAt >= TimeSpan.Zero && localAt < TimeSpan.FromDays(1))
                _task.LocalResetTime = localAt;

            if (_task.Schedule == ResetScheduleType.Duration &&
                TimeSpan.TryParse(_durationBox.Text, out var dur) && dur > TimeSpan.Zero)
                _task.ResetDuration = dur;
            _task.EnsureDurationAnchor(DateTime.UtcNow);

            if (int.TryParse(_countBox.Text, out var count) && count >= 1 && count <= 999)
                _task.TargetCount = count;

            _task.ClipboardContent = string.IsNullOrWhiteSpace(_clipboardBox.Text) ? null : _clipboardBox.Text;
            _task.Notes = string.IsNullOrWhiteSpace(_notesBox.Text) ? null : _notesBox.Text;

            _task.Subtasks = _workingSubtasks
                .Where(subtask => !string.IsNullOrWhiteSpace(subtask.Name))
                .ToList();
            TaskOrdering.Normalize(_task.Subtasks);
            foreach (var s in _task.Subtasks)
            {
                s.Name = s.Name.Trim();
                s.ClipboardContent = string.IsNullOrWhiteSpace(s.ClipboardContent)
                    ? null
                    : s.ClipboardContent.Trim();
                // The whole group shares one schedule - a subtask keeping its own stale
                // LocalResetTime/ResetDuration would make its individual reset sweep
                // (ResetEngine.ApplyResetRecursive) fire on its own timing instead of the
                // group's, which is exactly the "subtasks don't reflect the main task"
                // drift this schedule field belongs to.
                s.Schedule = _task.Schedule;
                s.LocalResetTime = _task.LocalResetTime;
                s.ResetDuration = _task.ResetDuration;
            }
            if (_task.HasSubtasks) { _task.TargetCount = 1; _task.CurrentCount = 0; }
            if (_task.CurrentCount > _task.TargetCount) _task.CurrentCount = _task.TargetCount;
            _task.SyncGroupAnchor(DateTime.UtcNow);

            Saved?.Invoke();
        }

        private static TodoTask CloneForEditing(TodoTask source)
        {
            var clone = new TodoTask
            {
                Id = source.Id,
                Name = source.Name,
                Order = source.Order,
                Schedule = source.Schedule,
                LocalResetTime = source.LocalResetTime,
                ResetDuration = source.ResetDuration,
                ClipboardContent = source.ClipboardContent,
                Notes = source.Notes,
                TargetCount = source.TargetCount,
                CurrentCount = source.CurrentCount,
                LastCompletedUtc = source.LastCompletedUtc,
                LastActivityUtc = source.LastActivityUtc
            };
            clone.Subtasks = source.Subtasks?.Select(CloneForEditing).ToList() ?? new List<TodoTask>();
            return clone;
        }
    }
}
