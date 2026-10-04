using System;
using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using NAS.Core;
using NAS.Core.Models;
using NAS.Core.Events;
using NAS.Core.Networking;
using NAS.Core.Vehicles;
using NAS.UI.Components;

namespace NAS.UI.Controllers
{
    /// <summary>
    /// Screen controller for car catalog browsing. Delegates card paging to CarPager
    /// and publishes CarSelectedEvent when the user starts AR.
    /// </summary>
    public class CarSelectionScreenController : MonoBehaviour
    {
        private const string LogPrefix = "[NAS Cars]";

        private enum CarLoadState { Loading, Loaded, Failed }

        // Not [SerializeField]: this controller is never pre-placed in the
        // scene (it's added dynamically via AddComponent<T>() by
        // ParentPageController), so it has no Inspector to assign these in.
        // They're handed in via Initialize() instead, sourced from
        // ParentPageController's own [SerializeField] fields (which - unlike
        // this controller - IS scene-placed and Inspector-wireable).
        private VisualTreeAsset _carCardTemplate;
        private IVehicleCatalogApi _vehicleApi;
        private RemoteTextureLoader _thumbnailLoader;
        private CarLoadState _loadState = CarLoadState.Loading;
        // car.imageUrl from the API is a relative path (e.g.
        // "/uploads/vehicles/<guid>.png") - RequestCarThumbnail needs this to
        // build an absolute URL before handing it to UnityWebRequestTexture,
        // which can't resolve a relative path to a host on its own.
        private string _apiBaseUrl;

        private Button _typeDropdownButton;
        private Label _selectedTypeLabel;
        private VisualElement _dropdownArrow;
        private VisualElement _dropdownMenu;
        private TextField _searchField;
        private EventCallback<ChangeEvent<string>> _onSearchChanged;
        private VisualElement _carsScrollView;
        private VisualElement _carsContainer;
        private Label _emptyStateLabel;
        private Button _startButton;
        private Button _changeDealershipButton;
        private bool _isDragging;
        private float _dragStartX;
        private int _dragPointerId = -1;

        private List<CarData> _allCars;
        private List<CarData> _filteredCars;
        private CarPager _pager;
        private string _selectedType = "All Types";
        private string _searchQuery = "";
        private bool _isDropdownOpen;

        private readonly List<string> _carTypes = new List<string>
        {
            "All Types", "Sedan", "SUV", "Hatchback", "Van"
        };

        // AddComponent<T>() runs OnEnable() synchronously, before the caller
        // (ParentPageController) gets a chance to call this - so the vehicle
        // fetch (which needs _apiSettings, only available after this runs)
        // happens here, not in OnEnable(). See this project's .claude/CLAUDE.md
        // "Important gotcha" for why - OnEnable only does the
        // data-independent DOM-querying/event wiring.
        public void Initialize(VisualTreeAsset carCardTemplate)
        {
            _carCardTemplate = carCardTemplate;
            InitializeCarData();
            SetupUI();
        }

        private void OnEnable()
        {
            // _carCardTemplate is only non-null here if Initialize() already
            // ran (e.g. OnEnable firing again later, not the synchronous
            // AddComponent-triggered first call) - SetupUI() is idempotent.
            if (_carCardTemplate != null)
                SetupUI();
        }

        private void SetupUI()
        {
            if (_pager != null)
                return;

            var uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null || _carCardTemplate == null)
                return;

            var root = uiDocument.rootVisualElement;

            _typeDropdownButton = root.Q<Button>("type-dropdown-button");
            _selectedTypeLabel = root.Q<Label>("selected-type-label");
            _dropdownArrow = root.Q<VisualElement>("dropdown-arrow");
            _dropdownMenu = root.Q<VisualElement>("dropdown-menu");
            _searchField = root.Q<TextField>("search-field");
            _carsScrollView = root.Q<VisualElement>("cars-scroll-view");
            _carsContainer = root.Q<VisualElement>("cars-container");
            _emptyStateLabel = root.Q<Label>("empty-state-label");
            _startButton = root.Q<Button>("start-ar-button");
            _changeDealershipButton = root.Q<Button>("change-dealership-button");
            var dealershipNameLabel = root.Q<Label>("dealership-name-label");
            var dealership = GameManager.Instance != null ? GameManager.Instance.SelectedDealership : null;
            if (dealershipNameLabel != null && dealership != null)
                dealershipNameLabel.text = dealership.name;
            if (_changeDealershipButton != null)
            {
                _changeDealershipButton.style.display = dealership != null ? DisplayStyle.Flex : DisplayStyle.None;
                _changeDealershipButton.clicked += OnChangeDealershipClicked;
            }

