using System;
using System.Collections.Generic;
using Blish_HUD;
using Blish_HUD.Controls;
using Blish_HUD.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using MonoGame.Extended.BitmapFonts;
using Taskmaster.Models;
using Taskmaster.UI.Controls;

namespace Taskmaster.UI
{
    public class TabStrip : Control
    {
        private const int TabPaddingX = 10;
        private const int TabGap = 4;
        private const int NameBadgeGap = 10;
        private const int BadgePaddingX = 6;
        private const int BadgeHeight = 18;
        private const int TabRadius = 3;
        private const int MinTabWidth = 88;
        private const int MaxTabNameWidth = 140;
        private const int PlusWidth = 28;
        private const int PlusIconSize = 14;
        private const int DragThreshold = 6;
        private const int ScrollArrowWidth = 18;
        private const int ScrollStep = 90;
        private const int ControlGap = 2;
        private const int EdgeFadeWidth = 10;
        private const int ControlRailWidth = ScrollArrowWidth * 2 + PlusWidth + ControlGap * 2;

        private IReadOnlyList<TodoTab> _tabs = new List<TodoTab>();
        private readonly List<Rectangle> _tabBounds = new List<Rectangle>();
        private int _hoverIndex = -1;
        private bool _hoverPlus;
        private bool _hoverLeft;
        private bool _hoverRight;

        private int _dragIndex = -1;
        private Point _dragStart;
        private bool _dragging;

        // Horizontal scroll - kicks in once tabs overflow the strip's width.
        private int _scrollX;
        private int _contentWidth;
        private bool _panning;
        private int _panStartX;
        private int _panStartScrollX;
        private Guid? _activeTabId;
        private Guid? _editingTabId;
        private bool _ensureActiveVisible;

        public Guid? ActiveTabId
        {
            get => _activeTabId;
            set
            {
                if (_activeTabId == value) return;
                _activeTabId = value;
                _ensureActiveVisible = true;
                Invalidate();
            }
        }
        public Guid? EditingTabId
        {
            get => _editingTabId;
            set
            {
                if (_editingTabId == value) return;
                _editingTabId = value;
                Invalidate();
            }
        }
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
                int nameWidth = Math.Min(MaxTabNameWidth, (int)font.MeasureString(tab.Name).Width);
                int badgeWidth = (int)badgeFont.MeasureString(BadgeText(tab)).Width + BadgePaddingX * 2;
                int w = Math.Max(MinTabWidth,
                    nameWidth + badgeWidth + NameBadgeGap + TabPaddingX * 2);
                _tabBounds.Add(new Rectangle(x, 2, w, Height - 3));
                x += w + TabGap;
            }
            _contentWidth = x;

            if (_ensureActiveVisible && ActiveTabId.HasValue)
            {
                int activeIndex = -1;
                for (int i = 0; i < _tabs.Count; i++)
                    if (_tabs[i].Id == ActiveTabId.Value) { activeIndex = i; break; }

                if (activeIndex >= 0 && ViewportWidth > 0)
                {
                    var activeBounds = _tabBounds[activeIndex];
                    if (activeBounds.Width >= ViewportWidth || activeBounds.X < _scrollX)
                        _scrollX = activeBounds.X;
                    else if (activeBounds.Right > _scrollX + ViewportWidth)
                        _scrollX = activeBounds.Right - ViewportWidth;
                }
            }
            _ensureActiveVisible = false;
            _scrollX = ClampScroll(_scrollX);

