using Blish_HUD;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Taskmaster.UI
{
    public static class TaskmasterTheme
    {
        // Text
        public static readonly Color CreamWhite = new Color(240, 230, 210);
        public static readonly Color MutedCream = new Color(200, 191, 169);
        public static readonly Color DimText = new Color(128, 122, 108);
        public static readonly Color DoneText = new Color(106, 106, 98);

        // Accents / state
        public static readonly Color Gold = new Color(212, 166, 86);
        public static readonly Color DueSoon = new Color(224, 168, 74);
        public static readonly Color Danger = new Color(201, 106, 90);
        public static readonly Color Success = new Color(143, 191, 106);

        // Surfaces
        // Built via the multiply operator (not the raw 4-arg Color ctor) so RGB is
        // scaled down along with alpha - Blish HUD's SpriteBatch expects premultiplied
        // colors, and a bare `new Color(255,255,255,18)` renders as near-opaque white
        // instead of a subtle highlight.
        public static readonly Color RowHover = Color.White * 0.07f;
        public static readonly Color RowSelected = Gold * 0.18f;
        public static readonly Color SubtleBorder = Color.White * 0.10f;
        public static readonly Color TabActiveFill = new Color(46, 40, 41);
        public static readonly Color TabActiveBorder = new Color(82, 70, 68);
        public static readonly Color TabHoverFill = Color.White * 0.055f;
        public static readonly Color TabBadgeFill = new Color(29, 28, 34);
        public static readonly Color TabBadgeActiveFill = new Color(31, 29, 31);
        public static readonly Color TabBadgeBorder = Color.White * 0.09f;
        public static readonly Color TabInactiveText = new Color(138, 138, 128);
        public static readonly Color ChipFill = new Color(38, 38, 44);
        public static readonly Color ChipBorder = new Color(74, 74, 82);
        public static readonly Color ActionBarFill = new Color(25, 24, 29, 235);

        // Icon buttons (same semantics as Maestro)
        public static readonly Color IconGlyph = new Color(57, 50, 38);
        public static readonly Color ToggleActiveFill = new Color(46, 34, 28);
        public static readonly Color ToggleActiveGlyph = CreamWhite;

        // Tab accent presets - a custom-color popup, not GW2 dye colors (those need
        // the Gw2Sharp package + a live API fetch; these are just app-theme accents).
        public static readonly (string Name, Color Value)[] TabAccentPresets =
        {
            ("Gold", Gold),
            ("Rose", new Color(201, 112, 122)),
            ("Coral", new Color(217, 125, 90)),
            ("Teal", new Color(90, 166, 160)),
            ("Sky", new Color(90, 143, 201)),
            ("Violet", new Color(143, 122, 201)),
            ("Sage", new Color(127, 166, 90)),
            ("Slate", new Color(138, 138, 148)),
        };

        public static string ToHex(Color c) => $"{c.R:X2}{c.G:X2}{c.B:X2}";

        public static Color? ParseAccentHex(string hex)
        {
            if (string.IsNullOrEmpty(hex) || hex.Length != 6) return null;
            if (!byte.TryParse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber, null, out var r)) return null;
            if (!byte.TryParse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber, null, out var g)) return null;
            if (!byte.TryParse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber, null, out var b)) return null;
            return new Color(r, g, b);
        }

        // Window background gradient
        private static readonly Color BgTop = new Color(24, 22, 30, 255);
        private static readonly Color BgBottom = new Color(36, 30, 26, 255);
        private const int BACKGROUND_X_OFFSET = 1;
        private const int BACKGROUND_Y_OFFSET = 13;

        public static Color WithAlpha(Color c, int a) => new Color(c.R, c.G, c.B, a);

        public static Texture2D CreateWindowBackground(int windowWidth, int windowHeight)
        {
            var width = windowWidth - BACKGROUND_X_OFFSET;
            var height = windowHeight - BACKGROUND_Y_OFFSET;
            var context = GameService.Graphics.LendGraphicsDeviceContext();
            try
            {
                var texture = new Texture2D(context.GraphicsDevice, width, height);
                var data = new Color[width * height];
                for (var y = 0; y < height; y++)
                {
                    var t = height > 1 ? (float)y / (height - 1) : 0f;
                    var row = Color.Lerp(BgTop, BgBottom, t);
                    for (var x = 0; x < width; x++) data[y * width + x] = row;
                }
                texture.SetData(data);
                return texture;
            }
            finally
            {
                context.Dispose();
            }
        }
    }
}
