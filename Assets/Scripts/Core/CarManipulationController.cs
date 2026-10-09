using System.Collections.Generic;
using NAS.Core.Events;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.EnhancedTouch;
using UnityEngine.UIElements;
using Touch = UnityEngine.InputSystem.EnhancedTouch.Touch;

namespace NAS.Core
{
    /// <summary>
    /// Owns every way the placed AR car's transform can be manipulated after
    /// placement: one-finger drag to reposition, two-finger pinch to scale,
    /// and the rotation slider in the AR viewport UI (ArViewportController
    /// owns the slider control itself, this just applies the values it
    /// raises via RotationSliderChangedEvent - purely event-driven, never
    /// talks to ArViewportController or ObjectPlacerController directly).
    /// Reports interaction counts via GestureCountsUpdatedEvent for
    /// ObjectPlacerController to fold into the ArSession telemetry it owns
    /// sending, and the live scale ratio via CarScaleChangedEvent for
    /// ArViewportController's "Pinch to scale [1:x]" hint label.
    ///
    /// Two-finger twist-to-rotate was deliberately replaced by the slider -
    /// real users found twisting hard to do accurately. Deliberately does
    /// NOT read raw touches for rotation at all any more.
    ///
    /// Touch handling is event-driven (EnhancedTouch's onFingerDown/Move/Up),
    /// not a per-frame Update() poll - the only Update() in this class is
    /// wrapped in #if UNITY_EDITOR as a mouse-drag fallback for testing, so
    /// it doesn't exist at all (no per-frame dispatch overhead whatsoever)
    /// on the real target platform, a touchscreen device.
    /// </summary>
    public class CarManipulationController : MonoBehaviour
    {
        [Header("Gesture Settings")]
        // A gesture only counts once its cumulative movement crosses this
        // threshold - guards against counting incidental jitter (a finger
        // that barely moved while lifting off, or a slider nudge) as a real
        // reposition/scale/rotation interaction.
        [SerializeField] private float _repositionThresholdMeters = 0.03f;
        [SerializeField] private float _scaleThresholdRatio = 0.05f;
        [SerializeField] private float _rotationThresholdDegrees = 8f;
        // 1 = real-world size, never below it - shrinking the car below its
        // real dimensions defeats the point of an AR showroom placement
        // (seeing it at true scale). Scaling UP past 1 is still allowed, for
        // inspecting details up close.
        [SerializeField] private float _minScaleMultiplier = 1f;
        [SerializeField] private float _maxScaleMultiplier = 2.5f;

        // Used only to hit-test whether a given touch started on a UI
        // element (any button, slider, or the Customize sheet) - see
        // IsScreenPositionOverUi. Optional: if unassigned, that check is
        // simply skipped rather than throwing, same graceful-degradation as
        // this project's other optional dependencies.
        [SerializeField] private UIDocument _uiDocument;

        private GameObject _placedInstance;
        private Vector3 _originalLocalScale;
        private float _scaleMultiplier = 1f;
        private bool _isCustomizeSheetOpen;
        private bool _isRotationSliderActive;
        private bool _isVerticalSliderActive;

        // Fingers whose FIRST touch-down landed on a UI element - tracked by
        // finger index rather than re-checking every frame, so a drag that
        // starts on a button and then (however unlikely) drifts off its
        // bounds while still held down stays suppressed for its whole
        // lifetime, instead of flip-flopping frame to frame. This is the
        // general case of the same problem RotationSliderGrabbedEvent and
        // CustomizeSheetToggledEvent each solve for their own specific
        // control - this catches every OTHER button (Settings, Reset,
        // Back, Confirm, category buttons, swatches) without needing a
        // dedicated event wired up per button, present or future.
        private readonly HashSet<int> _fingersStartedOverUi = new HashSet<int>();

        private int _repositionCount;
        private int _scaleCount;
        private int _rotationCount;

        // Per-gesture-segment state, reset whenever the active touch count
        // changes (drag) or the segment restarts (pinch/rotation) - "segment"
        // means one continuous interaction, not the whole AR visit.
        private bool _hasLastDragHit;
        private Vector3 _lastLocalDragHit;
        // The anchor's pose at the moment THIS drag gesture started, frozen
        // for the gesture's whole duration - see HandleReposition for why.
        private Vector3 _gestureAnchorPosition;
        private Quaternion _gestureAnchorRotation;
        private float _repositionAccumThisGesture;
        private bool _repositionCountedThisGesture;

        private bool _hasLastPinchDistance;
        private float _lastPinchDistance;
        private float _scaleAtGestureStart;
        private bool _scaleCountedThisGesture;

