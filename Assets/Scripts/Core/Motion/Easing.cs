using System;

namespace NAS.Core.Motion
{
    public enum Ease
    {
        Linear,
        OutQuad,
        InQuad,
        InOutQuad,
        OutCubic,
        InCubic,
        InOutCubic,
        OutQuint,
        OutExpo,
        OutBack,   // overshoots a little then settles: pop-ins
        Spring     // damped oscillation: things that should feel physical
    }

    // The curves behind every UI animation. Pure maths (no Unity types) so it is unit-tested; every curve maps 0 -> 0
    // and 1 -> 1, so an animation always ends exactly on its target value.
    public static class Easing
    {
        public static float Evaluate(Ease ease, float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;

            switch (ease)
            {
                case Ease.Linear: return t;
                case Ease.OutQuad: return 1f - (1f - t) * (1f - t);
                case Ease.InQuad: return t * t;
                case Ease.InOutQuad: return t < 0.5f ? 2f * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 2) / 2f;
                case Ease.OutCubic: return 1f - (float)Math.Pow(1f - t, 3);
                case Ease.InCubic: return t * t * t;
                case Ease.InOutCubic: return t < 0.5f ? 4f * t * t * t : 1f - (float)Math.Pow(-2f * t + 2f, 3) / 2f;
                case Ease.OutQuint: return 1f - (float)Math.Pow(1f - t, 5);
                case Ease.OutExpo: return 1f - (float)Math.Pow(2f, -10f * t);
                case Ease.OutBack:
                {
                    const float c1 = 1.70158f;
                    const float c3 = c1 + 1f;
                    var u = t - 1f;
                    return 1f + c3 * u * u * u + c1 * u * u;
                }
                case Ease.Spring: return 1f - (float)(Math.Exp(-7.0 * t) * Math.Cos(9.0 * t));
                default: return t;
            }
        }

        public static float Lerp(float from, float to, float eased) => from + (to - from) * eased;
    }
}
