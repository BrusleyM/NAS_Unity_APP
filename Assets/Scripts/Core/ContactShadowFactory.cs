using UnityEngine;
using UnityEngine.Rendering;

namespace NAS.Core
{
    /// <summary>
    /// Attaches a simple soft "blob" contact shadow under a placed AR
    /// object's real footprint. Not a real-time shadow - there's no ground
    /// mesh to receive one against a live camera feed - this is the standard
    /// mobile-AR technique instead: a flat, alpha-blended radial-gradient
    /// quad sized from the object's own renderer bounds, parented directly
    /// to it so gestures (drag/rotate/pinch-scale in CarManipulationController)
    /// carry the shadow along automatically with zero extra wiring. Makes any
    /// small residual placement/tracking offset read as far less obviously
    /// "floating."
    ///
    /// Callers are responsible for calling this exactly once per fresh
    /// instance - see the two call sites (SelectedCarModelLoader for the real
    /// downloaded car, ObjectPlacerController for the placeholder cube
    /// fallback) for why each is already naturally safe from duplicates.
    /// </summary>
    public static class ContactShadowFactory
    {
        private const string ShadowObjectName = "Contact Shadow (generated)";

        public static void Attach(GameObject target)
        {
            if (target == null || target.transform.Find(ShadowObjectName) != null)
                return;

            Bounds? bounds = ComputeRendererBounds(target);
            if (bounds == null)
                return; // Nothing rendered yet - nothing to size a shadow against.

            var shadow = GameObject.CreatePrimitive(PrimitiveType.Quad);
            shadow.name = ShadowObjectName;
            Object.Destroy(shadow.GetComponent<Collider>()); // Purely visual - never wanted for raycasts/physics.

            shadow.transform.SetParent(target.transform, worldPositionStays: false);
            shadow.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Quad faces +Z by default - lay it flat, facing up.

            // Sized off the real footprint (diagonal of the XZ extents), not
            // a fixed guess - a compact car and a full SUV shouldn't get the
            // same shadow size. Bounds are in world space, so divide out the
            // target's current scale to get a LOCAL scale that still looks
            // right after pinch-scaling changes the target's world size.
            float footprintDiameter = new Vector2(bounds.Value.size.x, bounds.Value.size.z).magnitude;
            float localFootprint = footprintDiameter / Mathf.Max(target.transform.lossyScale.x, 0.0001f);
            shadow.transform.localScale = Vector3.one * localFootprint * 0.7f; // Smaller than the full footprint reads as more grounded than a shadow that exactly matches or exceeds it.

            Vector3 localCenter = target.transform.InverseTransformPoint(new Vector3(bounds.Value.center.x, bounds.Value.min.y, bounds.Value.center.z));
            shadow.transform.localPosition = new Vector3(localCenter.x, localCenter.y + 0.002f, localCenter.z); // Nudged up slightly to avoid z-fighting with the real floor.

            var renderer = shadow.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.material = BuildShadowMaterial();
        }

        /// <summary>Combined world-space renderer bounds of target and its children, or null if it has no renderers yet. Also used by ObjectPlacerController to size placement clearance from the car's real footprint.</summary>
        public static Bounds? ComputeRendererBounds(GameObject target)
        {
            var renderers = target.GetComponentsInChildren<Renderer>();
            if (renderers.Length == 0)
                return null;

            Bounds combined = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
                combined.Encapsulate(renderers[i].bounds);
            return combined;
        }

        private static Material BuildShadowMaterial()
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit");
            var material = new Material(shader)
            {
                renderQueue = (int)RenderQueue.Transparent
            };
            material.SetFloat("_Surface", 1f); // Transparent
            material.SetFloat("_Blend", 0f); // Alpha
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.SetOverrideTag("RenderType", "Transparent");
            material.SetTexture("_BaseMap", BuildRadialGradientTexture());
            material.SetColor("_BaseColor", Color.white); // Alpha lives in the texture; color stays neutral so the texture's own black-with-falloff-alpha does the work.
            return material;
        }

        private static Texture2D BuildRadialGradientTexture()
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp
            };
            Vector2 center = new Vector2(size - 1, size - 1) * 0.5f;
            float maxDist = center.magnitude;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center) / maxDist;
                    float alpha = Mathf.Clamp01(1f - dist);
                    alpha *= alpha; // Eases the falloff so the center reads solid and the edge fades out gently rather than linearly.
                    // Capped at 50% opacity even at dead center - a fake
                    // shadow that goes fully opaque looks like a black hole,
                    // not a soft shadow.
                    texture.SetPixel(x, y, new Color(0f, 0f, 0f, alpha * 0.5f));
                }
            }
            texture.Apply();
            return texture;
        }
    }
}