            for (int i = 0; i < _tabBounds.Count; i++)
            {
                var r = _tabBounds[i];
                _tabBounds[i] = new Rectangle(r.X - _scrollX, r.Y, r.Width, r.Height);
            }
        }

        private int RailStartX => Math.Max(0, Width - ControlRailWidth);
        private int ViewportWidth => Math.Max(0, RailStartX - TabGap);
        private int MaxScroll() => Math.Max(0, _contentWidth - ViewportWidth);
        private int ClampScroll(int value) => Math.Max(0, Math.Min(value, MaxScroll()));
        private bool CanScrollLeft => _scrollX > 0;
        private bool CanScrollRight => _scrollX < MaxScroll();

        private Rectangle LeftArrowBounds =>
            new Rectangle(RailStartX, 2, ScrollArrowWidth, Height - 2);
        private Rectangle RightArrowBounds =>
            new Rectangle(LeftArrowBounds.Right + ControlGap, 2, ScrollArrowWidth, Height - 2);
        private Rectangle PlusBounds =>
            new Rectangle(RightArrowBounds.Right + ControlGap, 2, PlusWidth, Height - 2);

        private int IndexAt(Point p)
        {
            if (p.X < 0 || p.X >= ViewportWidth) return -1;
            for (int i = 0; i < _tabBounds.Count; i++)
                if (_tabBounds[i].Contains(p)) return i;
            return -1;
        }

        public bool TryGetTabEditBounds(Guid tabId, out Rectangle bounds)
        {
            RecomputeLayout();
            for (int i = 0; i < _tabs.Count; i++)
            {
                if (_tabs[i].Id != tabId) continue;
                var tabBounds = _tabBounds[i];
                if (tabBounds.Right <= 0 || tabBounds.X >= ViewportWidth) break;

                var visualBounds = ActiveTabId == tabId
                    ? new Rectangle(tabBounds.X, tabBounds.Y - 1, tabBounds.Width, tabBounds.Height + 1)
                    : tabBounds;
                bounds = new Rectangle(
                    visualBounds.X + 6,
                    visualBounds.Y + 3,
                    Math.Max(40, visualBounds.Width - 12),
                    24);
                return true;
            }

            bounds = Rectangle.Empty;
            return false;
        }

        protected override void OnMouseMoved(MouseEventArgs e)
        {
            base.OnMouseMoved(e);
            var p = RelativeMousePosition;
            _hoverIndex = IndexAt(p);
            _hoverPlus = PlusBounds.Contains(p);
            _hoverLeft = LeftArrowBounds.Contains(p);
            _hoverRight = RightArrowBounds.Contains(p);
            BasicTooltipText = _hoverPlus && Locked
                ? "Locked - unlock to add tabs"
                : _hoverIndex >= 0 && _hoverIndex < _tabs.Count &&
                  GameService.Content.DefaultFont14.MeasureString(_tabs[_hoverIndex].Name).Width > MaxTabNameWidth
                    ? _tabs[_hoverIndex].Name
                    : null;

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
            int delta = GameService.Input.Mouse.State.ScrollWheelValue;
            if (delta != 0)
                _scrollX = ClampScroll(_scrollX - Math.Sign(delta) * ScrollStep);
            Invalidate();
        }

        protected override void OnMouseLeft(MouseEventArgs e)
        {
            base.OnMouseLeft(e);
            _hoverIndex = -1;
            _hoverPlus = false;
            _hoverLeft = false;
            _hoverRight = false;
            _panning = false;
            BasicTooltipText = null;
            Invalidate();
        }

        protected override void OnLeftMouseButtonPressed(MouseEventArgs e)
        {
            base.OnLeftMouseButtonPressed(e);
            var p = RelativeMousePosition;
            if (LeftArrowBounds.Contains(p))
            {
                if (CanScrollLeft) _scrollX = ClampScroll(_scrollX - ScrollStep);
                Invalidate();
                return;
            }
            if (RightArrowBounds.Contains(p))
            {
                if (CanScrollRight) _scrollX = ClampScroll(_scrollX + ScrollStep);
                Invalidate();
                return;
            }
            if (PlusBounds.Contains(p)) { if (!Locked) AddClicked?.Invoke(); return; }
            int i = IndexAt(p);
            if (i >= 0)
            {
                _dragIndex = i;
                _dragStart = p;
                _dragging = false;
            }
            else if (p.X < ViewportWidth && MaxScroll() > 0)
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
                        if (target < 0) target = RelativeMousePosition.X >= ViewportWidth ? _tabs.Count - 1 : _dragIndex;
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

            spriteBatch.DrawOnCtrl(this, pixel,
                new Rectangle(0, Height - 1, ViewportWidth, 1),
                TaskmasterTheme.SubtleBorder);

            for (int i = 0; i < _tabs.Count; i++)
            {
                var tab = _tabs[i];
                var r = _tabBounds[i];
                if (r.Right <= 0 || r.X >= ViewportWidth) continue;
                bool active = ActiveTabId.HasValue && tab.Id == ActiveTabId.Value;
                bool hover = i == _hoverIndex;
                var visualBounds = active
                    ? new Rectangle(r.X, r.Y - 1, r.Width, r.Height + 1)
                    : r;

                if (active)
                {
                    DrawRoundedBorder(spriteBatch, visualBounds,
                        TaskmasterTheme.TabActiveBorder,
                        TaskmasterTheme.TabActiveFill);
                }
                else if (hover)
                {
                    DrawRoundedRect(spriteBatch, visualBounds, TaskmasterTheme.TabHoverFill);
                }

                var accent = TaskmasterTheme.ParseAccentHex(tab.AccentColorHex) ?? TaskmasterTheme.Gold;
                var accentLine = new Rectangle(
                    visualBounds.X + TabPaddingX,
                    visualBounds.Bottom - 2,
                    Math.Max(8, visualBounds.Width - TabPaddingX * 2),
                    2);
                spriteBatch.DrawOnCtrl(this, pixel, accentLine,
                    active ? accent : accent * (hover ? 0.72f : 0.48f));

                if (EditingTabId.HasValue && tab.Id == EditingTabId.Value) continue;

                var nameColor = active ? TaskmasterTheme.CreamWhite : TaskmasterTheme.TabInactiveText;
                var badge = BadgeText(tab);
                int badgeWidth = (int)badgeFont.MeasureString(badge).Width + BadgePaddingX * 2;
                var badgeBounds = new Rectangle(
                    visualBounds.Right - TabPaddingX - badgeWidth,
                    visualBounds.Y + (visualBounds.Height - BadgeHeight) / 2 + 1,
                    badgeWidth,
                    BadgeHeight);
                var nameRect = new Rectangle(
                    visualBounds.X + TabPaddingX,
                    visualBounds.Y + 2,
                    Math.Max(0, badgeBounds.X - NameBadgeGap - visualBounds.X - TabPaddingX),
                    visualBounds.Height - 2);
                spriteBatch.DrawStringOnCtrl(this, FitText(tab.Name, font, nameRect.Width), font, nameRect, nameColor,
                    false, HorizontalAlignment.Left, VerticalAlignment.Middle);

                bool complete = tab.TotalCount > 0 && tab.DoneCount == tab.TotalCount;
                var badgeAccent = complete
                    ? TaskmasterTheme.Success
                    : accent;
                DrawRoundedBorder(spriteBatch, badgeBounds,
                    active ? badgeAccent * 0.72f : TaskmasterTheme.TabBadgeBorder,
                    active ? TaskmasterTheme.TabBadgeActiveFill : TaskmasterTheme.TabBadgeFill,
                    radius: 2);
                spriteBatch.DrawStringOnCtrl(this, badge, badgeFont, badgeBounds,
                    active ? TaskmasterTheme.CreamWhite
                        : complete ? badgeAccent
                        : TaskmasterTheme.MutedCream,
                    false, HorizontalAlignment.Center, VerticalAlignment.Middle);
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

            DrawEdgeFades(spriteBatch);

            // The rail is painted after the tabs, masking anything beyond the viewport
            // and giving the navigation controls stable, non-overlapping real estate.
            var railBounds = new Rectangle(RailStartX, 2, ControlRailWidth, Height - 2);
            spriteBatch.DrawOnCtrl(this, pixel, railBounds, TaskmasterTheme.ChipFill);

            DrawScrollButton(spriteBatch, font, LeftArrowBounds, "<", CanScrollLeft, _hoverLeft);
            DrawScrollButton(spriteBatch, font, RightArrowBounds, ">", CanScrollRight, _hoverRight);

            // Plus button - always drawn as a visible chip (fill + border) so it reads
            // as a button rather than stray text, with a stronger highlight on hover.
            // Grayed out (not hidden) while locked, so it stays a stable landmark and
            // still visibly explains why clicking it does nothing.
            bool plusHover = _hoverPlus && !Locked;
            spriteBatch.DrawOnCtrl(this, pixel, PlusBounds,
                plusHover ? TaskmasterTheme.RowHover : TaskmasterTheme.ChipFill);
            DrawBorder(spriteBatch, PlusBounds, plusHover ? TaskmasterTheme.Gold : TaskmasterTheme.ChipBorder);

            var plusIconBounds = new Rectangle(
                PlusBounds.X + (PlusBounds.Width - PlusIconSize) / 2,
                PlusBounds.Y + (PlusBounds.Height - PlusIconSize) / 2,
                PlusIconSize, PlusIconSize);
            spriteBatch.DrawOnCtrl(this, TaskmasterIcons.Plus, plusIconBounds,
                Locked ? TaskmasterTheme.DimText : plusHover ? TaskmasterTheme.CreamWhite : TaskmasterTheme.MutedCream);
        }

        private void DrawScrollButton(
            SpriteBatch spriteBatch,
            BitmapFont font,
            Rectangle buttonBounds,
            string label,
            bool enabled,
            bool hovered)
        {
            var fill = enabled && hovered ? TaskmasterTheme.RowHover : TaskmasterTheme.ChipFill;
            var border = enabled && hovered ? TaskmasterTheme.Gold : TaskmasterTheme.ChipBorder;
            var text = enabled ? TaskmasterTheme.MutedCream : TaskmasterTheme.DimText;

            spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, buttonBounds, fill);
            DrawBorder(spriteBatch, buttonBounds, border);
            spriteBatch.DrawStringOnCtrl(this, label, font, buttonBounds, text,
                false, HorizontalAlignment.Center, VerticalAlignment.Middle);
        }

        private void DrawEdgeFades(SpriteBatch spriteBatch)
        {
            int fadeWidth = Math.Min(EdgeFadeWidth, ViewportWidth);
            if (fadeWidth <= 0) return;
            var pixel = ContentService.Textures.Pixel;

            if (CanScrollLeft)
                for (int i = 0; i < fadeWidth; i++)
                {
                    float strength = (float)(fadeWidth - i) / fadeWidth * 0.65f;
                    spriteBatch.DrawOnCtrl(this, pixel,
                        new Rectangle(i, 2, 1, Height - 2),
                        TaskmasterTheme.ChipFill * strength);
                }

            if (CanScrollRight)
                for (int i = 0; i < fadeWidth; i++)
                {
                    float strength = (float)(i + 1) / fadeWidth * 0.65f;
                    spriteBatch.DrawOnCtrl(this, pixel,
                        new Rectangle(ViewportWidth - fadeWidth + i, 2, 1, Height - 2),
                        TaskmasterTheme.ChipFill * strength);
                }
        }

        private static string FitText(string text, BitmapFont font, int maxWidth)
        {
            if (maxWidth <= 0) return "";
            if (font.MeasureString(text).Width <= maxWidth) return text;

            const string ellipsis = "...";
            int length = text.Length;
            while (length > 0 &&
                   font.MeasureString(text.Substring(0, length) + ellipsis).Width > maxWidth)
                length--;
            return length > 0 ? text.Substring(0, length) + ellipsis : ellipsis;
        }

        private void DrawRoundedBorder(
            SpriteBatch spriteBatch,
            Rectangle bounds,
            Color border,
            Color fill,
            int radius = TabRadius)
        {
            DrawRoundedRect(spriteBatch, bounds, border, radius);
            if (bounds.Width <= 2 || bounds.Height <= 2) return;
            DrawRoundedRect(spriteBatch,
                new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, bounds.Height - 2),
                fill,
                Math.Max(1, radius - 1));
        }

        private void DrawRoundedRect(SpriteBatch spriteBatch, Rectangle bounds, Color color, int radius = TabRadius)
        {
            radius = Math.Max(1, Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2));
            var pixel = ContentService.Textures.Pixel;

            if (bounds.Height > radius * 2)
                spriteBatch.DrawOnCtrl(this, pixel,
                    new Rectangle(bounds.X, bounds.Y + radius, bounds.Width, bounds.Height - radius * 2),
                    color);

            for (int row = 0; row < radius; row++)
            {
                int inset = radius - row - 1;
                int width = Math.Max(0, bounds.Width - inset * 2);
                spriteBatch.DrawOnCtrl(this, pixel,
                    new Rectangle(bounds.X + inset, bounds.Y + row, width, 1),
                    color);
                spriteBatch.DrawOnCtrl(this, pixel,
                    new Rectangle(bounds.X + inset, bounds.Bottom - row - 1, width, 1),
                    color);
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
    }
}
