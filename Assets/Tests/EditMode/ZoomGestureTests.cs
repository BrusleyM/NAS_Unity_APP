using NAS.Core;
using NUnit.Framework;

namespace NAS.Tests
{
    public class ZoomGestureTests
    {
        [TestCase(1f, 1.4f)]
        [TestCase(1.5f, 2.5f)]
        public void APinchThatEndedLargerIsAZoomIn(float start, float now)
        {
            Assert.AreEqual(ZoomDirection.In, ZoomGesture.DirectionOf(start, now));
        }

        [TestCase(2f, 1.2f)]
        [TestCase(1.3f, 1f)]
        public void APinchThatEndedSmallerIsAZoomOut(float start, float now)
        {
            Assert.AreEqual(ZoomDirection.Out, ZoomGesture.DirectionOf(start, now));
        }
    }
}
