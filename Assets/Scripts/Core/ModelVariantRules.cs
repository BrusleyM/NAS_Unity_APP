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

        // True for a node that belongs to a variant other than the default one: it, and everything under it, is hidden.
        public static bool IsHiddenByDefault(string nodeName)
        {
            if (string.IsNullOrEmpty(nodeName)) return false;
            var match = VariantNode.Match(nodeName);
            return match.Success && match.Groups["variant"].Value != DefaultVariant;
        }
    }
}
