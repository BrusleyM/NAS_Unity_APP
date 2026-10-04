using System;
using System.Collections.Generic;
using UnityEngine;

namespace NAS.Core
{
    /// <summary>
    /// Discovers and categorizes a placed car's Renderers by the naming
    /// convention the Blender mesh-regrouping pass produces (Body,
    /// Door_FL/FR/RL/RR, optional Roof/Bonnet/Trunk, Wheels, Trim, Interior/
    /// Dashboard, Glass, optional Fixed_* prefix for non-customizable
    /// accents). Runs once at Awake() and caches the result - other systems
    /// (CarPaintController, a future door-open animation, future wheel/trim
    /// customization) query this instead of each re-deriving categories from
    /// material names themselves.
    ///
    /// Matching is deliberately tolerant, not tied to any one car's exact
    /// node set - every category except Body is optional (present only if
    /// that car actually has it split out), and matching is case-insensitive
    /// with prefix fallbacks (e.g. "Wheels" or "Wheel_FL" both match wheels,
    /// "Interior" or "Dashboard" both match interior) since not every car in
    /// the catalog has gone through the same regrouping pass yet, and even
    /// once they all have, minor per-car naming variation isn't an error.
    ///
    /// Each door slot is a LIST, not a single Renderer - a real door is
    /// often more than one mesh (panel + handle + mirror housing, anything
    /// paintable that should move as a unit with that door). A name only
    /// needs to start with "Door_FL"/"FR"/"RL"/"RR" to join that door's
    /// list, e.g. "Door_FL", "Door_FL_Handle", "Door_FL_Mirror" all land in
    /// the same front-left list. This was previously a single Renderer field
    /// that a second matching name would silently overwrite - any car whose
    /// door had more than one paintable mesh was quietly losing data.
    /// A door-associated part that should NEVER be paintable (e.g. the black
    /// window trim/DLO molding around a door) does not belong in this list -
    /// name it with the existing "Fixed_" prefix instead (e.g.
    /// "Fixed_Door_FL_Trim") so it's excluded from PaintableBodyRenderers.
    /// Whether it visually MOVES with the door on a future open animation is
    /// a separate, purely hierarchical concern - handled by parenting it
    /// under the door's node in the model itself, not by anything here.
    /// </summary>
    public class CarComponents : MonoBehaviour
    {
        private Renderer _body;
        private readonly List<Renderer> _doorFrontLeft = new List<Renderer>();
        private readonly List<Renderer> _doorFrontRight = new List<Renderer>();
        private readonly List<Renderer> _doorRearLeft = new List<Renderer>();
        private readonly List<Renderer> _doorRearRight = new List<Renderer>();
        private Renderer _roof;
        private Renderer _bonnet;
        private Renderer _trunk;
        private readonly List<Renderer> _wheels = new List<Renderer>();
        private readonly List<Renderer> _trim = new List<Renderer>();
        private readonly List<Renderer> _interior = new List<Renderer>();
        private readonly List<Renderer> _glass = new List<Renderer>();
        private readonly List<Renderer> _fixedComponents = new List<Renderer>();

        public Renderer Body => _body;
        public IReadOnlyList<Renderer> DoorFrontLeft => _doorFrontLeft;
        public IReadOnlyList<Renderer> DoorFrontRight => _doorFrontRight;
        public IReadOnlyList<Renderer> DoorRearLeft => _doorRearLeft;
        public IReadOnlyList<Renderer> DoorRearRight => _doorRearRight;
        /// <summary>Null if this car's roof shares the body's color/material (the common case) rather than being split out.</summary>
        public Renderer Roof => _roof;
        /// <summary>Null if this car's bonnet shares the body's color/material (the common case) rather than being split out.</summary>
        public Renderer Bonnet => _bonnet;
        /// <summary>Null if this car has no separate trunk/boot lid (the common case for a hatchback/fastback silhouette) or it shares the body's material.</summary>
        public Renderer Trunk => _trunk;
        public IReadOnlyList<Renderer> Wheels => _wheels;
        public IReadOnlyList<Renderer> Trim => _trim;
        public IReadOnlyList<Renderer> Interior => _interior;
        public IReadOnlyList<Renderer> Glass => _glass;
        /// <summary>Fixed accent-color parts (e.g. an always-black roof, or a door's window trim) - never recolored by anything, body paint included.</summary>
        public IReadOnlyList<Renderer> FixedComponents => _fixedComponents;