        private float _lastSliderDegrees;
        private float _rotationAccumThisGesture;
        private bool _rotationCountedThisGesture;

        private void OnEnable()
        {
            EventBus.Subscribe<CarPlacedEvent>(OnCarPlaced);
            EventBus.Subscribe<EnterArRequestedEvent>(OnEnterAr);
            EventBus.Subscribe<CustomizeSheetToggledEvent>(OnCustomizeSheetToggled);
            EventBus.Subscribe<RotationSliderGrabbedEvent>(OnRotationSliderGrabbed);
            EventBus.Subscribe<RotationSliderChangedEvent>(OnRotationSliderChanged);
            EventBus.Subscribe<RotationSliderReleasedEvent>(OnRotationSliderReleased);
            EventBus.Subscribe<VerticalOffsetSliderGrabbedEvent>(OnVerticalOffsetSliderGrabbed);
            EventBus.Subscribe<VerticalOffsetSliderChangedEvent>(OnVerticalOffsetSliderChanged);
            EventBus.Subscribe<VerticalOffsetSliderReleasedEvent>(OnVerticalOffsetSliderReleased);
            EventBus.Subscribe<CarPositionResetRequestedEvent>(OnCarPositionReset);

            // Ref-counted internally - safe even if something else in the
            // project also enables it. This is the only place in the
            // project reading touches through EnhancedTouch specifically
            // (UnityInputProvider's tap detection uses the lower-level
            // Touchscreen.current.primaryTouch instead, which needs no
            // enabling).
            EnhancedTouchSupport.Enable();
            Touch.onFingerDown += OnFingerDown;
            Touch.onFingerMove += OnFingerMove;
            Touch.onFingerUp += OnFingerUp;
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<CarPlacedEvent>(OnCarPlaced);
            EventBus.Unsubscribe<EnterArRequestedEvent>(OnEnterAr);
            EventBus.Unsubscribe<CustomizeSheetToggledEvent>(OnCustomizeSheetToggled);
            EventBus.Unsubscribe<RotationSliderGrabbedEvent>(OnRotationSliderGrabbed);
            EventBus.Unsubscribe<RotationSliderChangedEvent>(OnRotationSliderChanged);
            EventBus.Unsubscribe<RotationSliderReleasedEvent>(OnRotationSliderReleased);

            EventBus.Unsubscribe<VerticalOffsetSliderGrabbedEvent>(OnVerticalOffsetSliderGrabbed);
            EventBus.Unsubscribe<VerticalOffsetSliderChangedEvent>(OnVerticalOffsetSliderChanged);
            EventBus.Unsubscribe<VerticalOffsetSliderReleasedEvent>(OnVerticalOffsetSliderReleased);
            EventBus.Unsubscribe<CarPositionResetRequestedEvent>(OnCarPositionReset);

            Touch.onFingerDown -= OnFingerDown;
            Touch.onFingerMove -= OnFingerMove;
            Touch.onFingerUp -= OnFingerUp;
            EnhancedTouchSupport.Disable();
        }

        private void OnEnterAr(EnterArRequestedEvent evt) => ResetSession();

        private void OnCarPlaced(CarPlacedEvent evt)
        {
            _placedInstance = evt.Instance;
            _originalLocalScale = _placedInstance.transform.localScale;
            _scaleMultiplier = 1f;
            _lastSliderDegrees = 0f;
            ResetDragState();
            ResetPinchState();
            ResetRotationGestureState();
            EventBus.Publish(new CarScaleChangedEvent(_scaleMultiplier));
        }

        private void ResetSession()
        {
            _placedInstance = null;
            _repositionCount = 0;
            _scaleCount = 0;
            _rotationCount = 0;
            _scaleMultiplier = 1f;
            _lastSliderDegrees = 0f;
            ResetDragState();
            ResetPinchState();
            ResetRotationGestureState();
            PublishGestureCounts();
            EventBus.Publish(new CarScaleChangedEvent(_scaleMultiplier));
        }

        private void ResetDragState()
        {
            _hasLastDragHit = false;
            _repositionAccumThisGesture = 0f;
            _repositionCountedThisGesture = false;
        }

        private void ResetPinchState()
        {
            _hasLastPinchDistance = false;
            _scaleAtGestureStart = _scaleMultiplier;
            _scaleCountedThisGesture = false;
        }

        private void ResetRotationGestureState()
        {
            _rotationAccumThisGesture = 0f;
            _rotationCountedThisGesture = false;
        }

