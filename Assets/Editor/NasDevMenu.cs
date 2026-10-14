using NAS.Core.Motion;
using UnityEditor;

namespace NAS.EditorTools
{
    // Developer-only switches. "Reduce UI Motion" makes every animation jump to its end state (tests, screenshots); it is
    // remembered between Editor sessions and does not exist in release builds (MotionSettings ignores it there).
    public static class NasDevMenu
    {
        private const string ReduceMotionMenu = "NAS/Dev/Reduce UI Motion";

        [MenuItem(ReduceMotionMenu)]
        private static void ToggleReduceMotion() => MotionSettings.Reduced = !MotionSettings.Reduced;

        [MenuItem(ReduceMotionMenu, true)]
        private static bool ToggleReduceMotionValidate()
        {
            Menu.SetChecked(ReduceMotionMenu, MotionSettings.Reduced);
            return true;
        }
    }
}
