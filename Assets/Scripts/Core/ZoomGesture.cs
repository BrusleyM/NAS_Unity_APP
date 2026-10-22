namespace NAS.Core
{
    public enum ZoomDirection
    {
        In,
        Out
    }

    public static class ZoomGesture
    {
        // A pinch that ended larger than it started zoomed in; otherwise out.
        public static ZoomDirection DirectionOf(float multiplierAtStart, float multiplierNow) =>
            multiplierNow > multiplierAtStart ? ZoomDirection.In : ZoomDirection.Out;
    }
}
