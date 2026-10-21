using System.Collections.Generic;
using UnityEngine;

namespace NAS.Core
{
    // Each original material gets one replacement that keeps its texture, so the grille keeps its pattern.
    public sealed class TrimGlow
    {
        public const string ShaderPath = "Shaders/TrimGlow";

        private static readonly int BaseMap = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");
        private static readonly int GlowColor = Shader.PropertyToID("_GlowColor");
        private static readonly int GlowIntensity = Shader.PropertyToID("_GlowIntensity");
        private static readonly int Mode = Shader.PropertyToID("_Mode");

        private readonly Dictionary<Renderer, Material[]> _originals = new Dictionary<Renderer, Material[]>();
        private readonly Dictionary<Material, Material> _replacements = new Dictionary<Material, Material>();
        private Shader _shader;

        public int Apply(IEnumerable<Renderer> renderers, TrimStyle? style)
        {
            if (style == null || style.Value.IsOriginal)
            {
                Restore();
                return 0;
            }

            _shader = _shader != null ? _shader : Resources.Load<Shader>(ShaderPath);
            if (_shader == null || !ColorUtility.TryParseHtmlString(style.Value.Hex, out var glow))
                return 0;

            var swapped = 0;
            foreach (var renderer in renderers)
            {
                if (renderer == null) continue;
                if (!_originals.TryGetValue(renderer, out var originals))
                {
                    originals = renderer.sharedMaterials;
                    _originals[renderer] = originals;
                }

                var replaced = new Material[originals.Length];
                for (var i = 0; i < originals.Length; i++)
                {
                    replaced[i] = ReplacementFor(originals[i]);
                    Style(replaced[i], style.Value, glow);
                    swapped++;
                }
                renderer.sharedMaterials = replaced;
            }
            return swapped;
        }

        public void Restore()
        {
            foreach (var pair in _originals)
                if (pair.Key != null) pair.Key.sharedMaterials = pair.Value;
            _originals.Clear();

            foreach (var replacement in _replacements.Values)
            {
                if (replacement == null) continue;
                if (Application.isPlaying) Object.Destroy(replacement);
                else Object.DestroyImmediate(replacement);
            }
            _replacements.Clear();
        }

        private Material ReplacementFor(Material original)
        {
            if (original != null && _replacements.TryGetValue(original, out var existing)) return existing;

            var replacement = new Material(_shader) { name = "TrimGlow" };
            if (original != null && original.HasProperty("baseColorTexture"))
                replacement.SetTexture(BaseMap, original.GetTexture("baseColorTexture"));
            if (original != null) _replacements[original] = replacement;
            return replacement;
        }

        private static void Style(Material material, TrimStyle style, Color glow)
        {
            material.SetColor(GlowColor, glow);
            material.SetColor(BaseColor, glow * 0.18f);
            material.SetFloat(GlowIntensity, style.Intensity);
            material.SetFloat(Mode, (float)style.Mode);
        }
    }
}
