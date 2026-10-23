using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NAS.Core
{
    // The stylesheets hard-code the app's cyan and USS cannot be re-pointed from code, so once a dealership with its
    // own primary colour is chosen this swaps the cyan for it on the elements as they appear.
    public sealed class AccentThemeRunner : MonoBehaviour
    {
        private const float TickSeconds = 0.3f;

        private sealed class Property
        {
            public Func<IResolvedStyle, Color> Resolved;
            public Func<IStyle, StyleColor> Get;
            public Action<IStyle, StyleColor> Set;
        }

        private sealed class Applied
        {
            public string Classes;
            public StyleColor[] Original = new StyleColor[Properties.Length];
            public bool[] Changed = new bool[Properties.Length];
        }

        private static readonly Property[] Properties =
        {
            new Property { Resolved = r => r.color, Get = s => s.color, Set = (s, v) => s.color = v },
            new Property { Resolved = r => r.backgroundColor, Get = s => s.backgroundColor, Set = (s, v) => s.backgroundColor = v },
            new Property { Resolved = r => r.borderTopColor, Get = s => s.borderTopColor, Set = (s, v) => s.borderTopColor = v },
            new Property { Resolved = r => r.borderRightColor, Get = s => s.borderRightColor, Set = (s, v) => s.borderRightColor = v },
            new Property { Resolved = r => r.borderBottomColor, Get = s => s.borderBottomColor, Set = (s, v) => s.borderBottomColor = v },
            new Property { Resolved = r => r.borderLeftColor, Get = s => s.borderLeftColor, Set = (s, v) => s.borderLeftColor = v },
            new Property { Resolved = r => r.unityBackgroundImageTintColor, Get = s => s.unityBackgroundImageTintColor, Set = (s, v) => s.unityBackgroundImageTintColor = v },
        };

        private readonly Dictionary<VisualElement, Applied> _applied = new Dictionary<VisualElement, Applied>();
        private readonly List<VisualElement> _scratch = new List<VisualElement>();
        private string _accentHex;
        private Color _accent;

        private IEnumerator Start()
        {
            var wait = new WaitForSeconds(TickSeconds);
            while (true)
            {
                Tick();
                yield return wait;
            }
        }

        private void Tick()
        {
            var hex = GameManager.Instance != null && GameManager.Instance.SelectedDealership != null
                ? GameManager.Instance.SelectedDealership.primaryColor
                : null;
            if (hex != _accentHex)
            {
                RevertAll();
                _accentHex = hex;
                _accent = default;
                if (AccentColor.TryParse(hex, out var r, out var g, out var b))
                    _accent = new Color(r, g, b, 1f);
            }
            if (_accent.a <= 0f) return;

            foreach (var document in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
            {
                var root = document.rootVisualElement;
                if (root == null) continue;
                _scratch.Clear();
                root.Query<VisualElement>().ToList(_scratch);
                foreach (var element in _scratch)
                    Process(element);
            }
            Prune();
        }

        private void Process(VisualElement element)
        {
            var classes = string.Join(" ", element.GetClasses());
            if (_applied.TryGetValue(element, out var applied))
            {
                // A class change (selected, active...) may bring cyan back from the stylesheet, so look again.
                if (applied.Classes == classes) return;
                Revert(element, applied);
                _applied.Remove(element);
                return;
            }

            applied = null;
            var resolved = element.resolvedStyle;
            for (var i = 0; i < Properties.Length; i++)
            {
                var colour = Properties[i].Resolved(resolved);
                if (colour.a <= 0f || !AccentColor.IsAccent(colour.r, colour.g, colour.b)) continue;

                applied ??= new Applied { Classes = classes };
                applied.Original[i] = Properties[i].Get(element.style);
                applied.Changed[i] = true;
                AccentColor.Recolor(colour.r, colour.g, colour.b, _accent.r, _accent.g, _accent.b, out var r, out var g, out var b);
                Properties[i].Set(element.style, new StyleColor(new Color(r, g, b, colour.a)));
            }
            if (applied != null) _applied[element] = applied;
        }

        private static void Revert(VisualElement element, Applied applied)
        {
            for (var i = 0; i < Properties.Length; i++)
                if (applied.Changed[i]) Properties[i].Set(element.style, applied.Original[i]);
        }

        private void RevertAll()
        {
            foreach (var pair in _applied)
                Revert(pair.Key, pair.Value);
            _applied.Clear();
        }

        private void Prune()
        {
            _scratch.Clear();
            foreach (var pair in _applied)
                if (pair.Key.panel == null) _scratch.Add(pair.Key);
            foreach (var element in _scratch)
                _applied.Remove(element);
        }
    }
}
