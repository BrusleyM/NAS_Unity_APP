using UnityEngine;
using UnityEngine.XR.ARFoundation;

namespace NAS.Core
{
    /// <summary>
    /// Feeds ARKit's real-time light estimate into a real Unity Light, so
    /// the placed car's lit materials respond to the actual room lighting
    /// instead of always looking like a flat CG object pasted onto the live
    /// camera feed. Prefers the richer directional estimate (direction +
    /// color + intensity) when the device/session provides one, falling back
    /// to the simpler ambient brightness/color-temperature estimate - which
    /// is available far more often - when it doesn't.
    ///
    /// Self-discovers ARCameraManager via GetComponent - meant to live on
    /// the AR camera. _targetLight is whatever directional light already
    /// lights the scene's content.
    /// </summary>
    [RequireComponent(typeof(ARCameraManager))]
    public class ArLightEstimationController : MonoBehaviour
    {
        [SerializeField] private Light _targetLight;
        // ARKit's mainLightIntensityLumens is on a very different scale than
        // Unity's Light.intensity - this converts lumens into a reasonable
        // starting intensity. Not derived from any spec, just a sane default
        // to retune once this is actually seen running on a device.
        [SerializeField] private float _lumensToIntensity = 1f / 1000f;

        private ARCameraManager _cameraManager;

        private void Awake()
        {
            _cameraManager = GetComponent<ARCameraManager>();
            _cameraManager.requestedLightEstimation =
                LightEstimation.AmbientIntensity | LightEstimation.AmbientColor |
                LightEstimation.MainLightDirection | LightEstimation.MainLightIntensity;

            if (_targetLight != null)
                _targetLight.useColorTemperature = true;
        }

        private void OnEnable()
        {
            _cameraManager.frameReceived += OnFrameReceived;
        }

        private void OnDisable()
        {
            _cameraManager.frameReceived -= OnFrameReceived;
        }

        private void OnFrameReceived(ARCameraFrameEventArgs args)
        {
            if (_targetLight == null)
                return;

            var estimate = args.lightEstimation;

            if (estimate.mainLightDirection.HasValue && estimate.mainLightColor.HasValue)
            {
                _targetLight.transform.rotation = Quaternion.LookRotation(estimate.mainLightDirection.Value);
                _targetLight.color = estimate.mainLightColor.Value;
                if (estimate.mainLightIntensityLumens.HasValue)
                    _targetLight.intensity = estimate.mainLightIntensityLumens.Value * _lumensToIntensity;
            }
            else if (estimate.averageBrightness.HasValue)
            {
                _targetLight.intensity = estimate.averageBrightness.Value;
                if (estimate.averageColorTemperature.HasValue)
                    _targetLight.colorTemperature = estimate.averageColorTemperature.Value;
            }
        }
    }
}
