using System.Collections.Generic;
using NAS.Core.Events;
using UnityEngine;

namespace NAS.Core
{
    // Switches the placed car between its Wheels_A/B and Interior_A/B meshes.
    public class CarVariantSwitcher : MonoBehaviour
    {
        private readonly Dictionary<string, string> _chosen = new Dictionary<string, string>();

        private void OnEnable() => EventBus.Subscribe<ModelVariantSelectedEvent>(HandleSelected);

        private void OnDisable() => EventBus.Unsubscribe<ModelVariantSelectedEvent>(HandleSelected);

        private void HandleSelected(ModelVariantSelectedEvent evt) => Select(evt.Group, evt.Variant);

        public string Chosen(string group) => _chosen.TryGetValue(group, out var variant) ? variant : ModelVariantRules.DefaultVariant;

        public List<string> Variants(string group)
        {
            var found = new SortedSet<string>();
            foreach (var node in GetComponentsInChildren<Transform>(true))
                if (ModelVariantRules.TryGetVariant(node.name, out var nodeGroup, out var variant) && nodeGroup == group)
                    found.Add(variant);
            return new List<string>(found);
        }

        public bool Select(string group, string variant)
        {
            if (!Variants(group).Contains(variant)) return false;

            _chosen[group] = variant;
            foreach (var node in GetComponentsInChildren<Transform>(true))
            {
                var show = ModelVariantRules.ShouldShow(node.name, group, variant);
                if (show.HasValue) node.gameObject.SetActive(show.Value);
            }
            return true;
        }

        // The rim or leather colour stands for the variant on its button.
        public Color? Preview(string group, string variant)
        {
            Color? first = null;
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                if (!BelongsTo(renderer.transform, group, variant)) continue;
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || !TryReadColour(material, out var colour)) continue;
                    if (material.name.Contains("Rim") || material.name.Contains("Leather")) return colour;
                    first = first ?? colour;
                }
            }
            return first;
        }

        private bool BelongsTo(Transform node, string group, string variant)
        {
            for (var t = node; t != null; t = t.parent)
            {
                if (ModelVariantRules.TryGetVariant(t.name, out var g, out var v) && g == group && v == variant) return true;
                if (t == transform) break;
            }
            return false;
        }

        private static bool TryReadColour(Material material, out Color colour)
        {
            foreach (var property in new[] { "baseColorFactor", "_BaseColor", "_Color" })
            {
                if (!material.HasProperty(property)) continue;
                colour = material.GetColor(property);
                return true;
            }
            colour = default;
            return false;
        }
    }
}
