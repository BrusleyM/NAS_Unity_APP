using UnityEngine;

namespace NAS.Core
{
    /// <summary>
    /// Sits between a placed object's ARAnchor and the object itself
    /// (Anchor -> this pivot -> car) and eases this GameObject's world pose
    /// toward the anchor's live, AR-Foundation-corrected pose over time,
    /// instead of matching it exactly every frame.
    ///
    /// Why this exists: ARAnchor's transform is continuously overwritten by
    /// AR Foundation based on ARKit's ongoing tracking corrections - that's
    /// the whole point of anchoring (see CLAUDE.md's AR viewport section).
    /// But a rigid 1:1 child of the anchor inherits every correction
    /// instantly, including large ones (e.g. a low-confidence initial
    /// estimate correcting once the user gets a wider viewpoint), which
    /// shows up as a visible pop/jump. This pivot keeps the anchor's
    /// correction fully authoritative - nothing here changes WHERE the car
    /// eventually ends up - it only changes HOW QUICKLY the visual result
    /// catches up, trading an instant snap for a short glide.
    ///
    /// Deliberately does not touch scale, and does not know anything about
    /// gestures - CarManipulationController keeps writing local
    /// position/rotation/scale offsets onto the car exactly as before,
    /// relative to whatever this pivot's current (already-smoothed) pose is.
    /// </summary>
    public class AnchorFollowSmoother : MonoBehaviour
    {
        private Transform _target;
        private Vector3 _positionVelocity;
        private float _positionSmoothTime = 0.35f;
        private float _rotationDegreesPerSecond = 90f;

        public void Configure(float positionSmoothTime, float rotationDegreesPerSecond)
        {
            _positionSmoothTime = positionSmoothTime;
            _rotationDegreesPerSecond = rotationDegreesPerSecond;
        }

        // Re-points this pivot at a (possibly new, e.g. after a re-anchor)
        // target without resetting the smoothing time constant.
        public void SetTarget(Transform target)
        {
            _target = target;
            _positionVelocity = Vector3.zero;
        }

        // LateUpdate, not Update - AR Foundation applies its own trackable
        // pose corrections during its own Update, so reading the anchor's
        // transform here guarantees we're always easing toward THIS frame's
        // corrected pose, never a stale one from before the correction landed.
        private void LateUpdate()
        {
            if (_target == null)
                return;

            transform.position = Vector3.SmoothDamp(transform.position, _target.position, ref _positionVelocity, _positionSmoothTime);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, _target.rotation, _rotationDegreesPerSecond * Time.deltaTime);
        }
    }
}
