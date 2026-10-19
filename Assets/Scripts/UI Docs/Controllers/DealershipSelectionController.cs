using System.Collections.Generic;
using NAS.Core.Motion;
using UnityEngine;
using UnityEngine.UIElements;
using NAS.Core;
using NAS.Core.Events;
using NAS.Core.Models;
using NAS.Core.Networking;

namespace NAS.UI.Controllers
{
    /// <summary>
    /// "Where are you buying from?" - lists the dealerships that currently have vehicles. Tapping a card selects it;
    /// the Continue button then publishes DealershipSelectedEvent, so a mis-tap can't commit. Doesn't know what happens
    /// next (ParentPageController shows car selection), same as the other cards. Needs nothing from its creator, so it
    /// fetches in OnEnable.
    ///
    /// Two modes, told apart by whether a dealership is already chosen this login (GameManager clears it on every
    /// login): the first time through there is nothing to go back to, so no Back button; when reached from the side
    /// menu's "Change" the current one is marked, Back returns to car selection, and a note warns that switching
    /// clears the selected car.
    /// </summary>
    public class DealershipSelectionController : MonoBehaviour
    {
        private const string LogPrefix = "[NAS Dealerships]";
        private const string ChangeNote = "Changing dealership changes the cars you can see; your selected car will be cleared.";

        private ScrollView _list;
        private VisualElement _status;
        private Label _statusLabel;
        private Button _retryButton;
        private Button _backButton;
        private Button _continueButton;
        private Label _changeNote;
        private IDealershipApi _api;
        private bool _loading;

        private DealershipInfo _current;                 // already chosen this login = change mode
        private CustomerDealershipDto _selected;
        private readonly Dictionary<int, VisualElement> _cards = new Dictionary<int, VisualElement>();

        private bool ChangeMode => _current != null;

        private void OnEnable()
        {
            var uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) return;

            var root = uiDocument.rootVisualElement;
            _list = root.Q<ScrollView>("dealership-list");
            _status = root.Q<VisualElement>("dealership-status");
            _statusLabel = root.Q<Label>("dealership-status-label");
            _retryButton = root.Q<Button>("dealership-retry-button");
            _backButton = root.Q<Button>("dealership-back-button");
            _continueButton = root.Q<Button>("dealership-continue-button");
            _changeNote = root.Q<Label>("dealership-change-note");

            _current = GameManager.Instance != null ? GameManager.Instance.SelectedDealership : null;
            if (_backButton != null)
            {
                _backButton.style.display = ChangeMode ? DisplayStyle.Flex : DisplayStyle.None;
                _backButton.clicked += OnBackClicked;
            }
            if (_retryButton != null) _retryButton.clicked += Load;
            if (_continueButton != null)
            {
                _continueButton.clicked += OnContinueClicked;
                _continueButton.SetEnabled(false);
            }

            Load();
        }

        private void OnDisable()
        {
            if (_retryButton != null) _retryButton.clicked -= Load;
            if (_backButton != null) _backButton.clicked -= OnBackClicked;
            if (_continueButton != null) _continueButton.clicked -= OnContinueClicked;
        }

        private void Load()
        {
            if (_loading) return;

            var accessToken = GameManager.Instance != null ? GameManager.Instance.AccessToken : null;
            var resolved = EnvironmentResolver.Resolve(LogPrefix);
            if (resolved.Settings == null || string.IsNullOrEmpty(accessToken))
            {
                ShowStatus("Couldn't load dealerships. Please sign in again.", showRetry: false);
                return;
            }

            _loading = true;
            _list.Clear();
            _cards.Clear();
            _selected = null;
            _continueButton?.SetEnabled(false);
            ShowStatus("Loading dealerships...", showRetry: false);

            _api ??= new DealershipApi(this, resolved.Settings, resolved.TrustAnyCertificate);
            _api.GetDealerships(accessToken, OnLoaded);
        }

