using System.Collections.Generic;
using NAS.Core;
using NUnit.Framework;
using UnityEngine;

namespace NAS.Tests
{
    public class TrimGlowTests
    {
        private readonly List<Object> _made = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (var o in _made) if (o != null) Object.DestroyImmediate(o);
            _made.Clear();
        }

        private static Shader LitShader() => Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");

        private Renderer Trim(string name, Material original)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.GetComponent<Renderer>().sharedMaterial = original;
            _made.Add(go);
            return go.GetComponent<Renderer>();
        }

        private Material Original(string name)
        {
            var material = new Material(LitShader()) { name = name };
            _made.Add(material);
            return material;
        }

        private static readonly TrimStyle Cyan = new TrimStyle("Neon Cyan", "#00D4FF", TrimMode.Steady, 2.5f);

        [Test]
        public void TheShaderIsPartOfTheBuildAndCompiles()
        {
            var shader = Resources.Load<Shader>(TrimGlow.ShaderPath);

            Assert.IsNotNull(shader, "Resources/Shaders/TrimGlow.shader is missing");
            Assert.AreEqual("NAS/TrimGlow", shader.name);
            Assert.IsTrue(shader.isSupported);
        }

        [Test]
        public void AStyleReplacesTheTrimMaterialsWithTheGlowShader()
        {
            var a = Trim("Trim_Badges", Original("Mat_Trim_Badges"));
            var glow = new TrimGlow();

            var swapped = glow.Apply(new[] { a }, Cyan);

            Assert.AreEqual(1, swapped);
            Assert.AreEqual("NAS/TrimGlow", a.sharedMaterial.shader.name);
            glow.Restore();
        }

        [Test]
        public void PartsThatShareAMaterialShareTheReplacement()
        {
            var shared = Original("Mat_BlackGloss");
            var a = Trim("Trim_MirrorCap_FL", shared);
            var b = Trim("Trim_MirrorCap_FR", shared);
            var glow = new TrimGlow();

            glow.Apply(new[] { a, b }, Cyan);

            Assert.AreSame(a.sharedMaterial, b.sharedMaterial);
            glow.Restore();
        }

        [Test]
        public void TheStyleSetsTheGlowColourModeAndIntensity()
        {
            var a = Trim("Trim_Grille", Original("Mat_Trim_Grille"));
            var glow = new TrimGlow();

            glow.Apply(new[] { a }, new TrimStyle("Magenta", "#FF2BD6", TrimMode.Pulse, 3f));

            var material = a.sharedMaterial;
            Assert.AreEqual(1f, material.GetFloat("_Mode"));
            Assert.AreEqual(3f, material.GetFloat("_GlowIntensity"));
            Assert.AreEqual(1f, material.GetColor("_GlowColor").r, 0.01f);
            glow.Restore();
        }

        [Test]
        public void AnotherStyleChangesTheSameMaterialsInsteadOfPilingUpNewOnes()
        {
            var a = Trim("Trim_Badges", Original("Mat_Trim_Badges"));
            var glow = new TrimGlow();

            glow.Apply(new[] { a }, Cyan);
            var first = a.sharedMaterial;
            glow.Apply(new[] { a }, new TrimStyle("Aurora", "#2BFFB0", TrimMode.Flow, 2.5f));

            Assert.AreSame(first, a.sharedMaterial);
            Assert.AreEqual(2f, a.sharedMaterial.GetFloat("_Mode"));
            glow.Restore();
        }

        [Test]
        public void OriginalPutsTheCarsOwnTrimBack()
        {
            var original = Original("Mat_Trim_Badges");
            var a = Trim("Trim_Badges", original);
            var glow = new TrimGlow();

            glow.Apply(new[] { a }, Cyan);
            glow.Apply(new[] { a }, TrimStyles.All[0]);

            Assert.AreSame(original, a.sharedMaterial);
        }

        [Test]
        public void RestoringWhenNothingWasChangedDoesNothing()
        {
            var original = Original("Mat_Trim_Badges");
            var a = Trim("Trim_Badges", original);

            new TrimGlow().Apply(new[] { a }, null);

            Assert.AreSame(original, a.sharedMaterial);
        }
    }
}
