using System.Linq;
using System.Collections.Generic;
using NAS.Core;
using NUnit.Framework;
using UnityEngine;

namespace NAS.Tests
{
    public class CarVariantSwitcherTests
    {
        private GameObject _car;

        [TearDown]
        public void TearDown()
        {
            if (_car != null) Object.DestroyImmediate(_car);
        }

        private static GameObject Child(Transform parent, string name)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent);
            return go;
        }

        private CarVariantSwitcher NewCar(bool withVariants = true)
        {
            _car = new GameObject("Car");
            Child(_car.transform, "Body");
            if (withVariants)
            {
                foreach (var v in new[] { "A", "B" })
                {
                    Child(Child(_car.transform, $"Wheels_{v}").transform, $"Wheels_{v}_FL");
                    Child(Child(_car.transform, $"Interior_{v}").transform, $"Interior_{v}_Seat_FL");
                }
                var door = Child(_car.transform, "Door_FL");
                Child(door.transform, "Interior_A_DoorCard_FL");
                Child(door.transform, "Interior_B_DoorCard_FL");
                // The loader hides all but the default variant first.
                foreach (var t in _car.GetComponentsInChildren<Transform>(true))
                    if (ModelVariantRules.IsHiddenByDefault(t.name)) t.gameObject.SetActive(false);
            }
            return _car.AddComponent<CarVariantSwitcher>();
        }

        private bool Active(string name) => _car.GetComponentsInChildren<Transform>(true)
            .Where(t => t.name == name).Single().gameObject.activeSelf;

        [Test]
        public void ACarListsTheVariantsItHasForEachGroup()
        {
            var switcher = NewCar();
            CollectionAssert.AreEqual(new[] { "A", "B" }, switcher.Variants("Wheels"));
            CollectionAssert.AreEqual(new[] { "A", "B" }, switcher.Variants("Interior"));
        }

        [Test]
        public void ACarWithoutVariantsListsNone()
        {
            var switcher = NewCar(withVariants: false);
            Assert.IsEmpty(switcher.Variants("Wheels"));
            Assert.IsEmpty(switcher.Variants("Interior"));
        }

        [Test]
        public void TheDefaultIsAUntilSomethingElseIsChosen()
        {
            var switcher = NewCar();
            Assert.AreEqual("A", switcher.Chosen("Wheels"));
        }

        [Test]
        public void ChoosingBShowsBAndHidesAForTheWheels()
        {
            var switcher = NewCar();

            Assert.IsTrue(switcher.Select("Wheels", "B"));

            Assert.IsTrue(Active("Wheels_B"));
            Assert.IsTrue(Active("Wheels_B_FL"));
            Assert.IsFalse(Active("Wheels_A"));
            Assert.IsFalse(Active("Wheels_A_FL"));
            Assert.AreEqual("B", switcher.Chosen("Wheels"));
        }

        [Test]
        public void TheInteriorSwitchesTheDoorCardsToo()
        {
            var switcher = NewCar();

            switcher.Select("Interior", "B");

            Assert.IsTrue(Active("Interior_B_Seat_FL"));
            Assert.IsTrue(Active("Interior_B_DoorCard_FL"));
            Assert.IsFalse(Active("Interior_A_Seat_FL"));
            Assert.IsFalse(Active("Interior_A_DoorCard_FL"));
        }

        [Test]
        public void ChoosingOneGroupLeavesTheOtherAlone()
        {
            var switcher = NewCar();

            switcher.Select("Interior", "B");

            Assert.IsTrue(Active("Wheels_A"));
            Assert.IsFalse(Active("Wheels_B"));
            Assert.IsTrue(Active("Body"));
        }

        [Test]
        public void ChoosingAGoesBack()
        {
            var switcher = NewCar();
            switcher.Select("Wheels", "B");

            switcher.Select("Wheels", "A");

            Assert.IsTrue(Active("Wheels_A"));
            Assert.IsFalse(Active("Wheels_B"));
        }

        [Test]
        public void AVariantTheCarDoesNotHaveIsRefusedAndNothingChanges()
        {
            var switcher = NewCar();

            Assert.IsFalse(switcher.Select("Wheels", "C"));

            Assert.IsTrue(Active("Wheels_A"));
            Assert.AreEqual("A", switcher.Chosen("Wheels"));
        }

        [Test]
        public void ANonexistentGroupIsRefused()
        {
            Assert.IsFalse(NewCar().Select("Engine", "B"));
        }

        [Test]
        public void TheButtonColourComesFromTheVariantsRimOrLeather()
        {
            var switcher = NewCar();
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard") ?? Shader.Find("Sprites/Default");
            var rim = new Material(shader) { name = "Mat_Rim_B", color = new Color(0.2f, 0.4f, 0.6f) };
            var wheel = _car.GetComponentsInChildren<Transform>(true).Single(t => t.name == "Wheels_B_FL").gameObject;
            wheel.AddComponent<MeshRenderer>().sharedMaterial = rim;

            var preview = switcher.Preview("Wheels", "B");

            Assert.IsTrue(preview.HasValue);
            Assert.AreEqual(0.4f, preview.Value.g, 0.01f);
            Assert.IsFalse(switcher.Preview("Wheels", "A").HasValue);
            Object.DestroyImmediate(rim);
        }
    }
}
