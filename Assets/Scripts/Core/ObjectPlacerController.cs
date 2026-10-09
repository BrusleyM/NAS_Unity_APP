using System;
using UnityEngine;
using NAS.Core.Events;
using NAS.Core.Interfaces;
using NAS.Core.Networking;
using System.Collections;
using System.Collections.Generic;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

namespace NAS.Core
{
    public class ObjectPlacerController : MonoBehaviour
    {
        [Header("Dependencies")]
        [SerializeField] private MonoBehaviour _inputProviderBehaviour; // Must implement IInputProvider
        [SerializeField] private MonoBehaviour _placementServiceBehaviour; // Must implement IARPlacementService
        [SerializeField] private ARPlaneManager _planeManager; // Optional: for plane visibility control
        [SerializeField] private ARSession _arSession; // Disabled/re-enabled (not destroyed) across AR entry/exit - see OnEnterAr/OnExitAr
        [SerializeField] private ARAnchorManager _anchorManager; // Anchors the placed car to the hit plane so it doesn't drift when tracking is corrected - see TryPlaceObject

        [Header("Settings")]
        [SerializeField] private bool _preventMultiplePerPlane = true; // Enable/disable plane tracking
        // Extra buffer ADDED to the car's own real footprint radius when
        // pushing placement away from the camera - see TryPlaceObject. Not a
        // standalone distance by itself any more: a single fixed number
        // can't be right for every vehicle size (a compact and a full-size
        // SUV need different clearance), so this is only the margin beyond
        // whatever the actual placed car's footprint already requires.
        [SerializeField] private float _extraClearanceFromUser = 0.3f;
        // A plane detected only a moment ago hasn't had enough viewpoints for
        // ARKit's pose estimate to converge yet - accepting a tap on it
        // immediately anchors the car to a low-confidence initial guess,
        // which is exactly what produced a large correction once the user
        // moved around (see the "anchor jumped to couch height" report).
        // Requiring a minimum tracked duration first gives ARKit a better
        // initial fix, so any later correction is smaller.
        [SerializeField] private float _minPlaneTrackingSeconds = 1.5f;
        // How the placed car visually eases toward its anchor's corrected
        // pose instead of snapping to it instantly - see AnchorFollowSmoother.
        [SerializeField] private float _anchorFollowPositionSmoothTime = 0.35f;
        [SerializeField] private float _anchorFollowRotationDegreesPerSecond = 90f;

        private IInputProvider _inputProvider;
        private IARPlacementService _placementService;
        private Coroutine _placementCoroutine;

        // Track which planes have already been used (only if _preventMultiplePerPlane is true)
        private HashSet<TrackableId> _usedPlanes = new HashSet<TrackableId>();

        // AR-session telemetry. Placement is this component's own doing;
        // reposition/scale/rotation come from CarManipulationController (see
        // its own file - a separate component owns everything about
        // manipulating the placed car, this one only owns getting it placed
        // and reporting the combined session) via GestureCountsUpdatedEvent,
        // not computed here. Reset both on the very first AR entry (OnEnable,
        // no EnterArRequestedEvent fires then - see GameEvents.cs) and on
        // every re-entry (OnEnterAr).
        private int _placementCount;
        private int _repositionCount;
        private int _scaleCount;
        private int _rotationCount;
        private string _clientArSessionId;
        private DateTime _arSessionStartedAt;

        // Tracks which anchor the placed car currently belongs to, so
        // OnAnchorsChanged can tell "an anchor was removed" from "OUR
        // anchor was removed" - see OnAnchorsChanged. Keyed by TrackableId
        // rather than holding onto the ARAnchor reference itself, matching
        // SinglePlaneVisualizerController's convention: a removed trackable
        // may already be in a not-safely-usable state by the time the event
        // fires, so only its id is trusted.
        private TrackableId _currentAnchorId = TrackableId.invalidId;
        private GameObject _currentPlacedInstance;
        // The car is parented to THIS, not directly to the anchor - see
        // AnchorFollowSmoother. Kept so ReanchorPlacedInstance can repoint
        // it at a new anchor instead of recreating it (preserving the car's
        // current local drag/rotate/scale offset), and so a fresh placement
        // can clean up a leftover pivot from an earlier placement.
        private GameObject _currentSmoothingPivot;

