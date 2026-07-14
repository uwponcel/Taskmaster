using System;
using System.Collections.Generic;
using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Taskmaster.Models;
using Taskmaster.UI.Controls;
using XnaMouse = Microsoft.Xna.Framework.Input.Mouse;

namespace Taskmaster.UI
{
    public class TabStrip : Control
    {
        private const int TabPaddingX = 12;
        private const int TabGap = 2;
        private const int PlusWidth = 28;
        private const int PlusIconSize = 14;
        private const int DragThreshold = 6;
        private const int ScrollArrowWidth = 18;
        private const int ScrollStep = 90;

        private IReadOnlyList<TodoTab> _tabs = new List<TodoTab>();
        private readonly List<Rectangle> _tabBounds = new List<Rectangle>();
        private Rectangle _plusBounds;
        private int _hoverIndex = -1;
        private bool _hoverPlus;

        private int _dragIndex = -1;
        private Point _dragStart;
        private bool _dragging;

        // Horizontal scroll - kicks in once tabs overflow the strip's width.
        private int _scrollX;
        private int _contentWidth;
        private bool _panning;
        private int _panStartX;
        private int _panStartScrollX;
        private int _lastWheelValue;
        private bool _wheelInitialized;

        public Guid? ActiveTabId { get; set; }
        /// <summary>When true, the "+" button is grayed out and stops adding tabs.</summary>
        public bool Locked { get; set; }

        public event Action<TodoTab> TabClicked;
        public event Action<TodoTab> TabRightClicked;
        public event Action AddClicked;
        /// <summary>Fired after a drag ends: (tab, newIndex).</summary>
        public event Action<TodoTab, int> TabReordered;

        public TabStrip()
        {
            Height = 32;
        }

        /// <summary>Also intercept the mouse wheel so hovering the strip scrolls it.</summary>
        protected override CaptureType CapturesInput() => CaptureType.Mouse | CaptureType.MouseWheel;

        public void SetTabs(IReadOnlyList<TodoTab> tabs)
        {
            _tabs = tabs ?? new List<TodoTab>();
            Invalidate();
        }

        private static string BadgeText(TodoTab tab) => $"{tab.DoneCount}/{tab.TotalCount}";

        private void RecomputeLayout()
        {
            _tabBounds.Clear();
            var font = GameService.Content.DefaultFont14;
            var badgeFont = GameService.Content.DefaultFont12;
            int x = 0;
            foreach (var tab in _tabs)
            {
                int w = (int)font.MeasureString(tab.Name).Width
                      + 6 + (int)badgeFont.MeasureString(BadgeText(tab)).Width
                      + TabPaddingX * 2;
                _tabBounds.Add(new Rectangle(x, 2, w, Height - 2));
                x += w + TabGap;
            }
            // Tabs only - the "+" button is pinned to the strip's right edge and never
            // scrolls, so it's always reachable even when tabs overflow.
            _contentWidth = x;
            _scrollX = ClampScroll(_scrollX);

            for (int i = 0; i < _tabBounds.Count; i++)
            {
                var r = _tabBounds[i];
                _tabBounds[i] = new Rectangle(r.X - _scrollX, r.Y, r.Width, r.Height);
            }
            _plusBounds = new Rectangle(Width - PlusWidth, 2, PlusWidth, Height - 2);
        }

        private int ViewportWidth => Math.Max(0, Width - PlusWidth - TabGap);
        private int MaxScroll() => Math.Max(0, _contentWidth - ViewportWidth);
        private int ClampScroll(int value) => Math.Max(0, Math.Min(value, MaxScroll()));
        private bool CanScrollLeft => _scrollX > 0;
        private bool CanScrollRight => _scrollX < MaxScroll();

        // Overlay affordances at the scrollable area's edges - always clickable
        // regardless of whether the tabs happen to leave any empty background to
        // drag from, unlike the pan-drag fallback below.
        private Rectangle LeftArrowBounds => new Rectangle(0, 2, ScrollArrowWidth, Height - 2);
        private Rectangle RightArrowBounds =>
            new Rectangle(ViewportWidth - ScrollArrowWidth, 2, ScrollArrowWidth, Height - 2);

        private int IndexAt(Point p)
        {
            for (int i = 0; i < _tabBounds.Count; i++)
                if (_tabBounds[i].Contains(p)) return i;
            return -1;
        }

