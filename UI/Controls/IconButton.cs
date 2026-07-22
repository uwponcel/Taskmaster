using Blish_HUD;
using Blish_HUD.Controls;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Taskmaster.UI;

namespace Taskmaster.UI.Controls
{
    /// <summary>
    /// A StandardButton that draws a single glyph icon centered in the button, tinted
    /// with a theme color. The built-in Icon layout left-aligns the glyph, so an
    /// icon-only StandardButton ends up off-center and untinted - this fixes that.
    /// </summary>
    public class IconButton : StandardButton
    {
        private Color _tint;
        private Texture2D _iconTexture;
        private bool _selected;
        private int _glyphSize = 16;

        public IconButton(Texture2D icon, Color tint)
        {
            _iconTexture = icon;
            _tint = tint;
        }

        public Color Tint
        {
            get => _tint;
            set { if (_tint == value) return; _tint = value; Invalidate(); }
        }

        public Texture2D IconTexture
        {
            get => _iconTexture;
            set { if (_iconTexture == value) return; _iconTexture = value; Invalidate(); }
        }

        public bool Selected
        {
            get => _selected;
            set { if (_selected == value) return; _selected = value; Invalidate(); }
        }

        public int GlyphSize
        {
            get => _glyphSize;
            set
            {
                int next = System.Math.Max(1, value);
                if (_glyphSize == next) return;
                _glyphSize = next;
                Invalidate();
            }
        }

        protected override void Paint(SpriteBatch spriteBatch, Rectangle bounds)
        {
            base.Paint(spriteBatch, bounds);
            if (_iconTexture == null) return;

            if (_selected)
            {
                var fill = new Rectangle(3, 3, _size.X - 6, _size.Y - 6);
                spriteBatch.DrawOnCtrl(this, ContentService.Textures.Pixel, fill, TaskmasterTheme.ToggleActiveFill);
            }

            var glyphColor = _selected ? TaskmasterTheme.ToggleActiveGlyph : _tint;
            int iconSize = System.Math.Min(_glyphSize, System.Math.Min(_size.X, _size.Y));
            var iconBounds = new Rectangle(
                _size.X / 2 - iconSize / 2,
                _size.Y / 2 - iconSize / 2,
                iconSize,
                iconSize);
            spriteBatch.DrawOnCtrl(this, _iconTexture, iconBounds, glyphColor);
        }
    }
}
