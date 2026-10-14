using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using UnityEngine;
using UnityEngine.UIElements;

namespace NAS.Core.Motion
{
    /// <summary>A running animation. Cancel() stops it where it is (no completion callback).</summary>
    public sealed class Tween
    {
        private IVisualElementScheduledItem _item;

        public bool IsRunning { get; private set; }

        internal void Start(IVisualElementScheduledItem item)
        {
            _item = item;
            IsRunning = true;
        }

        internal void Finish()
        {
            IsRunning = false;
            _item?.Pause();
        }

        public void Cancel() => Finish();
    }

    /// <summary>
    /// The one place UI animation lives. USS transitions (Resources/UI/Motion.uss) handle state changes that need no code
    /// (a button's press, hover colour); this handles motion that is sequenced or driven by data: screen entrances, staggered
    /// lists, pops, shakes, count-ups. No third-party package: a tween is a scheduled UI Toolkit item that eases a value 0 -> 1
    /// over real time (unaffected by Time.timeScale) and hands it to a callback.
    ///
    /// Rules every helper follows:
    ///  - the START state is set immediately and the END state is applied exactly, so an interrupted or skipped animation
    ///    never leaves an element half-visible;
    ///  - one running tween per element and channel: starting a new one on the same channel replaces the old;
    ///  - leaving the panel cancels the element's tweens, so a screen removed mid-animation leaks nothing;
    ///  - MotionSettings.Reduced (developer switch) jumps straight to the end state.
    /// </summary>
    public static class UiMotion
    {
        private static readonly ConditionalWeakTable<VisualElement, Dictionary<string, Tween>> Running =
            new ConditionalWeakTable<VisualElement, Dictionary<string, Tween>>();

        private static StyleSheet _motionSheet;

        // ---- the engine -----------------------------------------------------------------------------------------------

        /// <param name="apply">Receives the EASED progress (0 at the start, exactly 1 at the end; OutBack/Spring pass 1 on the way).</param>
        public static Tween Run(VisualElement element, float duration, Ease ease, Action<float> apply,
            float delay = 0f, string channel = "default", Action onComplete = null)
        {
            var tween = new Tween();
            if (element == null || apply == null) return tween;

            Cancel(element, channel);

            if (MotionSettings.Reduced || duration <= 0f)
            {
                apply(1f);
                onComplete?.Invoke();
                return tween;
            }

            var start = Time.realtimeSinceStartupAsDouble + delay;
            var item = element.schedule.Execute(() =>
            {
                var now = Time.realtimeSinceStartupAsDouble;
                if (now < start) return;

                var progress = (float)((now - start) / duration);
                if (progress >= 1f)
                {
                    tween.Cancel();
                    Forget(element, channel, tween);
                    apply(1f);
                    onComplete?.Invoke();
                }
                else
                {
                    apply(Easing.Evaluate(ease, progress));
                }
            }).Every(1);

            tween.Start(item);
            Remember(element, channel, tween);
            return tween;
        }

        /// <summary>Stops the element's tween on a channel, or all of them when channel is null.</summary>
        public static void Cancel(VisualElement element, string channel = null)
        {
            if (element == null || !Running.TryGetValue(element, out var byChannel)) return;

            if (channel != null)
            {
                if (byChannel.TryGetValue(channel, out var tween))
                {
                    tween.Cancel();
                    byChannel.Remove(channel);
                }
                return;
            }

            foreach (var tween in byChannel.Values) tween.Cancel();
            byChannel.Clear();
        }

        private static void Remember(VisualElement element, string channel, Tween tween)
        {
            if (!Running.TryGetValue(element, out var byChannel))
            {
                byChannel = new Dictionary<string, Tween>();
                Running.Add(element, byChannel);
                element.RegisterCallback<DetachFromPanelEvent>(_ => Cancel(element));
            }
            byChannel[channel] = tween;
        }

        private static void Forget(VisualElement element, string channel, Tween tween)
        {
            if (Running.TryGetValue(element, out var byChannel) && byChannel.TryGetValue(channel, out var current) && current == tween)
                byChannel.Remove(channel);
        }

        // ---- ready-made motions -----------------------------------------------------------------------------------------

        /// <summary>Loads the shared motion stylesheet (button press, hover, focus) onto a root. Safe to call repeatedly.</summary>
        public static void ApplyStyles(VisualElement root)
        {
            if (root == null) return;
            if (_motionSheet == null) _motionSheet = Resources.Load<StyleSheet>("UI/Motion");
            if (_motionSheet != null && !root.styleSheets.Contains(_motionSheet)) root.styleSheets.Add(_motionSheet);
        }

        public static Tween FadeIn(VisualElement element, float duration = 0.22f, float delay = 0f, Ease ease = Ease.OutCubic)
        {
            if (element == null) return new Tween();
            element.style.opacity = 0f;
            return Run(element, duration, ease, v => element.style.opacity = v, delay, "opacity");
        }