        protected override void OnMouseMoved(MouseEventArgs e)
        {
            base.OnMouseMoved(e);
            var p = RelativeMousePosition;
            _hoverIndex = IndexAt(p);
            _hoverPlus = _plusBounds.Contains(p);
            // A single Control only has one tooltip, so it's set dynamically based on
            // which hand-drawn sub-region the mouse is actually over - here, explaining
            // why the "+" button looks grayed out rather than just silently doing nothing.
            BasicTooltipText = (_hoverPlus && Locked) ? "Locked - unlock to add tabs" : null;

            if (_panning)
            {
                _scrollX = ClampScroll(_panStartScrollX + (_panStartX - p.X));
            }
            else if (_dragIndex >= 0 && !_dragging &&
                Math.Abs(p.X - _dragStart.X) > DragThreshold)
            {
                _dragging = true;
            }
            Invalidate();
        }

        protected override void OnMouseWheelScrolled(MouseEventArgs e)
        {
            base.OnMouseWheelScrolled(e);
            int current = XnaMouse.GetState().ScrollWheelValue;
            if (_wheelInitialized)
            {
                int delta = current - _lastWheelValue;
                if (delta != 0) _scrollX = ClampScroll(_scrollX - delta / 4);
            }
            _lastWheelValue = current;
            _wheelInitialized = true;
            Invalidate();
        }

        protected override void OnMouseLeft(MouseEventArgs e)
        {
            base.OnMouseLeft(e);
            _hoverIndex = -1;
            _hoverPlus = false;
            _panning = false;
            BasicTooltipText = null;
            Invalidate();
        }

        protected override void OnLeftMouseButtonPressed(MouseEventArgs e)
        {
            base.OnLeftMouseButtonPressed(e);
            var p = RelativeMousePosition;
            if (CanScrollLeft && LeftArrowBounds.Contains(p)) { _scrollX = ClampScroll(_scrollX - ScrollStep); Invalidate(); return; }
            if (CanScrollRight && RightArrowBounds.Contains(p)) { _scrollX = ClampScroll(_scrollX + ScrollStep); Invalidate(); return; }
            if (_plusBounds.Contains(p)) { if (!Locked) AddClicked?.Invoke(); return; }
            int i = IndexAt(p);
            if (i >= 0)
            {
                _dragIndex = i;
                _dragStart = p;
                _dragging = false;
            }
            else if (MaxScroll() > 0)
            {
                // Empty strip background with overflowing tabs - drag to pan instead.
                _panning = true;
                _panStartX = p.X;
                _panStartScrollX = _scrollX;
            }
        }

        protected override void OnLeftMouseButtonReleased(MouseEventArgs e)
        {
            base.OnLeftMouseButtonReleased(e);
            if (_dragIndex >= 0)
            {
                var tab = _dragIndex < _tabs.Count ? _tabs[_dragIndex] : null;
                if (tab != null)
                {
                    if (_dragging)
                    {
                        int target = IndexAt(RelativeMousePosition);
                        if (target < 0) target = RelativeMousePosition.X > _plusBounds.X ? _tabs.Count - 1 : _dragIndex;
                        if (target != _dragIndex) TabReordered?.Invoke(tab, target);
                    }
                    else
                    {
                        TabClicked?.Invoke(tab);
                    }
                }
            }
            _dragIndex = -1;
            _dragging = false;
            _panning = false;
        }

        protected override void OnRightMouseButtonPressed(MouseEventArgs e)
        {
            base.OnRightMouseButtonPressed(e);
            int i = IndexAt(RelativeMousePosition);
            if (i >= 0 && i < _tabs.Count) TabRightClicked?.Invoke(_tabs[i]);
        }

