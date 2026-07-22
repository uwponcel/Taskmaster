using System;
using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Taskmaster.Models;
using Taskmaster.Services;
using Taskmaster.UI.Controls;

namespace Taskmaster.UI
{
    public class TaskRow : Control
    {
        private static readonly TimeSpan DueSoonThreshold = TimeSpan.FromHours(1);

        private readonly TodoTask _task;
        private readonly bool _isSubtask;
        private readonly TaskmasterSizing _sizing;
        private string _countdownText = "";
        private bool _dueSoon;
        private bool _hover;
        private bool _isSelected;
        private bool _isDropTarget;
        private DateTime _copiedFlashUntilUtc;

        private Rectangle _chipBounds;
        private Rectangle _clipboardBounds;
        private Rectangle _noteBounds;
        private Rectangle _pencilBounds;
        private Rectangle _saveBounds;

        public TodoTask Task => _task;
        public TodoTask ParentTask { get; set; }
        public bool IsExpanded { get; set; }
        public bool Locked { get; set; }
        public bool DragReorderingEnabled { get; set; }
        public bool DropAfter { get; set; }
        public bool IsDropTarget
        {
            get => _isDropTarget;
            set
            {
                if (_isDropTarget == value) return;
                _isDropTarget = value;
                Invalidate();
            }
        }
        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected == value) return;
                _isSelected = value;
                Invalidate();
            }
        }
        /// <summary>True while this task's inline editor is open below the row.</summary>
        public bool IsEditing { get; set; }
        /// <summary>For a subtask row, the owning parent task - the countdown reflects
        /// the parent's shared anchor instead of this subtask's own, since a group of
        /// subtasks shares one reset, not one each. Null for top-level rows (own anchor).</summary>
        public TodoTask CountdownAnchor { get; set; }

        public event Action ToggleRequested;        // left-click checkbox / counter chip - toggles check/uncheck
        public event Action EditRequested;          // pencil/close click - opens or closes the inline editor
        public event Action SaveRequested;          // checkmark click (editing only) - applies the open editor's fields
        public event Action ContextMenuRequested;   // right-click anywhere on the row
        public event Action ExpandToggled;          // chevron click (parents only)
        public event Action CopyRequested;          // clipboard icon click
        public event Action<bool, bool> SelectionRequested; // Shift extends a range, Ctrl toggles one row
        public event Action<bool> DragCandidateRequested;

        public TaskRow(TodoTask task, bool isSubtask, TaskmasterSizing sizing)
        {
            _task = task;
            _isSubtask = isSubtask;
            _sizing = sizing ?? new TaskmasterSizing(1f, 1f);
            Height = RowHeight;
        }

        private int RowHeight => _sizing.Px(34);
        private int SubtaskIndent => _sizing.Px(22);
        private int CheckboxSize => _sizing.Px(18);
        private int InlineIconHitSize => _sizing.Px(26);
        private int InlineIconSize => _sizing.Px(18);
        private int ChevronHitSize => _sizing.Px(26);
        private int ChevronIconSize => _sizing.Px(19);
        private int EditIconSize => _sizing.Px(24);
        private int EditActionIconSize => _sizing.Px(28);
        private int Pad => _sizing.Px(6);
        private int LeftOffset => Pad + (_isSubtask ? SubtaskIndent : 0);

        // The list panel's Scrollbar (Blish HUD Controls.Scrollbar, 12px wide) overlaps
        // the last ~18px of ContentRegion.Width rather than shrinking it, so anything
        // hit-tested flush against Width would have its clicks swallowed by the
        // scrollbar whenever the list is tall enough to need one. Keep all right-side
        // interactive elements clear of that zone.
        private int ScrollbarMargin => _sizing.Px(20);

        // Right side, not left - so the checkbox/name column starts at the same X on
        // every row whether or not that particular task has subtasks.
        private Rectangle ChevronBounds =>
            new Rectangle(
                Width - ScrollbarMargin - ChevronHitSize,
                (Height - ChevronHitSize) / 2,
                ChevronHitSize,
                ChevronHitSize);

        private Rectangle ChevronGlyphBounds
        {
            get
            {
                var hitBounds = ChevronBounds;
                return new Rectangle(
                    hitBounds.X + (hitBounds.Width - ChevronIconSize) / 2,
                    hitBounds.Y + (hitBounds.Height - ChevronIconSize) / 2,
                    ChevronIconSize,
                    ChevronIconSize);
            }
        }

        private Rectangle CheckboxBounds =>
            new Rectangle(LeftOffset, (Height - CheckboxSize) / 2, CheckboxSize, CheckboxSize);

        private bool HasClipboard => !string.IsNullOrEmpty(_task.ClipboardContent);
        private bool HasCounter => _task.TargetCount > 1 && !_task.HasSubtasks;
        private bool InChipZone(Point p) => HasCounter && _chipBounds.Contains(p);

        /// <summary>Called by the list panel once per minute (and after any mutation).</summary>
        public void RefreshDisplay(DateTime nowUtc)
        {
            var anchor = CountdownAnchor ?? _task;
            var next = ResetEngine.NextBoundary(anchor, nowUtc);
            _countdownText = next.HasValue ? FormatCountdown(next.Value - nowUtc) : "";
            _dueSoon = next.HasValue && !_task.IsDone && next.Value - nowUtc < DueSoonThreshold
                       && anchor.Schedule != ResetScheduleType.Never;
            BasicTooltipText = string.IsNullOrEmpty(_task.Notes) ? null : _task.Notes;
            Invalidate();
        }

        public static string FormatCountdown(TimeSpan t)
        {
            if (t < TimeSpan.Zero) t = TimeSpan.Zero;
            if (t.TotalDays >= 1) return $"{(int)t.TotalDays}d {t.Hours}h";
            if (t.TotalHours >= 1) return $"{(int)t.TotalHours}h {t.Minutes:00}m";
            return $"{t.Minutes}m";
        }

        public void FlashCopied()
        {
            _copiedFlashUntilUtc = DateTime.UtcNow.AddSeconds(1.5);
            UpdateTooltip();
            Invalidate();
        }

        protected override void OnMouseMoved(MouseEventArgs e)
        {
            base.OnMouseMoved(e);
            _hover = true;
            UpdateTooltip();
            Invalidate();
        }

        private void UpdateTooltip()
        {
            // The row's tooltip otherwise shows the task's notes. Over the copy icon,
            // show temporary confirmation after copying, then restore the normal hint.
            BasicTooltipText = HasClipboard && _clipboardBounds.Contains(RelativeMousePosition)
                ? DateTime.UtcNow < _copiedFlashUntilUtc
                    ? "Copied!"
                    : "Click to copy to clipboard"
                : (string.IsNullOrEmpty(_task.Notes) ? null : _task.Notes);
        }

        protected override void OnMouseLeft(MouseEventArgs e) { base.OnMouseLeft(e); _hover = false; Invalidate(); }

        protected override void OnLeftMouseButtonPressed(MouseEventArgs e)
        {
            base.OnLeftMouseButtonPressed(e);
            var p = RelativeMousePosition;

            if (!_isSubtask && _task.HasSubtasks && ChevronBounds.Contains(p)) { ExpandToggled?.Invoke(); return; }
            if (CheckboxBounds.Contains(p) || InChipZone(p)) { ToggleRequested?.Invoke(); return; }
            if (!Locked && _saveBounds != Rectangle.Empty && _saveBounds.Contains(p)) { SaveRequested?.Invoke(); return; }
            if (!Locked && _pencilBounds != Rectangle.Empty && _pencilBounds.Contains(p)) { EditRequested?.Invoke(); return; }
            if (HasClipboard && _clipboardBounds.Contains(p)) { CopyRequested?.Invoke(); return; }
            bool selectSingleOnRelease = false;
            if (!Locked && !_isSubtask)
            {
                var modifiers = GameService.Input.Keyboard.ActiveModifiers;
                bool extendRange = modifiers.HasFlag(ModifierKeys.Shift);
                bool toggle = modifiers.HasFlag(ModifierKeys.Ctrl);
                selectSingleOnRelease =
                    DragReorderingEnabled &&
                    !extendRange &&
                    !toggle &&
                    IsSelected;
                if (!selectSingleOnRelease)
                    SelectionRequested?.Invoke(extendRange, toggle);
            }
            if (!Locked && DragReorderingEnabled)
                DragCandidateRequested?.Invoke(selectSingleOnRelease);
        }

        protected override void OnRightMouseButtonPressed(MouseEventArgs e)
        {
            base.OnRightMouseButtonPressed(e);
            // Right-click is context-menu only everywhere on the row - checking and
            // unchecking is left-click exclusively (ToggleRequested handles both directions).
            if (!Locked) ContextMenuRequested?.Invoke();
        }

        protected override void Paint(SpriteBatch spriteBatch, Rectangle bounds)
        {
            var pixel = ContentService.Textures.Pixel;
            var font = _sizing.BodyFont;
            var smallFont = _sizing.SmallFont;

            if (IsSelected)
                spriteBatch.DrawOnCtrl(this, pixel, bounds, TaskmasterTheme.RowSelected);
            if (_hover)
                spriteBatch.DrawOnCtrl(this, pixel, bounds, TaskmasterTheme.RowHover);
            if (IsDropTarget)
            {
                int markerHeight = _sizing.Px(2);
                int markerY = DropAfter ? bounds.Height - markerHeight : 0;
                spriteBatch.DrawOnCtrl(
                    this,
                    pixel,
                    new Rectangle(_sizing.Px(4), markerY, Math.Max(0, bounds.Width - _sizing.Px(8)), markerHeight),
                    TaskmasterTheme.Gold);
            }

            bool done = _task.IsDone;
            var textColor = done ? TaskmasterTheme.DoneText
                : _dueSoon ? TaskmasterTheme.DueSoon
                : TaskmasterTheme.CreamWhite;

            if (!_isSubtask && _task.HasSubtasks)
            {
                var chevron = IsExpanded ? TaskmasterIcons.ChevronUp : TaskmasterIcons.ChevronDown;
                bool chevronHovered = _hover && ChevronBounds.Contains(RelativeMousePosition);
                spriteBatch.DrawOnCtrl(this, chevron, ChevronGlyphBounds,
                    chevronHovered ? TaskmasterTheme.Gold : TaskmasterTheme.MutedCream);
            }

            var cb = CheckboxBounds;
            spriteBatch.DrawOnCtrl(this, pixel, cb, TaskmasterTheme.ChipFill);
            DrawBorder(spriteBatch, cb, done ? TaskmasterTheme.Success : TaskmasterTheme.ChipBorder);
            if (done)
            {
                var glyph = new Rectangle(cb.X + 2, cb.Y + 2, cb.Width - 4, cb.Height - 4);
                spriteBatch.DrawOnCtrl(this, TaskmasterIcons.Check, glyph, TaskmasterTheme.Success);
            }

            int x = cb.Right + 8;
            int itemGap = _sizing.Px(8);
            int compactGap = _sizing.Px(6);
            var nameSize = font.MeasureString(_task.Name);
            var nameRect = new Rectangle(x, 0, (int)nameSize.Width + 2, Height);
            spriteBatch.DrawStringOnCtrl(this, _task.Name, font, nameRect, textColor,
                false, HorizontalAlignment.Left, VerticalAlignment.Middle);
            if (done)
            {
                var strike = new Rectangle(x, Height / 2, (int)nameSize.Width, 1);
                spriteBatch.DrawOnCtrl(this, pixel, strike, TaskmasterTheme.DoneText);
            }
            x = nameRect.Right + itemGap;

            if (HasCounter)
            {
                var chipText = $"{_task.CurrentCount}/{_task.TargetCount}";
                var chipSize = smallFont.MeasureString(chipText);
                int chipHeight = _sizing.Px(18);
                _chipBounds = new Rectangle(
                    x,
                    (Height - chipHeight) / 2,
                    (int)chipSize.Width + _sizing.Px(12),
                    chipHeight);
                spriteBatch.DrawOnCtrl(this, pixel, _chipBounds, TaskmasterTheme.ChipFill);
                DrawBorder(spriteBatch, _chipBounds, TaskmasterTheme.ChipBorder);
                spriteBatch.DrawStringOnCtrl(this, chipText, smallFont, _chipBounds, TaskmasterTheme.Gold,
                    false, HorizontalAlignment.Center, VerticalAlignment.Middle);
                x = _chipBounds.Right + itemGap;
            }
            else if (!_isSubtask && _task.HasSubtasks)
            {
                int subCount = _task.Subtasks.Count;
                string subText = subCount == 1 ? "1 subtask" : $"{subCount} subtasks";
                var subSize = smallFont.MeasureString(subText);
                var subRect = new Rectangle(x, 0, (int)subSize.Width + 2, Height);
                spriteBatch.DrawStringOnCtrl(this, subText, smallFont, subRect, TaskmasterTheme.DimText,
                    false, HorizontalAlignment.Left, VerticalAlignment.Middle);
                x = subRect.Right + itemGap;
                _chipBounds = Rectangle.Empty;
            }
            else
            {
                _chipBounds = Rectangle.Empty;
            }

            if (HasClipboard)
            {
                _clipboardBounds = new Rectangle(
                    x,
                    (Height - InlineIconHitSize) / 2,
                    InlineIconHitSize,
                    InlineIconHitSize);
                var clipboardGlyphBounds = CenteredGlyph(_clipboardBounds, InlineIconSize);
                bool flashing = DateTime.UtcNow < _copiedFlashUntilUtc;
                spriteBatch.DrawOnCtrl(this, TaskmasterIcons.Clipboard, clipboardGlyphBounds,
                    flashing ? TaskmasterTheme.Success : TaskmasterTheme.DimText);
                x = _clipboardBounds.Right + compactGap;
            }
            else
            {
                _clipboardBounds = Rectangle.Empty;
            }

            if (!string.IsNullOrEmpty(_task.Notes))
            {
                _noteBounds = new Rectangle(
                    x,
                    (Height - InlineIconHitSize) / 2,
                    InlineIconHitSize,
                    InlineIconHitSize);
                spriteBatch.DrawOnCtrl(
                    this,
                    TaskmasterIcons.Note,
                    CenteredGlyph(_noteBounds, InlineIconSize),
                    TaskmasterTheme.DimText);
                x = _noteBounds.Right + compactGap;
            }
            else
            {
                _noteBounds = Rectangle.Empty;
            }

            // Reserve the chevron's slot on every top-level row (drawn only when the
            // task actually has subtasks) so the countdown/edit-button column lines up
            // whether or not this particular row has one.
            int rightX = _isSubtask
                ? Width - ScrollbarMargin
                : Width - ScrollbarMargin - ChevronHitSize - compactGap;
            // Subtasks share the parent's countdown (see TaskRow.CountdownAnchor), so
            // repeating identical text on every child row was just noise - removed for
            // now, parent row still shows it.
            if (!_isSubtask && !string.IsNullOrEmpty(_countdownText))
            {
                var cdSize = smallFont.MeasureString(_countdownText);
                var cdRect = new Rectangle(rightX - (int)cdSize.Width - 2, 0, (int)cdSize.Width + 2, Height);
                spriteBatch.DrawStringOnCtrl(this, _countdownText, smallFont, cdRect,
                    _dueSoon ? TaskmasterTheme.DueSoon : TaskmasterTheme.DimText,
                    false, HorizontalAlignment.Right, VerticalAlignment.Middle);
                rightX = cdRect.X - itemGap;
            }

            // Close stays visible while this task's editor is open. Subtasks don't get
            // edit actions because they have no inline editor of their own.
            if (!_isSubtask && (_hover || IsEditing) && !Locked)
            {
                // Checkmark (save) sits just left of the close button, editing only -
                // this is the only way to commit the open editor's fields now that the
                // editor panel itself carries no Save/Cancel/Delete buttons of its own.
                if (IsEditing)
                {
                    int hitSize = RowHeight;
                    int actionGap = _sizing.Px(2);
                    _pencilBounds = new Rectangle(rightX - hitSize, 0, hitSize, hitSize);

                    var closeGlyphBounds = new Rectangle(
                        _pencilBounds.X + (hitSize - EditActionIconSize) / 2,
                        _pencilBounds.Y + (hitSize - EditActionIconSize) / 2,
                        EditActionIconSize,
                        EditActionIconSize);
                    spriteBatch.DrawOnCtrl(this, TaskmasterIcons.Cancel, closeGlyphBounds, TaskmasterTheme.CreamWhite);

                    _saveBounds = new Rectangle(
                        _pencilBounds.X - actionGap - hitSize,
                        0,
                        hitSize,
                        hitSize);

                    var saveGlyphBounds = new Rectangle(
                        _saveBounds.X + (hitSize - EditActionIconSize) / 2,
                        _saveBounds.Y + (hitSize - EditActionIconSize) / 2,
                        EditActionIconSize,
                        EditActionIconSize);
                    spriteBatch.DrawOnCtrl(this, TaskmasterIcons.Check, saveGlyphBounds, TaskmasterTheme.Success);
                }
                else
                {
                    int hitSize = RowHeight;
                    _pencilBounds = new Rectangle(
                        rightX - hitSize,
                        (Height - hitSize) / 2,
                        hitSize,
                        hitSize);
                    var pencilGlyphBounds = new Rectangle(
                        _pencilBounds.X + (hitSize - EditIconSize) / 2,
                        _pencilBounds.Y + (hitSize - EditIconSize) / 2,
                        EditIconSize,
                        EditIconSize);
                    spriteBatch.DrawOnCtrl(this, TaskmasterIcons.Pencil, pencilGlyphBounds, TaskmasterTheme.CreamWhite);
                    _saveBounds = Rectangle.Empty;
                }
            }
            else
            {
                _pencilBounds = Rectangle.Empty;
                _saveBounds = Rectangle.Empty;
            }
        }

        private void DrawBorder(SpriteBatch spriteBatch, Rectangle r, Color color)
        {
            var pixel = ContentService.Textures.Pixel;
            spriteBatch.DrawOnCtrl(this, pixel, new Rectangle(r.X, r.Y, r.Width, 1), color);
            spriteBatch.DrawOnCtrl(this, pixel, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), color);
            spriteBatch.DrawOnCtrl(this, pixel, new Rectangle(r.X, r.Y, 1, r.Height), color);
            spriteBatch.DrawOnCtrl(this, pixel, new Rectangle(r.Right - 1, r.Y, 1, r.Height), color);
        }

        private static Rectangle CenteredGlyph(Rectangle bounds, int glyphSize) =>
            new Rectangle(
                bounds.X + (bounds.Width - glyphSize) / 2,
                bounds.Y + (bounds.Height - glyphSize) / 2,
                glyphSize,
                glyphSize);
    }
}