        public static Tween FadeOut(VisualElement element, float duration = 0.16f, Action onComplete = null)
        {
            if (element == null) return new Tween();
            var from = element.resolvedStyle.opacity;
            return Run(element, duration, Ease.InQuad, v => element.style.opacity = from * (1f - v), 0f, "opacity", onComplete);
        }

        /// <summary>Fade in while rising into place: the standard entrance for a screen or one item of a list.</summary>
        public static Tween Enter(VisualElement element, float delay = 0f, float rise = 14f, float duration = 0.28f)
        {
            if (element == null) return new Tween();
            element.style.opacity = 0f;
            element.style.translate = new Translate(0, rise);
            return Run(element, duration, Ease.OutCubic, v =>
            {
                element.style.opacity = v;
                element.style.translate = new Translate(0, rise * (1f - v));
            }, delay, "enter");
        }

        /// <summary>Enter() for each element in turn, <paramref name="step"/> seconds apart.</summary>
        public static void Stagger(IEnumerable<VisualElement> elements, float step = 0.05f, float startDelay = 0f, float rise = 12f)
        {
            if (elements == null) return;
            var index = 0;
            foreach (var element in elements)
                Enter(element, startDelay + index++ * step, rise);
        }

        /// <summary>Scale up from slightly smaller with a small overshoot: buttons appearing, a confirmation, a sheet's content.</summary>
        public static Tween Pop(VisualElement element, float from = 0.86f, float duration = 0.34f, float delay = 0f)
        {
            if (element == null) return new Tween();
            element.style.scale = new Scale(new Vector2(from, from));
            element.style.opacity = 0f;
            return Run(element, duration, Ease.OutBack, v =>
            {
                var s = Mathf.LerpUnclamped(from, 1f, v);
                element.style.scale = new Scale(new Vector2(s, s));
                element.style.opacity = Mathf.Clamp01(v * 2.5f);
            }, delay, "pop");
        }

        /// <summary>A short horizontal shake: "that was wrong" (a rejected login, an invalid field).</summary>
        public static Tween Shake(VisualElement element, float amplitude = 7f, float duration = 0.34f)
        {
            if (element == null) return new Tween();
            return Run(element, duration, Ease.Linear, v =>
                element.style.translate = new Translate(Mathf.Sin(v * Mathf.PI * 6f) * amplitude * (1f - v), 0), 0f, "shake");
        }

        /// <summary>Counts a number up to its value, formatted by the caller (prices, "2 / 6").</summary>
        public static Tween CountUp(Label label, float from, float to, Func<float, string> format, float duration = 0.6f)
        {
            if (label == null || format == null) return new Tween();
            return Run(label, duration, Ease.OutCubic, v => label.text = format(Mathf.Lerp(from, to, v)), 0f, "count");
        }

        /// <summary>
        /// Rotates an element continuously until <see cref="StopSpin"/> (spinners). Not affected by the reduce-motion switch:
        /// a spinner is the "working" indicator, not decoration.
        /// </summary>
        public static Tween Spin(VisualElement element, float degreesPerSecond = 220f)
        {
            var tween = new Tween();
            if (element == null) return tween;

            Cancel(element, "spin");
            var start = Time.realtimeSinceStartupAsDouble;
            var item = element.schedule.Execute(() =>
            {
                var angle = (float)((Time.realtimeSinceStartupAsDouble - start) * degreesPerSecond % 360.0);
                element.style.rotate = new StyleRotate(new Rotate(new Angle(angle, AngleUnit.Degree)));
            }).Every(1);
            tween.Start(item);
            Remember(element, "spin", tween);
            return tween;
        }

        public static void StopSpin(VisualElement element) => Cancel(element, "spin");

        /// <summary>The transition between screens, used by the router for every card swap.</summary>
        public static Tween ScreenEnter(VisualElement screenContainer) => Enter(screenContainer, 0f, 18f, 0.3f);

        /// <summary>USS class that opts an element into its screen's staggered entrance (see <see cref="Reveal"/>).</summary>
        public const string ItemClass = "motion-item";

        /// <summary>
        /// Shows a freshly built screen: a screen that marks its parts with <see cref="ItemClass"/> fades in while those parts
        /// rise into place one after another, in document order; a screen with none simply enters as a whole.
        /// </summary>
        public static void Reveal(VisualElement screenContainer, float step = 0.06f)
        {
            if (screenContainer == null) return;
            var items = screenContainer.Query<VisualElement>(className: ItemClass).ToList();
            if (items.Count == 0)
            {
                ScreenEnter(screenContainer);
                return;
            }

            // A long screen (many items) must not take long to appear: cap the whole stagger at about half a second.
            FadeIn(screenContainer, 0.2f);
            Stagger(items, Mathf.Min(step, 0.45f / items.Count), 0.04f, 16f);
        }
    }
}