        // When each currently-tracked plane was first detected (Time.time),
        // used by IsPlaneStable to gate placement - see _minPlaneTrackingSeconds.
        private readonly Dictionary<TrackableId, float> _planeFirstSeenAt = new Dictionary<TrackableId, float>();

        private void OnEnable()
        {
            _inputProvider = _inputProviderBehaviour as IInputProvider;
            _placementService = _placementServiceBehaviour as IARPlacementService;

            if (_inputProvider == null || _placementService == null)
            {
                Debug.LogError("Missing required dependencies. Disabling component.");
                enabled = false;
                return;
            }

            EventBus.Subscribe<EnterArRequestedEvent>(OnEnterAr);
            EventBus.Subscribe<ExitArRequestedEvent>(OnExitAr);
            EventBus.Subscribe<GestureCountsUpdatedEvent>(OnGestureCountsUpdated);

            if (_anchorManager != null)
                _anchorManager.trackablesChanged.AddListener(OnAnchorsChanged);
            if (_planeManager != null)
                _planeManager.trackablesChanged.AddListener(OnPlanesChangedForStability);

            ResetArSessionTracking();
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<EnterArRequestedEvent>(OnEnterAr);
            EventBus.Unsubscribe<ExitArRequestedEvent>(OnExitAr);
            EventBus.Unsubscribe<GestureCountsUpdatedEvent>(OnGestureCountsUpdated);

            if (_anchorManager != null)
                _anchorManager.trackablesChanged.RemoveListener(OnAnchorsChanged);
            if (_planeManager != null)
                _planeManager.trackablesChanged.RemoveListener(OnPlanesChangedForStability);
        }

        private void OnPlanesChangedForStability(ARTrackablesChangedEventArgs<ARPlane> args)
        {
            foreach (var plane in args.added)
            {
                if (!_planeFirstSeenAt.ContainsKey(plane.trackableId))
                    _planeFirstSeenAt[plane.trackableId] = Time.time;
            }
            foreach (var removedPlane in args.removed)
                _planeFirstSeenAt.Remove(removedPlane.Key);
        }

        private bool IsPlaneStable(TrackableId planeId) =>
            _planeFirstSeenAt.TryGetValue(planeId, out float firstSeenAt) &&
            Time.time - firstSeenAt >= _minPlaneTrackingSeconds;

        // ARKit periodically merges plane fragments as it scans more of a
        // surface (very common right after placement, when the user moves
        // around and gives it a wider view of the floor) - when the plane an
        // anchor is attached to gets merged away, AR Foundation removes that
        // anchor, which by default destroys its GameObject. The car itself
        // is safe from that destruction - it's parented to our own
        // AnchorFollowSmoother pivot, not to the anchor directly (see
        // TryPlaceObject) - but the pivot would be left tracking a dead
        // target forever, silently un-anchored, unless we repoint it here.
        private void OnAnchorsChanged(ARTrackablesChangedEventArgs<ARAnchor> args)
        {
            if (_currentAnchorId == TrackableId.invalidId || _currentSmoothingPivot == null)
                return;

            foreach (var removed in args.removed)
            {
                if (removed.Key != _currentAnchorId)
                    continue;

                ReanchorPlacedInstance();
                break;
            }
        }

