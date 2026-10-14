using System;
using NAS.Core.Motion;
using NUnit.Framework;

namespace NAS.Tests
{
    public class EasingTests
    {
        private static readonly Ease[] AllCurves = (Ease[])Enum.GetValues(typeof(Ease));

        [Test]
        public void EveryCurveStartsAtZeroAndEndsExactlyOnOne()
        {
            foreach (var ease in AllCurves)
            {
                Assert.AreEqual(0f, Easing.Evaluate(ease, 0f), 1e-6f, ease + " at 0");
                Assert.AreEqual(1f, Easing.Evaluate(ease, 1f), 1e-6f, ease + " at 1");
            }
        }

        [Test]
        public void ValuesOutsideTheRangeAreClamped()
        {
            foreach (var ease in AllCurves)
            {
                Assert.AreEqual(0f, Easing.Evaluate(ease, -3f), 1e-6f, ease + " below 0");
                Assert.AreEqual(1f, Easing.Evaluate(ease, 7f), 1e-6f, ease + " above 1");
            }
        }

        [Test]
        public void OutCurvesStartFastAndInCurvesStartSlow()
        {
            Assert.Greater(Easing.Evaluate(Ease.OutCubic, 0.25f), 0.25f);
            Assert.Greater(Easing.Evaluate(Ease.OutQuint, 0.25f), Easing.Evaluate(Ease.OutCubic, 0.25f));
            Assert.Less(Easing.Evaluate(Ease.InCubic, 0.25f), 0.25f);
        }

        [Test]
        public void OutCubicNeverGoesBackwardsOrPastTheTarget()
        {
            var previous = 0f;
            for (var t = 0.01f; t <= 1f; t += 0.01f)
            {
                var value = Easing.Evaluate(Ease.OutCubic, t);
                Assert.GreaterOrEqual(value, previous);
                Assert.LessOrEqual(value, 1f);
                previous = value;
            }
        }

        [Test]
        public void OutBackOvershootsThenSettlesAndSpringOscillatesAroundTheTarget()
        {
            var peak = 0f;
            for (var t = 0f; t <= 1f; t += 0.01f) peak = Math.Max(peak, Easing.Evaluate(Ease.OutBack, t));
            Assert.Greater(peak, 1.02f);
            Assert.Less(peak, 1.2f);

            var crossed = false;
            for (var t = 0f; t <= 1f; t += 0.01f) crossed |= Easing.Evaluate(Ease.Spring, t) > 1f;
            Assert.IsTrue(crossed, "a spring should swing past its target at least once");
        }

        [Test]
        public void InOutCurvesPassThroughTheMiddle()
        {
            Assert.AreEqual(0.5f, Easing.Evaluate(Ease.InOutCubic, 0.5f), 1e-5f);
            Assert.AreEqual(0.5f, Easing.Evaluate(Ease.InOutQuad, 0.5f), 1e-5f);
        }

        [Test]
        public void LerpMapsTheEasedValueOntoARange()
        {
            Assert.AreEqual(10f, Easing.Lerp(10f, 30f, 0f), 1e-6f);
            Assert.AreEqual(30f, Easing.Lerp(10f, 30f, 1f), 1e-6f);
            Assert.AreEqual(20f, Easing.Lerp(10f, 30f, 0.5f), 1e-6f);
        }
    }
}