        // Suspend touch manipulation while the Customize sheet is open -
        // otherwise tapping/dragging across a paint swatch would also read
        // as a reposition drag on the car underneath it.
        private void OnCustomizeSheetToggled(CustomizeSheetToggledEvent evt)
        {
            _isCustomizeSheetOpen = evt.IsOpen;
            if (_isCustomizeSheetOpen)
            {
                ResetDragState();
                ResetPinchState();
            }
        }

        // The slider is a real UI Toolkit control, so a tap that lands on
        // it never gets consumed by anything else in the UI - but the SAME
        // physical touch is still visible to Update()'s raw Touchscreen
        // read below, which has no idea a UI element is also handling it.
        // Without this suspension, dragging the slider also drags the car
        // underneath: most noticeable once the slider hits its -180/180
        // limit and stops producing RotationSliderChangedEvents, but the
        // finger is still moving - that continued movement was leaking
        // straight into HandleReposition.
        private void OnRotationSliderGrabbed(RotationSliderGrabbedEvent evt)
        {
            _isRotationSliderActive = true;
            ResetDragState();
            ResetPinchState();
        }

        private void OnRotationSliderChanged(RotationSliderChangedEvent evt)
        {
            if (_placedInstance == null)
                return;

            float delta = Mathf.Abs(Mathf.DeltaAngle(_lastSliderDegrees, evt.Degrees));
            // Absolute, not cumulative - the slider's own position is the
            // single source of truth for the car's current rotation, unlike
            // the old twist gesture which only ever applied deltas. Negated:
            // dragging the slider right (increasing degrees) should turn the
            // car the same way it turns for a swipe-right in AR, which is
            // the opposite sign from a raw Y-axis Euler increase given how
            // the camera views the placed car.
            _placedInstance.transform.localRotation = Quaternion.Euler(0f, -evt.Degrees, 0f);
            _rotationAccumThisGesture += delta;
            _lastSliderDegrees = evt.Degrees;

            if (!_rotationCountedThisGesture && _rotationAccumThisGesture >= _rotationThresholdDegrees)
            {
                _rotationCount++;
                _rotationCountedThisGesture = true;
                PublishGestureCounts();
            }
        }

        private void OnRotationSliderReleased(RotationSliderReleasedEvent evt)
        {
            _isRotationSliderActive = false;
            ResetRotationGestureState();
        }

        // Same "raw touch reading fights the slider" reasoning as the
        // rotation slider's grab handler above.
        private void OnVerticalOffsetSliderGrabbed(VerticalOffsetSliderGrabbedEvent evt)
        {
            _isVerticalSliderActive = true;
            ResetDragState();
            ResetPinchState();
        }

        private void OnVerticalOffsetSliderChanged(VerticalOffsetSliderChangedEvent evt)
        {
            if (_placedInstance == null)
                return;

            // Absolute, not cumulative - same convention as the rotation
            // slider: the slider's own position is the single source of
            // truth for the car's vertical offset from its anchor. Only the
            // Y component is touched; whatever X/Z offset dragging has
            // produced is left exactly as it is.
            Vector3 local = _placedInstance.transform.localPosition;
            _placedInstance.transform.localPosition = new Vector3(local.x, evt.OffsetMeters, local.z);
        }

        private void OnVerticalOffsetSliderReleased(VerticalOffsetSliderReleasedEvent evt)
        {
            _isVerticalSliderActive = false;
        }

        // Clears drag (X/Z) and vertical-slider (Y) offsets back to exactly
        // where the anchor itself sits. Deliberately leaves rotation and
        // scale untouched - each already has its own dedicated control, and
        // "reset position" shouldn't silently also undo those.
        private void OnCarPositionReset(CarPositionResetRequestedEvent evt)
        {
            if (_placedInstance == null)
                return;

            _placedInstance.transform.localPosition = Vector3.zero;
            ResetDragState();
        }

        // Decides UI-vs-car ownership right here, at touch-down, rather than
        // re-checking every move - see _fingersStartedOverUi's comment for why.
        private void OnFingerDown(Finger finger)
        {
            if (IsScreenPositionOverUi(finger.screenPosition))
                _fingersStartedOverUi.Add(finger.index);
        }