        /// <summary>
        /// Body + doors + a non-fixed roof/bonnet/trunk - what a single
        /// body-color paint change should apply to as one uniform set.
        /// Doors and a same-color roof/bonnet/trunk are only separate
        /// objects for animation/two-tone-detection purposes, not because
        /// they're meant to hold a different color from the body.
        /// </summary>
        public IEnumerable<Renderer> PaintableBodyRenderers
        {
            get
            {
                if (_body != null) yield return _body;
                foreach (var r in _doorFrontLeft) yield return r;
                foreach (var r in _doorFrontRight) yield return r;
                foreach (var r in _doorRearLeft) yield return r;
                foreach (var r in _doorRearRight) yield return r;
                if (_roof != null) yield return _roof;
                if (_bonnet != null) yield return _bonnet;
                if (_trunk != null) yield return _trunk;
            }
        }

        private void Awake()
        {
            Discover();
        }

        private void Discover()
        {
            foreach (var renderer in GetComponentsInChildren<Renderer>(true))
            {
                Categorize(renderer.gameObject.name, renderer);
            }
        }

        private void Categorize(string name, Renderer renderer)
        {
            if (StartsWithIgnoreCase(name, "Fixed_"))
            {
                _fixedComponents.Add(renderer);
                return;
            }

            if (EqualsIgnoreCase(name, "Body"))
            {
                _body = renderer;
                return;
            }

            if (StartsWithIgnoreCase(name, "Door_"))
            {
                AssignDoor(name, renderer);
                return;
            }

            if (EqualsIgnoreCase(name, "Roof"))
            {
                _roof = renderer;
                return;
            }

            if (EqualsIgnoreCase(name, "Bonnet") || EqualsIgnoreCase(name, "Hood"))
            {
                _bonnet = renderer;
                return;
            }

            if (EqualsIgnoreCase(name, "Trunk") || EqualsIgnoreCase(name, "Boot"))
            {
                _trunk = renderer;
                return;
            }

            if (StartsWithIgnoreCase(name, "Wheel"))
            {
                _wheels.Add(renderer);
                return;
            }

            if (StartsWithIgnoreCase(name, "Trim"))
            {
                _trim.Add(renderer);
                return;
            }

            if (StartsWithIgnoreCase(name, "Interior") || StartsWithIgnoreCase(name, "Dashboard"))
            {
                _interior.Add(renderer);
                return;
            }

            if (StartsWithIgnoreCase(name, "Glass"))
            {
                _glass.Add(renderer);
                return;
            }

            // Not a crash - just means this node isn't part of any category
            // a consumer currently looks for (e.g. a car with an extra decal
            // mesh). Logged so a real naming-convention gap is visible
            // instead of silently doing nothing.
            Debug.LogWarning($"CarComponents: unrecognized component '{name}' on {gameObject.name} - not categorized.");
        }

        private void AssignDoor(string name, Renderer renderer)
        {
            if (name.IndexOf("FL", StringComparison.OrdinalIgnoreCase) >= 0) _doorFrontLeft.Add(renderer);
            else if (name.IndexOf("FR", StringComparison.OrdinalIgnoreCase) >= 0) _doorFrontRight.Add(renderer);
            else if (name.IndexOf("RL", StringComparison.OrdinalIgnoreCase) >= 0) _doorRearLeft.Add(renderer);
            else if (name.IndexOf("RR", StringComparison.OrdinalIgnoreCase) >= 0) _doorRearRight.Add(renderer);
            else
                Debug.LogWarning($"CarComponents: door component '{name}' on {gameObject.name} doesn't match FL/FR/RL/RR - not assigned to a slot.");
        }

        private static bool EqualsIgnoreCase(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        private static bool StartsWithIgnoreCase(string value, string prefix) => value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }
}