        protected override void Paint(SpriteBatch spriteBatch, Rectangle bounds)
        {
            RecomputeLayout();
            var font = GameService.Content.DefaultFont14;
            var badgeFont = GameService.Content.DefaultFont12;
            var pixel = ContentService.Textures.Pixel;

            for (int i = 0; i < _tabs.Count; i++)
            {
                var tab = _tabs[i];
                var r = _tabBounds[i];
                bool active = ActiveTabId.HasValue && tab.Id == ActiveTabId.Value;
                bool hover = i == _hoverIndex;

                if (active)
                    spriteBatch.DrawOnCtrl(this, pixel, r, TaskmasterTheme.TabActiveFill);
                else if (hover)
                    spriteBatch.DrawOnCtrl(this, pixel, r, TaskmasterTheme.RowHover);

                var accent = TaskmasterTheme.ParseAccentHex(tab.AccentColorHex) ?? TaskmasterTheme.Gold;
                var accentBar = new Rectangle(r.X, r.Y, 3, r.Height);
                spriteBatch.DrawOnCtrl(this, pixel, accentBar, accent);

                var nameColor = active ? TaskmasterTheme.CreamWhite : TaskmasterTheme.TabInactiveText;
                var nameRect = new Rectangle(r.X + TabPaddingX, r.Y, r.Width - TabPaddingX * 2, r.Height);
                spriteBatch.DrawStringOnCtrl(this, tab.Name, font, nameRect, nameColor,
                    false, HorizontalAlignment.Left, VerticalAlignment.Middle);

                var badge = BadgeText(tab);
                var badgeColor = tab.TotalCount > 0 && tab.DoneCount == tab.TotalCount
                    ? TaskmasterTheme.Success
                    : TaskmasterTheme.Gold;
                spriteBatch.DrawStringOnCtrl(this, badge, badgeFont, nameRect, badgeColor,
                    false, HorizontalAlignment.Right, VerticalAlignment.Middle);
            }

            if (_dragging && _dragIndex >= 0 && _dragIndex < _tabBounds.Count)
            {
                int target = IndexAt(RelativeMousePosition);
                if (target >= 0 && target < _tabBounds.Count)
                {
                    var t = _tabBounds[target];
                    var marker = new Rectangle(target > _dragIndex ? t.Right - 2 : t.X, t.Y, 2, t.Height);
                    spriteBatch.DrawOnCtrl(this, pixel, marker, TaskmasterTheme.Gold);
                }
            }

            // Scroll arrows - overlaid on top of the tabs at the edges of the scrollable
            // area, only when there's actually somewhere to scroll to. Guaranteed
            // reachable even when tabs completely fill (or overflow) the viewport,
            // unlike relying on empty background to drag from.
            if (CanScrollLeft)
            {
                spriteBatch.DrawOnCtrl(this, pixel, LeftArrowBounds, TaskmasterTheme.ChipFill);
                DrawBorder(spriteBatch, LeftArrowBounds, TaskmasterTheme.ChipBorder);
                spriteBatch.DrawStringOnCtrl(this, "<", font, LeftArrowBounds, TaskmasterTheme.CreamWhite,
                    false, HorizontalAlignment.Center, VerticalAlignment.Middle);
            }
            if (CanScrollRight)
            {
                spriteBatch.DrawOnCtrl(this, pixel, RightArrowBounds, TaskmasterTheme.ChipFill);
                DrawBorder(spriteBatch, RightArrowBounds, TaskmasterTheme.ChipBorder);
                spriteBatch.DrawStringOnCtrl(this, ">", font, RightArrowBounds, TaskmasterTheme.CreamWhite,
                    false, HorizontalAlignment.Center, VerticalAlignment.Middle);
            }

            // Plus button - always drawn as a visible chip (fill + border) so it reads
            // as a button rather than stray text, with a stronger highlight on hover.
            // Grayed out (not hidden) while locked, so it stays a stable landmark and
            // still visibly explains why clicking it does nothing.
            bool plusHover = _hoverPlus && !Locked;
            spriteBatch.DrawOnCtrl(this, pixel, _plusBounds,
                plusHover ? TaskmasterTheme.RowHover : TaskmasterTheme.ChipFill);
            DrawBorder(spriteBatch, _plusBounds, plusHover ? TaskmasterTheme.Gold : TaskmasterTheme.ChipBorder);

            var plusIconBounds = new Rectangle(
                _plusBounds.X + (_plusBounds.Width - PlusIconSize) / 2,
                _plusBounds.Y + (_plusBounds.Height - PlusIconSize) / 2,
                PlusIconSize, PlusIconSize);
            spriteBatch.DrawOnCtrl(this, TaskmasterIcons.Plus, plusIconBounds,
                Locked ? TaskmasterTheme.DimText : plusHover ? TaskmasterTheme.CreamWhite : TaskmasterTheme.MutedCream);
        }

        private void DrawBorder(SpriteBatch spriteBatch, Rectangle r, Color color)
        {
            var pixel = ContentService.Textures.Pixel;
            spriteBatch.DrawOnCtrl(this, pixel, new Rectangle(r.X, r.Y, r.Width, 1), color);
            spriteBatch.DrawOnCtrl(this, pixel, new Rectangle(r.X, r.Bottom - 1, r.Width, 1), color);
            spriteBatch.DrawOnCtrl(this, pixel, new Rectangle(r.X, r.Y, 1, r.Height), color);
            spriteBatch.DrawOnCtrl(this, pixel, new Rectangle(r.Right - 1, r.Y, 1, r.Height), color);
        }
    }
}