        // Tries to attach a fresh anchor to whatever plane is currently
        // detected under the pivot's present position, reusing the same
        // placement-pose lookup used for the original tap so this stays
        // consistent with how placement already decides "is there a plane
        // here." If no plane is currently detected there, the pivot is left
        // with no target (holds its last smoothed pose, un-corrected) rather
        // than the car being lost outright - same fallback ObjectPlacerController
        // already uses in TryPlaceObject when the very first placement finds
        // no plane to anchor to.
        private void ReanchorPlacedInstance()
        {
            Transform pivotTransform = _currentSmoothingPivot.transform;

            ARAnchor newAnchor = null;
            if (_anchorManager != null && _planeManager != null && Camera.main != null && _placementService != null)
            {
                Vector2 screenPos = Camera.main.WorldToScreenPoint(pivotTransform.position);
                if (_placementService.TryGetPlacementPose(screenPos, out _, out TrackableId planeId))
                {
                    ARPlane plane = _planeManager.GetPlane(planeId);
                    if (plane != null)
                    {
                        var reanchorPose = new Pose(pivotTransform.position, pivotTransform.rotation);
                        newAnchor = _anchorManager.AttachAnchor(plane, reanchorPose);
                    }
                }
            }

            if (newAnchor != null)
            {
                // Snap the pivot to the new anchor immediately rather than
                // smoothing into it - the OLD anchor's pose is now
                // meaningless (it's gone), so there's nothing worth easing
                // away from. Ordinary corrections from this point on still
                // smooth normally via AnchorFollowSmoother.
                pivotTransform.SetPositionAndRotation(newAnchor.transform.position, newAnchor.transform.rotation);
                _currentSmoothingPivot.GetComponent<AnchorFollowSmoother>().SetTarget(newAnchor.transform);
                _currentAnchorId = newAnchor.trackableId;
                Debug.Log("Re-anchored placed car after its previous anchor was removed (likely a plane merge).");
            }
            else
            {
                _currentAnchorId = TrackableId.invalidId;
                Debug.LogWarning("Placed car's anchor was removed and no plane was found to re-anchor it to - it will not be corrected for tracking drift until it is.");
            }
        }

        // CarManipulationController publishes this every time any of its
        // counts changes (including the reset-to-zero it publishes on its
        // own OnEnterAr/new-placement) - just mirror whatever it last
        // reported rather than tracking gesture state here too.
        private void OnGestureCountsUpdated(GestureCountsUpdatedEvent evt)
        {
            _repositionCount = evt.RepositionCount;
            _scaleCount = evt.ScaleCount;
            _rotationCount = evt.RotationCount;
        }

        // AR Scene is loaded once and never reloaded (see ParentPageController.OnCarSelected) -
        // these handle re-entry/exit by disabling/re-enabling ARSession and
        // plane detection instead of relying on a fresh scene load to reset
        // them. Disabling (not destroying) ARSession is the AR
        // Foundation-documented way to pause tracking; re-enabling lets it
        // attempt to recover, which is what fixed the black camera feed on
        // the second+ AR entry - destroying and recreating it was the bug.
        private void OnEnterAr(EnterArRequestedEvent evt)
        {
            if (_arSession != null)
                _arSession.enabled = true;
            if (_planeManager != null)
                _planeManager.enabled = true;
            ResetUsedPlanes();
            ResetArSessionTracking();
        }

        private void OnExitAr(ExitArRequestedEvent evt)
        {
            SendArSessionTelemetry();
            DisablePlacement();
            if (_planeManager != null)
                _planeManager.enabled = false;
            if (_arSession != null)
                _arSession.enabled = false;
        }

        private void ResetArSessionTracking()
        {
            _placementCount = 0;
            _repositionCount = 0;
            _scaleCount = 0;
            _rotationCount = 0;
            _clientArSessionId = Guid.NewGuid().ToString();
            _arSessionStartedAt = DateTime.UtcNow;
            _currentAnchorId = TrackableId.invalidId;
            _currentPlacedInstance = null;
            _planeFirstSeenAt.Clear();
        }