            _pager = new CarPager(_carsContainer, _carCardTemplate, RequestCarThumbnail);

            PopulateDropdown();

            _typeDropdownButton.clicked += ToggleDropdown;
            _onSearchChanged = evt =>
            {
                _searchQuery = evt.newValue;
                UpdateFilteredCars();
            };
            _searchField.RegisterValueChangedCallback(_onSearchChanged);
            _startButton.clicked += OnStartARClicked;

            _carsScrollView.RegisterCallback<PointerDownEvent>(OnCarTrackPointerDown);
            _carsScrollView.RegisterCallback<PointerMoveEvent>(OnCarTrackPointerMove);
            _carsScrollView.RegisterCallback<PointerUpEvent>(OnCarTrackPointerUp);
            _carsScrollView.RegisterCallback<PointerCaptureOutEvent>(OnCarTrackPointerCaptureOut);

            UpdateFilteredCars();
        }

        private void InitializeCarData()
        {
            _allCars = new List<CarData>();
            _loadState = CarLoadState.Loading;

            var resolved = EnvironmentResolver.Resolve(LogPrefix);
            if (resolved.Settings == null)
            {
                Debug.LogError($"{LogPrefix} ApiSettings is missing on CarSelectionScreenController.");
                _loadState = CarLoadState.Failed;
                UpdateFilteredCars();
                return;
            }

            _vehicleApi = new VehicleCatalogApi(this, resolved.Settings, resolved.TrustAnyCertificate);
            _thumbnailLoader = new RemoteTextureLoader(this, resolved.TrustAnyCertificate);
            _apiBaseUrl = resolved.Settings.BaseUrl;

            string accessToken = GameManager.Instance != null ? GameManager.Instance.AccessToken : null;
            // Only the dealership the customer chose, so whichever car they
            // pick (and the lead it produces) belongs to it. Null only if
            // nothing was chosen, which the router shouldn't allow.
            var selectedDealership = GameManager.Instance != null ? GameManager.Instance.SelectedDealership : null;
            int? dealershipId = selectedDealership != null ? selectedDealership.id : (int?)null;
            _vehicleApi.GetVehicles(dealershipId, accessToken, OnVehiclesLoaded);
        }

        private void OnVehiclesLoaded(ApiResult<List<CarData>> result)
        {
            _allCars = result.Success ? (result.Value ?? new List<CarData>()) : new List<CarData>();
            _loadState = result.Success ? CarLoadState.Loaded : CarLoadState.Failed;

            if (!result.Success)
                Debug.LogWarning($"{LogPrefix} Vehicle catalog fetch failed: {result.Error.Detail}");

            UpdateFilteredCars(restoreSelectedIndex: true);
        }

        // No automatic fallback to Resources.LoadAll on failure - local fixture
        // CarData assets don't have real backend ids, so silently substituting
        // them would let someone "select" a car that doesn't exist server-side.

        private void RequestCarThumbnail(CarData car, Action<Texture2D> onLoaded)
        {
            if (car == null || car.image != null || string.IsNullOrEmpty(car.imageUrl))
            {
                // Hand-authored fixtures with an Editor-assigned image never hit
                // the network - preserves today's behavior for those.
                onLoaded?.Invoke(car != null ? car.image : null);
                return;
            }

            _thumbnailLoader.RequestTexture(ResolveImageUrl(car.imageUrl), onLoaded);
        }

