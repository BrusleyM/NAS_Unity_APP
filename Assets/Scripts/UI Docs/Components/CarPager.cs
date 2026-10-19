using System;
using System.Collections.Generic;
using NAS.Core.Models;
using UnityEngine;
using UnityEngine.UIElements;

namespace NAS.UI.Components
{
    /// <summary>
    /// Owns car selection index and a fixed pool of five reusable card views.
    /// The ScrollView is a viewport only — currentIndex is the source of truth.
    /// </summary>
    public class CarPager
    {
        public const int SlotCount = 3;
        public const int CenterSlot = 1;

        private readonly CarCardView[] _pool = new CarCardView[SlotCount];
        private readonly Action<CarData, Action<Texture2D>> _requestThumbnail;
        private IReadOnlyList<CarData> _cars = Array.Empty<CarData>();
        private int _currentIndex;

        /// <summary>Raised after the centre car changed through Next/Previous/GoTo, with how far it moved (+ forward, - back).</summary>
        public event Action<int> IndexChanged;

        public int CurrentIndex => _currentIndex;
        public bool HasCars => _cars.Count > 0;
        public bool CanGoPrevious => HasCars && _currentIndex > 0;
        public bool CanGoNext => HasCars && _currentIndex < _cars.Count - 1;

        public CarData SelectedCar =>
            HasCars ? _cars[_currentIndex] : null;

        // requestThumbnail is optional so CarPager stays fully decoupled from
        // networking - callers that don't need remote thumbnails (or tests)
        // can omit it entirely.
        public CarPager(VisualElement track, VisualTreeAsset cardTemplate, Action<CarData, Action<Texture2D>> requestThumbnail = null)
        {
            _requestThumbnail = requestThumbnail;
            track.Clear();

            for (int slot = 0; slot < SlotCount; slot++)
            {
                var card = new CarCardView(cardTemplate);
                _pool[slot] = card;
                track.Add(card.Root);

                int capturedSlot = slot;
                card.Root.RegisterCallback<ClickEvent>(_ => OnSlotClicked(capturedSlot));
            }
        }

        public void SetCars(IReadOnlyList<CarData> cars, int startIndex = 0)
        {
            _cars = cars ?? Array.Empty<CarData>();

            if (_cars.Count == 0)
            {
                _currentIndex = 0;
                RefreshCards();
                return;
            }

            _currentIndex = ClampIndex(startIndex);
            RefreshCards();
        }

        public void Next()
        {
            if (!HasCars || _currentIndex >= _cars.Count - 1)
                return;

            _currentIndex++;
            RefreshCards();
            IndexChanged?.Invoke(1);
        }

        public void Previous()
        {
            if (!HasCars || _currentIndex <= 0)
                return;

            _currentIndex--;
            RefreshCards();
            IndexChanged?.Invoke(-1);
        }

        public void GoTo(int index)
        {
            if (!HasCars)
                return;

            int clamped = ClampIndex(index);
            if (clamped == _currentIndex)
                return;

            var delta = clamped - _currentIndex;
            _currentIndex = clamped;
            RefreshCards();
            IndexChanged?.Invoke(delta);
        }

        private void OnSlotClicked(int slot)
        {
            if (!HasCars || slot == CenterSlot)
                return;

            GoTo(_currentIndex + (slot - CenterSlot));
        }

        private void RefreshCards()
        {
            for (int slot = 0; slot < SlotCount; slot++)
            {
                int carIndex = _currentIndex + (slot - CenterSlot);

                if (carIndex < 0 || carIndex >= _cars.Count)
                {
                    _pool[slot].SetEmpty();
                }
                else
                {
                    var card = _pool[slot];
                    var boundCar = _cars[carIndex];
                    card.SetCar(boundCar, carIndex);
                    // Fires in the same frame as SetCar - a cache hit (the
                    // common case for a previously-shown car) resolves with no
                    // visible flicker; a cache miss leaves the placeholder
                    // SetCar already applied until this resolves later.
                    _requestThumbnail?.Invoke(boundCar, texture => card.ApplyThumbnail(boundCar, texture));
                }

                _pool[slot].SetSelected(slot == CenterSlot);
            }
        }

        private int ClampIndex(int index) =>
            _cars.Count == 0 ? 0 : Math.Clamp(index, 0, _cars.Count - 1);
    }
}