        // Best-effort, same philosophy as every other telemetry send in this
        // project - a failed/slow call must never block exiting AR. Sent
        // even when _placementCount is 0 ("entered AR, placed nothing" is
        // real signal too), as long as a telemetry session and a selected
        // car both exist. Uses `this` as the coroutine runner (not
        // GameManager.Instance) because this component stays enabled through
        // OnExitAr - it isn't being destroyed the way EstimatorCardController
        // is when it sends its own telemetry from OnDisable.
        private void SendArSessionTelemetry()
        {
            var gameManager = GameManager.Instance;
            if (gameManager == null || gameManager.TelemetrySessionId <= 0) return;

            // A visit where the real car model failed to load (network error,
            // bad glTF, etc.) isn't genuine placement behaviour - the
            // customer either stared at the wrong placeholder or immediately
            // backed out to retry, same as what actually happened in testing
            // this. A resulting 0-placement record would misread as "looked
            // and lost interest" when it's really a technical failure -
            // don't record it at all rather than ship a false signal.
            if (!gameManager.CurrentArModelLoadSucceeded) return;

            var selectedCar = gameManager.SelectedCar;
            var accessToken = gameManager.AccessToken;
            if (selectedCar == null || selectedCar.id <= 0 || string.IsNullOrEmpty(accessToken)) return;

            var resolved = EnvironmentResolver.Resolve("[NAS AR Telemetry]");
            if (resolved.Settings == null) return;

            var telemetryApi = new TelemetryApi(this, resolved.Settings, resolved.TrustAnyCertificate);
            var request = new ArSessionTelemetryRequest
            {
                customerSessionId = gameManager.TelemetrySessionId,
                clientArSessionId = _clientArSessionId,
                vehicleModelId = selectedCar.id,
                startedAt = _arSessionStartedAt.ToString("o"),
                endedAt = DateTime.UtcNow.ToString("o"),
                placementCount = _placementCount,
                repositionCount = _repositionCount,
                scaleCount = _scaleCount,
                rotationCount = _rotationCount
            };
            telemetryApi.LogArSession(request, accessToken, result =>
            {
                if (!result.Success)
                    Debug.LogWarning($"[NAS AR Telemetry] AR session telemetry failed: {result.Error.Detail}");
            });
        }

        // No auto-start here on purpose - EnablePlacement() is called by
        // SelectedCarModelLoader once it knows what to place (the downloaded
        // car model, or the default placeholder on failure). Starting
        // placement immediately in Start() risked a tap landing before that
        // async swap finished, placing the stale placeholder instead.

        /// <summary>Call this to enable placement mode (e.g., from a UI button).</summary>
        public void EnablePlacement()
        {
            if (_placementCoroutine != null)
            {
                Debug.LogWarning("Placement already active. Wait for it to complete or call DisablePlacement().");
                return;
            }

            _placementCoroutine = StartCoroutine(PlacementRoutine());
            Debug.Log("Placement mode enabled. Tap to place an object.");
        }

        /// <summary>Call this to cancel placement mode manually (e.g., if user changes mind).</summary>
        public void DisablePlacement()
        {
            if (_placementCoroutine != null)
            {
                StopCoroutine(_placementCoroutine);
                _placementCoroutine = null;
                Debug.Log("Placement mode cancelled.");
            }
        }

        private IEnumerator PlacementRoutine()
        {
            bool objectPlaced = false;

            while (!objectPlaced)
            {
                // Wait for a tap
                yield return new WaitUntil(() => _inputProvider.GetTap(out _));

                // Get the tap position
                _inputProvider.GetTap(out Vector2 screenPos);

                // Attempt to place the object (this coroutine handles the attempt)
                yield return StartCoroutine(TryPlaceObject(screenPos, (success) => objectPlaced = success));

                // Small delay to prevent immediate retry if the tap was held
                yield return new WaitForSeconds(0.1f);
            }

            // Placement finished successfully – clean up
            _placementCoroutine = null;
            Debug.Log("Placement mode ended. Call EnablePlacement() to place another.");
        }