        private void OnLoaded(ApiResult<CustomerDealershipListResponse> result)
        {
            _loading = false;
            // The card may have been swapped out while the request was in flight.
            if (this == null || _list == null) return;

            if (!result.Success)
            {
                Debug.LogWarning($"{LogPrefix} Fetch failed: {result.Error.Detail}");
                ShowStatus(result.Error.UserMessage, showRetry: true);
                return;
            }

            var dealerships = result.Value?.dealerships ?? System.Array.Empty<CustomerDealershipDto>();
            if (dealerships.Length == 0)
            {
                ShowStatus("No dealerships have cars available yet. Please check back soon.", showRetry: true);
                return;
            }

            _status.style.display = DisplayStyle.None;
            var lastId = GameManager.Instance != null ? GameManager.Instance.LastDealershipId : 0;
            foreach (var dealership in dealerships)
            {
                // Change mode marks the one in use; otherwise the one picked last time on this device is only a hint.
                var badge = ChangeMode
                    ? (dealership.id == _current.id ? "Current" : null)
                    : (dealership.id == lastId ? "Last visited" : null);
                var card = BuildCard(dealership, badge);
                _cards[dealership.id] = card;
                _list.Add(card);
            }

            UiMotion.Stagger(_cards.Values, 0.07f, 0.04f, 14f);
        }

        private void ShowStatus(string message, bool showRetry)
        {
            _status.style.display = DisplayStyle.Flex;
            _statusLabel.text = message;
            _retryButton.style.display = showRetry ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private VisualElement BuildCard(CustomerDealershipDto dealership, string badgeText)
        {
            var card = new Button();
            card.AddToClassList("dealership-card");

            var body = new VisualElement();
            body.AddToClassList("dealership-card-body");
            var name = new Label(dealership.name);
            name.AddToClassList("dealership-card-name");
            body.Add(name);
            if (!string.IsNullOrWhiteSpace(dealership.address))
                body.Add(DetailRow("dealership-card-icon--pin", dealership.address));
            if (!string.IsNullOrWhiteSpace(dealership.phone))
                body.Add(DetailRow("dealership-card-icon--phone", dealership.phone));
            card.Add(body);

            var side = new VisualElement();
            side.AddToClassList("dealership-card-side");
            if (!string.IsNullOrEmpty(badgeText))
            {
                var badge = new Label(badgeText);
                badge.AddToClassList("dealership-card-badge");
                side.Add(badge);
            }
            var check = new VisualElement();
            check.AddToClassList("dealership-card-check");
            var checkLabel = new Label("✓");
            checkLabel.AddToClassList("dealership-card-check-label");
            check.Add(checkLabel);
            side.Add(check);
            card.Add(side);

            card.clicked += () => Select(dealership);
            return card;
        }

        private static VisualElement DetailRow(string iconClass, string text)
        {
            var row = new VisualElement();
            row.AddToClassList("dealership-card-detail");
            var icon = new VisualElement();
            icon.AddToClassList("dealership-card-icon");
            icon.AddToClassList(iconClass);
            row.Add(icon);
            var label = new Label(text);
            label.AddToClassList("dealership-card-detail-text");
            row.Add(label);
            return row;
        }

        private void Select(CustomerDealershipDto dealership)
        {
            _selected = dealership;
            foreach (var pair in _cards)
                pair.Value.EnableInClassList("dealership-card--selected", pair.Key == dealership.id);
            _continueButton?.SetEnabled(true);
            UpdateChangeNote();
        }

        // Only worth saying when switching to a different dealership while a car is already chosen.
        private void UpdateChangeNote()
        {
            if (_changeNote == null) return;
            var hasCar = GameManager.Instance != null && GameManager.Instance.SelectedCar != null;
            var switching = ChangeMode && _selected != null && _selected.id != _current.id;
            _changeNote.text = ChangeNote;
            _changeNote.style.display = switching && hasCar ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private void OnContinueClicked()
        {
            if (_selected == null) return;
            EventBus.Publish(new DealershipSelectedEvent(new DealershipInfo
            {
                id = _selected.id,
                name = _selected.name
            }));
        }

        private void OnBackClicked() => EventBus.Publish(new ReturnToCarSelectionRequestedEvent());
    }
}
