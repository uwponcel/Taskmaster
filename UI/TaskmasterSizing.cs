using System;
using Blish_HUD;
using MonoGame.Extended.BitmapFonts;

namespace Taskmaster.UI
{
    public sealed class TaskmasterSizing
    {
        public const float MinInterfaceScale = 0.8f;
        public const float MaxInterfaceScale = 1.5f;
        public const float MinTextScale = 0.85f;
        public const float MaxTextScale = 1.4f;

        public float InterfaceScale { get; }
        public float TextScale { get; }

        public BitmapFont SmallFont => ClosestFont(12f * TextScale);
        public BitmapFont BodyFont => ClosestFont(14f * TextScale);
        public BitmapFont HeadingFont => ClosestFont(16f * TextScale);

        public TaskmasterSizing(float interfaceScale, float textScale)
        {
            InterfaceScale = Clamp(interfaceScale, MinInterfaceScale, MaxInterfaceScale);
            TextScale = Clamp(textScale, MinTextScale, MaxTextScale);
        }

        public int Px(int value) =>
            Math.Max(1, (int)Math.Round(value * InterfaceScale, MidpointRounding.AwayFromZero));

        private static float Clamp(float value, float min, float max) =>
            Math.Max(min, Math.Min(max, value));

        private static BitmapFont ClosestFont(float targetSize)
        {
            if (targetSize < 13f) return GameService.Content.DefaultFont12;
            if (targetSize < 15f) return GameService.Content.DefaultFont14;
            if (targetSize < 17f) return GameService.Content.DefaultFont16;
            return GameService.Content.DefaultFont18;
        }
    }
}