        // car.imageUrl is a relative path like "/uploads/vehicles/<guid>.png" -
        // UnityWebRequestTexture can't resolve that to a host on its own, so it
        // needs prefixing with the same API base URL the vehicle catalog/auth
        // requests already use. Left as-is if it's already absolute (e.g. a
        // future CDN-hosted image), so this doesn't double-prefix.
        private string ResolveImageUrl(string imageUrl)
        {
            if (imageUrl.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                imageUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
                return imageUrl;

            return string.IsNullOrEmpty(_apiBaseUrl) ? imageUrl : $"{_apiBaseUrl}{imageUrl}";
        }

        private void PopulateDropdown()
        {
            if (_dropdownMenu == null)
                return;

            _dropdownMenu.Clear();
            for (int i = 0; i < _carTypes.Count; i++)
            {
                var type = _carTypes[i];
                var item = new Button();
                item.text = type;
                item.AddToClassList("dropdown-item");
                if (i == _carTypes.Count - 1)
                    item.AddToClassList("last-dropdown-item");
                if (type == _selectedType)
                    item.AddToClassList("selected");

                item.clicked += () =>
                {
                    _selectedType = type;
                    _selectedTypeLabel.text = type;
                    _isDropdownOpen = false;
                    _dropdownMenu.style.display = DisplayStyle.None;
                    _dropdownArrow.RemoveFromClassList("rotate");
                    UpdateFilteredCars();
                };
                _dropdownMenu.Add(item);
            }
        }

        private void ToggleDropdown()
        {
            _isDropdownOpen = !_isDropdownOpen;
            if (_isDropdownOpen)
                PositionDropdownMenu();
            _dropdownMenu.style.display = _isDropdownOpen ? DisplayStyle.Flex : DisplayStyle.None;
            if (_isDropdownOpen)
                _dropdownArrow.AddToClassList("rotate");
            else
                _dropdownArrow.RemoveFromClassList("rotate");
        }

        private void PositionDropdownMenu()
        {
            var parent = _dropdownMenu.parent;
            if (parent == null)
                return;

            var buttonBound = _typeDropdownButton.worldBound;
            var parentBound = parent.worldBound;

            _dropdownMenu.style.left = buttonBound.xMin - parentBound.xMin;
            _dropdownMenu.style.top = buttonBound.yMax - parentBound.yMin + 4f;
            _dropdownMenu.style.width = buttonBound.width;
        }

        // restoreSelectedIndex is only true right after a fresh vehicle-list load
        // (see OnVehiclesLoaded) - a filter/search change should always reset to
        // index 0 like before, not keep jumping back to the previously selected car.
        private void UpdateFilteredCars(bool restoreSelectedIndex = false)
        {
            if (_allCars == null)
                return;

            // car.type/category can be null - real vehicles don't always have a
            // body type or powertrain set yet (see NAS_Backend's nullable
            // BodyType/Powertrain columns), unlike the old local fixtures which
            // always had both.
            _filteredCars = _allCars.FindAll(car =>
                (_selectedType == "All Types" || car.type == _selectedType) &&
                (string.IsNullOrEmpty(_searchQuery) ||
                 (car.carName ?? string.Empty).ToLower().Contains(_searchQuery.ToLower()) ||
                 (car.category ?? string.Empty).ToLower().Contains(_searchQuery.ToLower()))
            );

            int startIndex = 0;
            if (restoreSelectedIndex)
            {
                // GameManager.SelectedCar survives the AR Scene round-trip (it's a
                // DontDestroyOnLoad singleton) - find where that car landed in the
                // freshly fetched list. Falls back to 0 if nothing was selected yet,
                // or the car is no longer present (e.g. went inactive server-side).
                var selected = GameManager.Instance != null ? GameManager.Instance.SelectedCar : null;
                if (selected != null)
                {
                    int idx = _filteredCars.FindIndex(c => c.id == selected.id);
                    if (idx >= 0)
                        startIndex = idx;
                }
            }

            if (_pager != null)
                _pager.SetCars(_filteredCars, startIndex);

            UpdateEmptyState();
        }

        private void UpdateEmptyState()
        {
            if (_emptyStateLabel == null || _carsScrollView == null || _startButton == null)
                return;

            bool hasResults = _filteredCars != null && _filteredCars.Count > 0;

            _emptyStateLabel.text = _loadState switch
            {
                CarLoadState.Loading => "Loading cars...",
                CarLoadState.Failed => "Couldn't load the car catalog. Please try again later.",
                _ => "No cars match your search."
            };

            _emptyStateLabel.style.display = hasResults ? DisplayStyle.None : DisplayStyle.Flex;
            _carsScrollView.style.display = hasResults ? DisplayStyle.Flex : DisplayStyle.None;
            _startButton.SetEnabled(hasResults);
        }

        private void OnChangeDealershipClicked() => EventBus.Publish(new ChangeDealershipRequestedEvent());

        private void OnStartARClicked()
        {
            if (_pager == null || !_pager.HasCars)
                return;

            var selectedCar = _pager.SelectedCar;
            var vehicle = new VehicleInfo
            {
                id = selectedCar.id,
                modelName = selectedCar.carName,
                retailPrice = selectedCar.retailPrice,
                tigrisModelKey = selectedCar.tigrisModelKey,
                exteriorColors = selectedCar.exteriorColors
            };
            EventBus.Publish(new CarSelectedEvent(vehicle));
        }

        private void OnDisable()
        {
            if (_typeDropdownButton != null)
                _typeDropdownButton.clicked -= ToggleDropdown;
            if (_searchField != null && _onSearchChanged != null)
                _searchField.UnregisterValueChangedCallback(_onSearchChanged);
            if (_startButton != null)
                _startButton.clicked -= OnStartARClicked;
            if (_changeDealershipButton != null)
                _changeDealershipButton.clicked -= OnChangeDealershipClicked;

            if (_carsScrollView != null)
            {
                _carsScrollView.UnregisterCallback<PointerDownEvent>(OnCarTrackPointerDown);
                _carsScrollView.UnregisterCallback<PointerMoveEvent>(OnCarTrackPointerMove);
                _carsScrollView.UnregisterCallback<PointerUpEvent>(OnCarTrackPointerUp);
                _carsScrollView.UnregisterCallback<PointerCaptureOutEvent>(OnCarTrackPointerCaptureOut);
            }

            _pager = null;
        }
    

private void OnCarTrackPointerDown(PointerDownEvent evt)
        {
            if (_pager == null || !_pager.HasCars)
                return;

            _isDragging = true;
            _dragPointerId = evt.pointerId;
            _dragStartX = evt.position.x;
            _carsScrollView.CapturePointer(evt.pointerId);
        }

        private void OnCarTrackPointerMove(PointerMoveEvent evt)
        {
            if (!_isDragging || evt.pointerId != _dragPointerId)
                return;

            float deltaX = evt.position.x - _dragStartX;

            // Clamp: don't let the drag reveal a neighbor that doesn't exist.
            if (deltaX > 0 && !_pager.CanGoPrevious)
                deltaX = 0;
            else if (deltaX < 0 && !_pager.CanGoNext)
                deltaX = 0;

            _carsContainer.style.translate = new Translate(deltaX, 0);
        }

        private void OnCarTrackPointerUp(PointerUpEvent evt)
        {
            if (!_isDragging || evt.pointerId != _dragPointerId)
                return;

            _carsScrollView.ReleasePointer(evt.pointerId);
            EndDrag(evt.position.x - _dragStartX);
        }

        private void OnCarTrackPointerCaptureOut(PointerCaptureOutEvent evt)
        {
            if (!_isDragging)
                return;

            EndDrag(0f);
        }

        private void EndDrag(float totalDeltaX)
        {
            _isDragging = false;
            _dragPointerId = -1;

            float viewportWidth = _carsScrollView.resolvedStyle.width;
            float threshold = viewportWidth * 0.25f;

            if (totalDeltaX > threshold && _pager.CanGoPrevious)
                _pager.Previous();
            else if (totalDeltaX < -threshold && _pager.CanGoNext)
                _pager.Next();

            // Instant swap: no easing. RefreshCards() has already rebound content
            // around the (possibly new) center slot, so rest position is always
            // translate 0 — nothing to compute or ease toward.
            _carsContainer.style.translate = new Translate(0, 0);
        }
}
}
