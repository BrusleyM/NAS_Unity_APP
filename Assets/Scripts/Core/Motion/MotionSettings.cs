using UnityEngine;

namespace NAS.Core.Motion
{
    // "Reduce motion": every animation jumps straight to its end state. A DEVELOPER switch only, by decision: it exists in the
    // Editor and in development builds (NAS > Dev > Reduce UI Motion in the Editor menu), and a release build always plays the
    // animations. Handy for tests and screenshots, where waiting for an animation to finish is noise.
    public static class MotionSettings
    {
        private const string PrefKey = "nas.dev.reduce_motion";
        private static bool? _reduced;

        public static bool DevToggleAvailable => Application.isEditor || Debug.isDebugBuild;

        public static bool Reduced
        {
            get
            {
                if (!DevToggleAvailable) return false;
                if (!_reduced.HasValue) _reduced = PlayerPrefs.GetInt(PrefKey, 0) == 1;
                return _reduced.Value;
            }
            set
            {
                if (!DevToggleAvailable) return;
                _reduced = value;
                PlayerPrefs.SetInt(PrefKey, value ? 1 : 0);
            }
        }
    }
}
