using System.Collections.Generic;
using NAS.Core.Models;
using NAS.Core.Motion;
using UnityEngine;
using UnityEngine.UIElements;

namespace NAS.UI.Components
{
    /// <summary>
    /// Binds one cloned CarCard.uxml instance to a CarData entry.
    /// </summary>
    public class CarCardView
    {
        private static Texture2D _placeholderTexture;

        private readonly VisualElement _root;
        private readonly Image _thumbnail;
        private readonly Label _nameLabel;
        private readonly Label _typeLabel;

        private CarData _boundCar;

        public VisualElement Root => _root;
        public int BoundCarIndex { get; private set; } = -1;

        public CarCardView(VisualTreeAsset template)
        {
            _root = template.CloneTree();
            _root.AddToClassList("car-card");

            _thumbnail = _root.Q<Image>("thumbnail");
            _nameLabel = _root.Q<Label>("name");
            _typeLabel = _root.Q<Label>("type");
        }

public void SetCar(CarData car, int carIndex)
        {
            BoundCarIndex = carIndex;
            _boundCar = car;
            _root.RemoveFromClassList("car-card--hidden");
            _root.style.visibility = Visibility.Visible;

            if (_nameLabel != null)
                _nameLabel.text = car.carName;

            if (_typeLabel != null)
                _typeLabel.text = BuildLabel(car);

            // Set synchronously so a pending remote thumbnail load never shows
            // the previous car's image while it's in flight - the real image
            // (if any) replaces this via ApplyThumbnail once it resolves.
            if (_thumbnail != null)
                _thumbnail.image = car.image != null ? car.image : PlaceholderTexture;
        }

        // Called by whatever kicked off a thumbnail request (see CarPager) once
        // it resolves. No-ops if this slot has since been rebound to a
        // different car - required because the 3-slot pool reuses the same
        // CarCardView instances as the user swipes, so a slow load for car A
        // must not land after this slot has already moved on to car B.
        public void ApplyThumbnail(CarData forCar, Texture2D texture)
        {
            if (_thumbnail == null || !ReferenceEquals(forCar, _boundCar))
                return;

            _thumbnail.image = texture != null ? texture : PlaceholderTexture;
            if (texture != null) UiMotion.FadeIn(_thumbnail, 0.35f);   // the real picture eases in over the placeholder
        }

        private static string BuildLabel(CarData car)
        {
            var parts = new List<string>(3);
            if (car.year > 0)
                parts.Add(car.year.ToString());
            if (!string.IsNullOrEmpty(car.type))
                parts.Add(car.type);
            if (!string.IsNullOrEmpty(car.category))
                parts.Add(car.category);
            return string.Join(" · ", parts);
        }

        private static Texture2D PlaceholderTexture
        {
            get
            {
                if (_placeholderTexture == null)
                {
                    _placeholderTexture = new Texture2D(1, 1);
                    _placeholderTexture.SetPixel(0, 0, new Color(0.2f, 0.2f, 0.2f, 1f));
                    _placeholderTexture.Apply();
                }
                return _placeholderTexture;
            }
        }

public void SetEmpty()
        {
            BoundCarIndex = -1;
            _boundCar = null;
            _root.AddToClassList("car-card--hidden");
            // visibility:hidden (not display:none) so this slot still reserves its
            // layout space — with 3 symmetric slots, justify-content:center on the
            // row naturally centers the middle one. display:none would remove this
            // slot from layout entirely at a boundary (no prev/next car), leaving
            // only 2 children, which breaks that symmetry and shows half of each
            // remaining card instead of one centered card.
            _root.style.visibility = Visibility.Hidden;
        }

        public void SetSelected(bool selected)
        {
            if (selected)
                _root.AddToClassList("car-card--selected");
            else
                _root.RemoveFromClassList("car-card--selected");
        }
    }
}
