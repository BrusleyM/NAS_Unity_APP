using System;
using System.Collections;
using UnityEngine;

namespace NAS.Core.Motion
{
    /// <summary>Drives coroutine-based tweens for things that are not UI Toolkit elements (3D objects). One hidden runner object.</summary>
    internal sealed class MotionRunner : MonoBehaviour
    {
        private static MotionRunner _instance;

        public static MotionRunner Instance
        {
            get
            {
                if (_instance == null)
                {
                    var go = new GameObject("MotionRunner") { hideFlags = HideFlags.HideAndDontSave };
                    DontDestroyOnLoad(go);
                    _instance = go.AddComponent<MotionRunner>();
                }
                return _instance;
            }
        }
    }

    /// <summary>
    /// Motion for 3D objects, with the same rules as UiMotion: the end state is applied exactly, real time (not Time.timeScale),
    /// and the developer reduce-motion switch skips straight to the end.
    /// </summary>
    public static class TransformMotion
    {
        public static Coroutine Run(float duration, Ease ease, Action<float> apply, Action onComplete = null)
        {
            if (apply == null) return null;
            if (MotionSettings.Reduced || duration <= 0f)
            {
                apply(1f);
                onComplete?.Invoke();
                return null;
            }
            return MotionRunner.Instance.StartCoroutine(Routine(duration, ease, apply, onComplete));
        }

        private static IEnumerator Routine(float duration, Ease ease, Action<float> apply, Action onComplete)
        {
            var start = Time.realtimeSinceStartup;
            while (true)
            {
                var progress = (Time.realtimeSinceStartup - start) / duration;
                if (progress >= 1f) break;
                apply(Easing.Evaluate(ease, progress));
                yield return null;
            }
            apply(1f);
            onComplete?.Invoke();
        }

        /// <summary>A placed object settles in with a small overshoot instead of appearing at full size.</summary>
        public static void PopIn(Transform target, float from = 0.9f, float duration = 0.45f)
        {
            if (target == null) return;
            var finalScale = target.localScale;
            target.localScale = finalScale * from;
            Run(duration, Ease.OutBack, v =>
            {
                if (target != null) target.localScale = finalScale * Mathf.LerpUnclamped(from, 1f, v);
            });
        }
    }
}
