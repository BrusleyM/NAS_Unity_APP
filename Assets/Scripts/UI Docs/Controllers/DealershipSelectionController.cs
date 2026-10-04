using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using NAS.Core;
using NAS.Core.Events;
using NAS.Core.Models;
using NAS.Core.Networking;

namespace NAS.UI.Controllers
{
    /// <summary>
    /// "Where are you buying from?" - lists the dealerships that currently have
    /// vehicles and publishes DealershipSelectedEvent for the one the customer
    /// taps. Doesn't know what happens next (ParentPageController shows car
    /// selection), same as the other cards. Needs nothing from its creator, so
    /// it fetches in OnEnable.
    /// </summary>
    public class DealershipSelectionController : MonoBehaviour
    {
        private const string LogPrefix = "[NAS Dealerships]";

        private ScrollView _list;
        private VisualElement _status;
        private Label _statusLabel;
        private Button _retryButton;
        private IDealershipApi _api;
        private bool _loading;

        private void OnEnable()
        {
            var uiDocument = GetComponent<UIDocument>();
            if (uiDocument == null) return;

            var root = uiDocument.rootVisualElement;
            _list = root.Q<ScrollView>("dealership-list");
            _status = root.Q<VisualElement>("dealership-status");
            _statusLabel = root.Q<Label>("dealership-status-label");
            _retryButton = root.Q<Button>("dealership-retry-button");

            if (_retryButton != null) _retryButton.clicked += Load;
            Load();
        }

        private void OnDisable()
        {
            if (_retryButton != null) _retryButton.clicked -= Load;
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
                ShowStatus("No dealerships are available right now.", showRetry: true);
                return;
            }

            _status.style.display = DisplayStyle.None;
            var lastId = GameManager.Instance != null ? GameManager.Instance.LastDealershipId : 0;
            foreach (var dealership in dealerships)
                _list.Add(BuildCard(dealership, dealership.id == lastId));
        }

        private void ShowStatus(string message, bool showRetry)
        {
            _status.style.display = DisplayStyle.Flex;
            _statusLabel.text = message;
            _retryButton.style.display = showRetry ? DisplayStyle.Flex : DisplayStyle.None;
        }

        private VisualElement BuildCard(CustomerDealershipDto dealership, bool isLastChoice)
        {
            var card = new Button();
            card.AddToClassList("dealership-card");
            if (isLastChoice) card.AddToClassList("dealership-card--last");

            var accent = new VisualElement();
            accent.AddToClassList("dealership-card-accent");
            if (!string.IsNullOrEmpty(dealership.primaryColor) &&
                ColorUtility.TryParseHtmlString(dealership.primaryColor, out var brand))
                accent.style.backgroundColor = brand;
            card.Add(accent);

            var body = new VisualElement();
            body.AddToClassList("dealership-card-body");
            var name = new Label(dealership.name);
            name.AddToClassList("dealership-card-name");
            body.Add(name);
            foreach (var detail in Details(dealership))
            {
                var label = new Label(detail);
                label.AddToClassList("dealership-card-detail");
                body.Add(label);
            }
            card.Add(body);

            if (isLastChoice)
            {
                var badge = new Label("Last visited");
                badge.AddToClassList("dealership-card-badge");
                card.Add(badge);
            }

            card.clicked += () => OnDealershipTapped(dealership);
            return card;
        }

        private static IEnumerable<string> Details(CustomerDealershipDto dealership)
        {
            if (!string.IsNullOrWhiteSpace(dealership.address)) yield return dealership.address;
            if (!string.IsNullOrWhiteSpace(dealership.phone)) yield return dealership.phone;
        }

        private void OnDealershipTapped(CustomerDealershipDto dealership)
        {
            EventBus.Publish(new DealershipSelectedEvent(new DealershipInfo
            {
                id = dealership.id,
                name = dealership.name
            }));
        }
    }
}