        // Only ever does anything once something's placed. Reads
        // Touch.activeTouches (this frame's full active set), not just the
        // finger that triggered this particular callback, so a two-finger
        // pinch/rotate always sees both current positions regardless of
        // which finger's movement fired the event.
        private void OnFingerMove(Finger finger)
        {
            if (_placedInstance == null || _isCustomizeSheetOpen || _isRotationSliderActive || _isVerticalSliderActive)
                return;

            var activeTouches = Touch.activeTouches;
            for (int i = 0; i < activeTouches.Count; i++)
            {
                if (_fingersStartedOverUi.Contains(activeTouches[i].finger.index))
                    return; // Any relevant finger started on a button/slider/sheet - never treat this as a car gesture.
            }

            if (activeTouches.Count == 1)
            {
                ResetPinchState();
                HandleReposition(activeTouches[0].screenPosition);
            }
            else if (activeTouches.Count >= 2)
            {
                ResetDragState();
                HandlePinch(activeTouches[0].screenPosition, activeTouches[1].screenPosition);
            }
        }

        // A finger lifting changes the active count for whichever gesture
        // was in progress (2->1 means the pinch/rotate segment is over, even
        // though a drag could still start fresh with the remaining finger;
        // 1->0 means everything stops) - reset both unconditionally rather
        // than trying to infer which one to keep, since either can restart
        // cleanly from its own next onFingerMove.
        private void OnFingerUp(Finger finger)
        {
            _fingersStartedOverUi.Remove(finger.index);
            ResetDragState();
            ResetPinchState();
        }

        // Hit-tests a screen position against the AR viewport's own UI
        // panel. panel.Pick() already respects picking-mode="Ignore" (which
        // is why the root VisualElement and its purely-decorative containers
        // don't block this), so this correctly returns null for empty AR
        // space and non-null for any real button, slider, or sheet content -
        // without needing to know about any specific control.
        private bool IsScreenPositionOverUi(Vector2 screenPosition)
        {
            if (_uiDocument == null || _uiDocument.rootVisualElement == null)
                return false;

            IPanel panel = _uiDocument.rootVisualElement.panel;
            if (panel == null)
                return false;

            Vector2 panelPosition = RuntimePanelUtils.ScreenToPanel(panel, screenPosition);
            return panel.Pick(panelPosition) != null;
        }

#if UNITY_EDITOR
        // Editor-only mouse-drag fallback (reposition only - pinch has no
        // natural mouse equivalent, and a mouse can't fire finger events at
        // all). Wrapped in UNITY_EDITOR rather than just runtime-checking
        // Touchscreen.current, so this method - and the per-frame Update()
        // dispatch Unity gives any MonoBehaviour that defines one - doesn't
        // exist at all on the real target platform (a touchscreen device).
        // Real touch handling is fully event-driven via
        // OnFingerDown/Move/Up above regardless of build target.
        private void Update()
        {
            if (Touchscreen.current != null)
                return;
            if (_placedInstance == null || _isCustomizeSheetOpen || _isRotationSliderActive || _isVerticalSliderActive)
                return;

            if (Mouse.current != null && Mouse.current.leftButton.isPressed)
            {
                Vector2 mousePos = Mouse.current.position.ReadValue();
                if (IsScreenPositionOverUi(mousePos))
                {
                    ResetDragState();
                    return;
                }
                HandleReposition(mousePos);
            }
            else
                ResetDragState();
        }
#endif

        // The anchor's pose is deliberately re-read fresh only when a NEW
        // gesture starts, then frozen for that gesture's whole duration -
        // see TryGetFrozenLocalHit for why re-reading it every frame (the
        // first version of this fix) was itself a bug. Using the object's
        // own current position as the reference when there's no anchor
        // parent keeps the no-anchor fallback path behaving the same as
        // before this existed.
        private void HandleReposition(Vector2 screenPos)
        {
            if (_placedInstance == null)
                return;

            Transform planeReference = _placedInstance.transform.parent != null
                ? _placedInstance.transform.parent
                : _placedInstance.transform;

            if (!_hasLastDragHit)
            {
                _gestureAnchorPosition = planeReference.position;
                _gestureAnchorRotation = planeReference.rotation;
            }

            if (!TryGetFrozenLocalHit(screenPos, out var localHit))
            {
                _hasLastDragHit = false;
                return;
            }

            if (_hasLastDragHit)
            {
                Vector3 rawLocalDelta = localHit - _lastLocalDragHit;
                // The drag math above is entirely scale-agnostic - it's
                // driven by a raycast against the floor, not by the car's
                // own size - so the same finger movement always produces the
                // same real-world-meters delta regardless of zoom. At a high
                // pinch-scale multiplier (zoomed in to inspect details up
                // close) that same delta is a much bigger fraction of what's
                // on screen, so it feels wildly oversensitive. Dividing by
                // the current scale multiplier restores a consistent FEEL
                // across zoom levels - twice as zoomed in means half the
                // real-world movement per finger-pixel, so fine positioning
                // while zoomed in doesn't fling the car across the room.
                Vector3 localDelta = rawLocalDelta / Mathf.Max(_scaleMultiplier, 0.01f);
                _placedInstance.transform.localPosition += localDelta;
                _repositionAccumThisGesture += localDelta.magnitude;

                if (!_repositionCountedThisGesture && _repositionAccumThisGesture >= _repositionThresholdMeters)
                {
                    _repositionCount++;
                    _repositionCountedThisGesture = true;
                    PublishGestureCounts();
                }
            }

            _lastLocalDragHit = localHit;
            _hasLastDragHit = true;
        }