        private IEnumerator TryPlaceObject(Vector2 screenPos, System.Action<bool> callback)
        {
            GameObject prefab = _placementService.RaycastPrefab;
            if (prefab == null)
            {
                Debug.LogWarning("No prefab assigned to ARRaycastManager. Cannot place object.");
                callback?.Invoke(false);
                yield break;
            }

            bool success = false;

            try
            {
                if (_placementService.TryGetPlacementPose(screenPos, out Pose pose, out TrackableId planeId))
                {
                    if (!IsPlaneStable(planeId))
                    {
                        Debug.Log("Surface detected but still stabilizing - give it a moment, then tap again.");
                        // Optionally provide user feedback (UI text, sound)
                    }
                    // Check if plane already used (if enabled)
                    else if (_preventMultiplePerPlane && _usedPlanes.Contains(planeId))
                    {
                        Debug.Log("This plane already has an object. Try a different plane.");
                        // Optionally provide user feedback (UI text, sound)
                    }
                    else
                    {
                        // The downloaded car model is already a live scene
                        // instance (SelectedCarModelLoader built it once and
                        // parked it inactive) - reposition and reveal that
                        // same object instead of cloning it, since only one
                        // placement ever happens per session anyway (this
                        // coroutine loop exits after the first success). The
                        // placeholder prefab fallback has no scene instance
                        // yet, so it still needs a real Instantiate().
                        // Use the raycast hit's Y as-is - it's the real
                        // detected floor height. A fixed 0f assumed local
                        // Y=0 always meant "the floor", which only held while
                        // XR Origin's CameraYOffset was compensating for it;
                        // with that offset at 0, Y=0 is wherever the AR
                        // session started tracking from, not the floor.
                        Vector3 placementPosition = pose.position;

                        ARPlane hitPlane = _planeManager != null ? _planeManager.GetPlane(planeId) : null;

                        GameObject placedInstance;
                        if (_placementService.RaycastPrefabIsLiveInstance)
                        {
                            placedInstance = prefab;
                            placedInstance.SetActive(true);
                        }
                        else
                        {
                            placedInstance = Instantiate(prefab);
                            // The live-instance car path gets its shadow once,
                            // in SelectedCarModelLoader, when the model is
                            // first built - this Instantiate() branch is the
                            // ONLY path that needs it added here, and does so
                            // exactly once per placement since a fresh
                            // instance is created every time.
                            ContactShadowFactory.Attach(placedInstance);
                        }

                        // Set the FINAL orientation now, before measuring
                        // bounds below - an axis-aligned bounding box's shape
                        // depends on rotation, so measuring at whatever
                        // arbitrary rotation the object happened to have
                        // before this (e.g. identity, left over from being
                        // parked off-scene) would give the wrong footprint
                        // for a long, narrow vehicle depending on which way
                        // it ends up facing.
                        placedInstance.transform.rotation = pose.rotation;

                        // Tapping close to your own feet would spawn the car
                        // on top of you - push the placement point out along
                        // the tap direction from the camera until the car's
                        // OWN real footprint clears the camera by
                        // _extraClearanceFromUser, not just its origin point.
                        // A single flat distance (the original approach here)
                        // works for a small placeholder cube but does almost
                        // nothing for an actual ~4-5m car - pushing a
                        // 4-5m-long object's origin out by one meter still
                        // leaves most of its body swept back through wherever
                        // the user is standing, which is exactly the
                        // "placement leaves the user inside the car" bug
                        // this replaces.
                        if (Camera.main != null)
                        {
                            Bounds? footprintBounds = ContactShadowFactory.ComputeRendererBounds(placedInstance);
                            // Half-diagonal of the XZ footprint - the
                            // farthest any point of the (now correctly
                            // rotated) car can be from its own center,
                            // regardless of which side of it the user ends
                            // up standing on.
                            float footprintRadius = footprintBounds.HasValue
                                ? new Vector2(footprintBounds.Value.extents.x, footprintBounds.Value.extents.z).magnitude
                                : 0f;
                            float requiredDistance = footprintRadius + _extraClearanceFromUser;

                            Vector3 camPos = Camera.main.transform.position;
                            Vector3 fromCamera = new Vector3(placementPosition.x - camPos.x, 0f, placementPosition.z - camPos.z);
                            float distance = fromCamera.magnitude;
                            if (distance < requiredDistance)
                            {
                                Vector3 direction = distance > 0.001f
                                    ? fromCamera / distance
                                    : new Vector3(Camera.main.transform.forward.x, 0f, Camera.main.transform.forward.z).normalized; // camera exactly on the point - fall back to where it's facing
                                placementPosition = new Vector3(camPos.x + direction.x * requiredDistance, placementPosition.y, camPos.z + direction.z * requiredDistance);
                            }
                        }

                        // Attach the car to an ARAnchor pinned to the hit
                        // plane instead of just setting a raw world-space
                        // Transform - without this, a brief tracking-quality
                        // drop (a freeze) followed by AR Foundation
                        // correcting the session's coordinate frame visibly
                        // drags an unanchored object along with the
                        // correction ("car follows the camera, shifted from
                        // where it was placed"). An anchored object gets that
                        // same correction applied to its transform, so it
                        // stays pinned to its real-world point instead.
                        Pose anchoredPose = new Pose(placementPosition, pose.rotation);
                        ARAnchor anchor = (_anchorManager != null && hitPlane != null)
                            ? _anchorManager.AttachAnchor(hitPlane, anchoredPose)
                            : null;

                        if (_currentSmoothingPivot != null)
                            Destroy(_currentSmoothingPivot);

                        if (anchor != null)
                        {
                            // Car is parented to this pivot, not to the
                            // anchor directly - the pivot eases toward the
                            // anchor's pose (AnchorFollowSmoother) instead of
                            // matching it exactly every frame, so a tracking
                            // correction plays out as a glide, not a pop.
                            var pivot = new GameObject("Car Anchor Pivot (smoothed)");
                            pivot.transform.SetPositionAndRotation(anchor.transform.position, anchor.transform.rotation);
                            var smoother = pivot.AddComponent<AnchorFollowSmoother>();
                            smoother.Configure(_anchorFollowPositionSmoothTime, _anchorFollowRotationDegreesPerSecond);
                            smoother.SetTarget(anchor.transform);

                            placedInstance.transform.SetParent(pivot.transform, worldPositionStays: false);
                            placedInstance.transform.localPosition = Vector3.zero;
                            placedInstance.transform.localRotation = Quaternion.identity;
                            _currentAnchorId = anchor.trackableId;
                            _currentSmoothingPivot = pivot;
                        }
                        else
                        {
                            Debug.LogWarning("Could not create an ARAnchor for this placement - car will not be corrected for tracking drift.");
                            placedInstance.transform.SetPositionAndRotation(anchoredPose.position, anchoredPose.rotation);
                            _currentAnchorId = TrackableId.invalidId;
                            _currentSmoothingPivot = null;
                        }
                        _currentPlacedInstance = placedInstance;

                        EventBus.Publish(new CarPlacedEvent(placedInstance));
                        _placementCount++;

                        if (_preventMultiplePerPlane)
                            _usedPlanes.Add(planeId);

                        // Optional: hide other planes
                        if (_planeManager != null)
                        {
                            if (hitPlane != null)
                            {
                                foreach (var plane in _planeManager.trackables)
                                    //plane.gameObject.SetActive(plane == hitPlane);
                                    if(plane.gameObject.activeSelf)
                                        plane.gameObject.SetActive(false);
                            }
                            _planeManager.enabled = false; // Stops new plane detection
                        }

                        Debug.Log("Object placed successfully.");
                        success = true;
                    }
                }
                else
                {
                    Debug.Log("No surface detected. Try again.");
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogException(ex, this);
            }

            // Brief cooldown to prevent accidental double-taps (optional)
            yield return new WaitForSeconds(0.25f);

            callback?.Invoke(success);
        }

        /// <summary>Optional: clear the list of used planes (e.g., for a new session).</summary>
        public void ResetUsedPlanes()
        {
            _usedPlanes.Clear();
        }
    }
}
