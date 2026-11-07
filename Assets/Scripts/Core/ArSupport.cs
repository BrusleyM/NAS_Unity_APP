namespace NAS.Core
{
    /// <summary>Why AR cannot start on this phone, if it cannot.</summary>
    public enum ArSupportProblem
    {
        None,
        CameraPermissionDenied,
        DeviceNotSupported,
        ServicesMissing,
    }

    /// <summary>
    /// What the customer reads when AR cannot start, and the last answer of the check made when AR opens. No Unity, no UI: unit-tested.
    /// Without this the camera view stays black and nothing says why (no ARCore on the phone, Google Play Services for AR missing or out
    /// of date, camera refused).
    /// </summary>
    public static class ArSupport
    {
        /// <summary>The last check. Kept here so the AR screen shows it even when the answer came before the screen was listening.</summary>
        public static ArSupportProblem Last { get; private set; }

        public static void Report(ArSupportProblem problem) => Last = problem;

        public static string MessageFor(ArSupportProblem problem)
        {
            switch (problem)
            {
                case ArSupportProblem.CameraPermissionDenied:
                    return "Camera access is needed to place the car in your space. Allow the camera for this app in your phone's settings, then open AR again.";
                case ArSupportProblem.DeviceNotSupported:
                    return "This phone does not support the AR needed to place the car. You can still browse the cars.";
                case ArSupportProblem.ServicesMissing:
                    return "Google Play Services for AR is missing or out of date. Install or update it from the Play Store, then open AR again.";
                default:
                    return null;
            }
        }
    }
}
