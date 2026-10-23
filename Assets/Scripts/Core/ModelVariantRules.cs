using System.Text.RegularExpressions;

namespace NAS.Core
{
    // Cars come with their options built in as alternative nodes (NAS_Backend/docs, NEO AR Showroom/docs/MODELER_BRIEF.md):
    // Wheels_A / Wheels_B (a group of four wheels each), Interior_A / Interior_B (seats, console...) and, under each door,
    // Interior_A_DoorCard_* / Interior_B_DoorCard_*. Until the customer can choose, variant A is shown and every other
    // letter is hidden. Pure name rules (no Unity types) so they are unit-tested like the other engine-free logic.
    // Cars without variant nodes (the older demo models) match nothing, so they load exactly as before.
    public static class ModelVariantRules
    {
        public const string DefaultVariant = "A";

        private static readonly Regex VariantNode =
            new Regex(@"^(Wheels|Interior)_(?<variant>[A-Z])(_|$)", RegexOptions.Compiled | RegexOptions.CultureInvariant);

        public const string Wheels = "Wheels";
        public const string Interior = "Interior";

        // True for a node that belongs to a variant other than the default one: it, and everything under it, is hidden.
        public static bool IsHiddenByDefault(string nodeName) =>
            TryGetVariant(nodeName, out _, out var variant) && variant != DefaultVariant;

        public static bool TryGetVariant(string nodeName, out string group, out string variant)
        {
            group = variant = null;
            if (string.IsNullOrEmpty(nodeName)) return false;
            var match = VariantNode.Match(nodeName);
            if (!match.Success) return false;
            group = match.Groups[1].Value;
            variant = match.Groups["variant"].Value;
            return true;
        }

        public static bool? ShouldShow(string nodeName, string group, string chosenVariant) =>
            TryGetVariant(nodeName, out var nodeGroup, out var variant) && nodeGroup == group ? variant == chosenVariant : (bool?)null;
    }
}
