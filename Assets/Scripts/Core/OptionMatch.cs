using System;
using System.Collections.Generic;

namespace NAS.Core
{
    public readonly struct OptionRef
    {
        public readonly int Id;
        public readonly string Name;
        public readonly float Price;

        public OptionRef(int id, string name, float price)
        {
            Id = id;
            Name = name;
            Price = price;
        }
    }

    // Option rows are found by name: the style letter a name ends in ("Style B"), or a glow style's name.
    public static class OptionMatch
    {
        public static OptionRef? ForVariant(IEnumerable<OptionRef> options, string letter)
        {
            foreach (var option in options)
            {
                var name = (option.Name ?? string.Empty).Trim();
                var last = name.Substring(name.LastIndexOf(' ') + 1);
                if (string.Equals(last, letter, StringComparison.OrdinalIgnoreCase)) return option;
            }
            return null;
        }

        public static OptionRef? ForName(IEnumerable<OptionRef> options, string name)
        {
            foreach (var option in options)
                if (string.Equals((option.Name ?? string.Empty).Trim(), name?.Trim(), StringComparison.OrdinalIgnoreCase)) return option;
            return null;
        }

        public static string PriceTag(float price) =>
            price >= 0.5f ? "+" + Money.Rand(price) : string.Empty;
    }
}