        // Raycasts against a plane built from the FROZEN gesture-start
        // anchor pose (_gestureAnchorPosition/_gestureAnchorRotation), not
        // the anchor's live pose, and expresses the hit in that same frozen
        // pose's local space.
        //
        // This went through two versions. The first read the anchor's LIVE
        // pose every frame, on the reasoning that AR Foundation's drift
        // correction could move the anchor mid-gesture and dragging against
        // a stale plane would visibly detach the car from the real floor.
        // That fixed the original bug (a translation correction mid-drag
        // got double-applied - once automatically via the transform
        // hierarchy, once again as a spurious drag delta - producing a
        // sudden snap to a distant position), but it introduced a new one:
        // a ROTATION correction (e.g. ARKit refining a plane's exact yaw as
        // it scans more of the floor) makes the SAME physical point resolve
        // to different local coordinates from one frame to the next, purely
        // because the coordinate frame itself rotated - not because
        // anything actually moved. That phantom delta could easily point
        // opposite to the real finger movement.
        //
        // Freezing the reference pose for the gesture's duration fixes both:
        // every hit point in this gesture is measured against the exact
        // same (position, rotation), so only genuine screen-space finger
        // movement produces a nonzero delta - any correction the anchor
        // undergoes mid-gesture still reaches the car exactly once, via the
        // ordinary (unfrozen) live parent transform, same as it would
        // between gestures or with no drag happening at all.
        private bool TryGetFrozenLocalHit(Vector2 screenPos, out Vector3 localHit)
        {
            localHit = default;
            if (Camera.main == null || _placedInstance == null)
                return false;

            var dragPlane = new Plane(_gestureAnchorRotation * Vector3.up, _gestureAnchorPosition);

            Ray ray = Camera.main.ScreenPointToRay(screenPos);
            // Plane.Raycast returns true (with a negative `enter`) whenever
            // the plane is behind the ray's origin along its direction, not
            // just when the ray is genuinely pointing at the floor - without
            // this check, a steep upward camera tilt (common when standing
            // close to a placed car and looking slightly up at it) can
            // produce a "hit" behind the camera, which reads as a wildly
            // wrong, sometimes seemingly axis-flipped drag delta.
            if (!dragPlane.Raycast(ray, out float enter) || enter < 0f)
                return false;

            Vector3 worldHit = ray.GetPoint(enter);
            localHit = _placedInstance.transform.parent != null
                ? Quaternion.Inverse(_gestureAnchorRotation) * (worldHit - _gestureAnchorPosition)
                : worldHit;
            return true;
        }

        private void HandlePinch(Vector2 posA, Vector2 posB)
        {
            float distance = Vector2.Distance(posA, posB);

            if (_hasLastPinchDistance && _lastPinchDistance > 0.001f)
            {
                float frameRatio = distance / _lastPinchDistance;
                float newMultiplier = Mathf.Clamp(_scaleMultiplier * frameRatio, _minScaleMultiplier, _maxScaleMultiplier);
                if (!Mathf.Approximately(newMultiplier, _scaleMultiplier))
                {
                    _scaleMultiplier = newMultiplier;
                    _placedInstance.transform.localScale = _originalLocalScale * _scaleMultiplier;
                    EventBus.Publish(new CarScaleChangedEvent(_scaleMultiplier));

                    if (!_scaleCountedThisGesture && Mathf.Abs(_scaleMultiplier - _scaleAtGestureStart) >= _scaleThresholdRatio)
                    {
                        _scaleCount++;
                        _scaleCountedThisGesture = true;
                        PublishGestureCounts();
                    }
                }
            }

            _lastPinchDistance = distance;
            _hasLastPinchDistance = true;
        }

        private void PublishGestureCounts() =>
            EventBus.Publish(new GestureCountsUpdatedEvent(_repositionCount, _scaleCount, _rotationCount));
    }
}
