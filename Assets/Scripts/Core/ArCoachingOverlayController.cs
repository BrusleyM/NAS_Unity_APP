using System.Collections;
using NAS.Core.Events;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
#if UNITY_IOS
using UnityEngine.XR.ARKit;
#endif

namespace NAS.Core
{
    /// <summary>
    /// Drives ARKit's native coaching overlay (Apple's own "move your phone
    /// to scan the room" UI) so users get visible guidance while tracking is
    /// still initializing, instead of a bare camera feed with nothing
    /// happening. iOS/ARKit-only - the cast to ARKitSessionSubsystem is null
    /// on any other platform (Editor, etc.), which makes every call here a
    /// safe no-op there rather than needing its own guard everywhere.
    ///
    /// Directly complements ObjectPlacerController's _minPlaneTrackingSeconds
    /// gate: that gate silently rejects a tap on a too-fresh plane, this is
    /// what tells the user WHY nothing happened yet and what to do about it.
    ///
    /// Self-discovers ARSession via GetComponent - meant to live on the same
    /// GameObject as the scene's ARSession component.
    /// </summary>
    [RequireComponent(typeof(ARSession))]
    public class ArCoachingOverlayController : MonoBehaviour
    {
        private ARSession _arSession;

        private void Awake()
        {
            _arSession = GetComponent<ARSession>();
        }

        private void OnEnable()
        {
            EventBus.Subscribe<EnterArRequestedEvent>(OnEnterAr);
            StartCoroutine(ConfigureWhenReady());
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EnterArRequestedEvent>(OnEnterAr);
        }

        // Re-entering AR re-enables the (disabled, not destroyed) ARSession -
        // re-apply the coaching configuration each time rather than assuming
        // it survived, matching how every other AR-session-scoped setting in
        // this project (used planes, session tracking, etc.) resets on entry
        // instead of trusting stale state.
        private void OnEnterAr(EnterArRequestedEvent evt) => StartCoroutine(ConfigureWhenReady());

        // ARSession.subsystem isn't guaranteed to exist the instant this
        // component enables (session creation can lag a frame or two) - poll
        // briefly rather than silently giving up on the first null read.
        private IEnumerator ConfigureWhenReady()
        {
            float deadline = Time.unscaledTime + 3f;
            while (_arSession.subsystem == null && Time.unscaledTime < deadline)
                yield return null;

            TryConfigure();
        }

        private void TryConfigure()
        {
#if UNITY_IOS
            var arkitSubsystem = _arSession.subsystem as ARKitSessionSubsystem;
            if (arkitSubsystem == null || !ARKitSessionSubsystem.coachingOverlaySupported)
                return;

            arkitSubsystem.requestedCoachingGoal = ARCoachingGoal.HorizontalPlane;
            arkitSubsystem.coachingActivatesAutomatically = true;
#endif
        }
    }
}
