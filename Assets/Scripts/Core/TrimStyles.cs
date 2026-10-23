using System.Collections.Generic;

namespace NAS.Core
{
    public enum TrimMode
    {
        Steady = 0,
        Pulse = 1,
        Flow = 2
    }

    // Flow cycles through colours, so its Hex is only the swatch colour.
    public readonly struct TrimStyle
    {
        public readonly string Name;
        public readonly string Hex;
        public readonly TrimMode Mode;
        public readonly float Intensity;

        public TrimStyle(string name, string hex, TrimMode mode, float intensity)
        {
            Name = name;
            Hex = hex;
            Mode = mode;
            Intensity = intensity;
        }

        public bool IsOriginal => Hex == null;

        public string Label => IsOriginal ? Name : $"{Name} · {Mode}";
    }

    public static class TrimStyles
    {
        public static readonly IReadOnlyList<TrimStyle> All = new[]
        {
            new TrimStyle("Original", null, TrimMode.Steady, 0f),
            new TrimStyle("Neon Cyan", "#00D4FF", TrimMode.Steady, 1.6f),
            new TrimStyle("Amber", "#FFA726", TrimMode.Steady, 1.4f),
            new TrimStyle("Magenta", "#FF2BD6", TrimMode.Pulse, 1.8f),
            new TrimStyle("Crimson", "#FF2B3A", TrimMode.Pulse, 1.8f),
            new TrimStyle("Aurora", "#2BFFB0", TrimMode.Flow, 1.6f),
        };
    }
}
