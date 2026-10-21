using NAS.Core.Events;
using UnityEngine;

namespace NAS.Core
{
    public class CarTrimController : MonoBehaviour
    {
        private readonly TrimGlow _glow = new TrimGlow();
        private CarComponents _components;

        private void OnEnable() => EventBus.Subscribe<TrimStyleSelectedEvent>(HandleSelected);

        private void OnDisable() => EventBus.Unsubscribe<TrimStyleSelectedEvent>(HandleSelected);

        private void OnDestroy() => _glow.Restore();

        private void HandleSelected(TrimStyleSelectedEvent evt)
        {
            _components = _components != null ? _components : GetComponent<CarComponents>();
            if (_components == null) return;

            TrimStyle? style = evt.Hex == null ? (TrimStyle?)null : new TrimStyle(string.Empty, evt.Hex, (TrimMode)evt.Mode, evt.Intensity);
            _glow.Apply(_components.Trim, style);
        }
    }
}
