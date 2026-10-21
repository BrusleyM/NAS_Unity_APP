using NUnit.Framework;
using UnityEditor;
using UnityEngine.UIElements;

namespace NAS.Tests
{
    // The controller finds its controls by name; this catches a rename in the UXML that would silently break the sheet.
    public class CustomizeSheetLayoutTests
    {
        private static VisualElement Load()
        {
            var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>("Assets/UI Toolkit/UI Docs/ArViewportScreen.uxml");
            Assert.IsNotNull(asset);
            return asset.Instantiate();
        }

        [TestCase("category-wheel")]
        [TestCase("category-paint")]
        [TestCase("category-trims")]
        [TestCase("category-interior")]
        public void EachCategoryHasItsButton(string name)
        {
            Assert.IsNotNull(Load().Q<Button>(name), name);
        }

        [Test]
        public void ThereIsNoDashboardButtonAndNoComingSoon()
        {
            var root = Load();
            Assert.IsNull(root.Q<Button>("category-dashboard"));
            Assert.IsNull(root.Query<Label>().Where(l => l.text == "Coming soon").First());
        }

        [Test]
        public void TheSheetHasASwatchRowAndACaption()
        {
            var root = Load();
            Assert.IsNotNull(root.Q<VisualElement>("swatch-row"));
            Assert.IsNotNull(root.Q<Label>("swatch-caption"));
        }
    }
}
