using System;
using System.Globalization;

namespace NAS.Core
{
    public static class Money
    {
        // "R1 234 567", the same on every device (a phone's own currency format would show "$" or a comma).
        public static string Rand(float value) =>
            "R" + ((long)Math.Round(value)).ToString("N0", CultureInfo.InvariantCulture).Replace(',', ' ');
    }
}
