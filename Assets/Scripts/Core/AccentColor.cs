using System;
using System.Globalization;

namespace NAS.Core
{
    // The app's look is built around one cyan; this finds the colours that are that cyan (any brightness or
    // transparency) and re-casts them in a dealership's primary colour.
    public static class AccentColor
    {
        private const float MinBrightness = 0.55f; // a dark primary would vanish on the app's dark screens

        public static bool TryParse(string hex, out float r, out float g, out float b)
        {
            r = g = b = 0f;
            if (string.IsNullOrWhiteSpace(hex)) return false;
            hex = hex.Trim().TrimStart('#');
            if (hex.Length != 6 || !int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value))
                return false;
            r = ((value >> 16) & 0xFF) / 255f;
            g = ((value >> 8) & 0xFF) / 255f;
            b = (value & 0xFF) / 255f;
            return true;
        }

        public static bool IsAccent(float r, float g, float b)
        {
            ToHsv(r, g, b, out var h, out var s, out var v);
            return h >= 170f && h <= 210f && s >= 0.5f && v >= 0.4f;
        }

        // Keeps the source colour's brightness so shades of the cyan stay shades of the new colour.
        public static void Recolor(float r, float g, float b, float accentR, float accentG, float accentB,
            out float outR, out float outG, out float outB)
        {
            ToHsv(r, g, b, out _, out _, out var v);
            ToHsv(accentR, accentG, accentB, out var accentH, out var accentS, out var accentV);
            FromHsv(accentH, accentS, v * Math.Max(accentV, MinBrightness), out outR, out outG, out outB);
        }

        private static void ToHsv(float r, float g, float b, out float h, out float s, out float v)
        {
            var max = Math.Max(r, Math.Max(g, b));
            var min = Math.Min(r, Math.Min(g, b));
            var delta = max - min;
            v = max;
            s = max <= 0f ? 0f : delta / max;
            if (delta <= 0f) { h = 0f; return; }
            if (max == r) h = 60f * (((g - b) / delta) % 6f);
            else if (max == g) h = 60f * (((b - r) / delta) + 2f);
            else h = 60f * (((r - g) / delta) + 4f);
            if (h < 0f) h += 360f;
        }

        private static void FromHsv(float h, float s, float v, out float r, out float g, out float b)
        {
            var c = v * s;
            var x = c * (1f - Math.Abs((h / 60f) % 2f - 1f));
            var m = v - c;
            float r1, g1, b1;
            if (h < 60f) { r1 = c; g1 = x; b1 = 0f; }
            else if (h < 120f) { r1 = x; g1 = c; b1 = 0f; }
            else if (h < 180f) { r1 = 0f; g1 = c; b1 = x; }
            else if (h < 240f) { r1 = 0f; g1 = x; b1 = c; }
            else if (h < 300f) { r1 = x; g1 = 0f; b1 = c; }
            else { r1 = c; g1 = 0f; b1 = x; }
            r = r1 + m; g = g1 + m; b = b1 + m;
        }
    }
}
