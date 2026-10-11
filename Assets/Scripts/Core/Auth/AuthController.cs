using UnityEngine;
using NAS.Core.Auth.Dtos;
using NAS.Core.Events;
using NAS.Core.Models;
using NAS.Core.Networking;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NAS.Core;

namespace NAS.Core.Auth
{
    /// <summary>
    /// Orchestrates auth requests and publishes domain events. UI and HTTP
    /// details stay outside the caller-facing event flow.
    /// </summary>
    public sealed class AuthController : MonoBehaviour
    {
        private const string LogPrefix = "[NAS Auth]";

        private ICustomerAuthApi _authApi;
        private AuthSession _session;
        private bool _requestInProgress;

        private static readonly Regex EmailRegex = new Regex(@"^[^\s@]+@[^\s@]+\.[^\s@]+$", RegexOptions.Compiled);
        private static readonly Regex CellNumberRegex = new Regex(@"^\+?[\d\s\-()]{7,15}$", RegexOptions.Compiled);

        public AuthSession Session => _session;

        private void Start()
        {
            // Reads GameManager.Instance here, not in Awake() - Unity doesn't
            // guarantee Awake() order across different GameObjects, but Start()
            // is guaranteed to run only after every object's Awake() has, so
            // GameManager.Instance is reliably set by this point.
            var resolved = EnvironmentResolver.Resolve(LogPrefix);

            if (resolved.Settings == null)
            {
                Debug.LogError($"{LogPrefix} ApiSettings is missing on AuthController.");
                return;
            }

            _session = new AuthSession(new TokenStorage(resolved.Settings.PersistToken));
            _authApi = new CustomerAuthApi(this, resolved.Settings, resolved.TrustAnyCertificate);
            Debug.Log($"{LogPrefix} Ready. API base URL: {resolved.Settings.BaseUrl}{(resolved.TrustAnyCertificate ? " (cert override)" : "")}");
        }

        private void OnEnable()
        {
            EventBus.Subscribe<LoginRequestedEvent>(HandleLoginRequested);
            EventBus.Subscribe<RegisterRequestedEvent>(HandleRegisterRequested);
            EventBus.Subscribe<LogoutRequestedEvent>(HandleLogoutRequested);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<LoginRequestedEvent>(HandleLoginRequested);
            EventBus.Unsubscribe<RegisterRequestedEvent>(HandleRegisterRequested);
            EventBus.Unsubscribe<LogoutRequestedEvent>(HandleLogoutRequested);
        }

        // Forgets the stored token. GameManager clears the rest of the session on the same event.
        private void HandleLogoutRequested(LogoutRequestedEvent evt) => Logout();

private void HandleLoginRequested(LoginRequestedEvent evt)
        {
            if (_requestInProgress)
            {
                Debug.Log($"{LogPrefix} Login ignored because another request is in progress.");
                return;
            }

            var errors = new Dictionary<string, string>();

            if (string.IsNullOrWhiteSpace(evt.Email))
                errors["email"] = "Email is required.";
            else if (!EmailRegex.IsMatch(evt.Email))
                errors["email"] = "Enter a valid email address.";

            if (string.IsNullOrWhiteSpace(evt.Password))
                errors["password"] = "Password is required.";
            else if (evt.Password.Length < 8)
                errors["password"] = "Password must be at least 8 characters.";

            if (errors.Count > 0)
            {
                PublishFieldErrors(errors);
                return;
            }

            if (_authApi == null)
            {
                PublishFailure("Authentication is not configured.");
                return;
            }

            _requestInProgress = true;
            Debug.Log($"{LogPrefix} Login request started.");
            _authApi.Login(new NAS.Core.Auth.Dtos.CustomerLoginRequest
            {
                email = evt.Email.Trim(),
                password = evt.Password
            }, HandleApiResult);
        }

private void HandleRegisterRequested(RegisterRequestedEvent evt)
        {
            if (_requestInProgress)
            {
                Debug.Log($"{LogPrefix} Registration ignored because another request is in progress.");
                return;
            }

            var errors = new Dictionary<string, string>();

            if (string.IsNullOrWhiteSpace(evt.FirstName))
                errors["firstName"] = "First name is required.";

            if (string.IsNullOrWhiteSpace(evt.LastName))
                errors["lastName"] = "Last name is required.";

            if (string.IsNullOrWhiteSpace(evt.CellNumber))
                errors["cellNumber"] = "Cell number is required.";
            else if (!CellNumberRegex.IsMatch(evt.CellNumber))
                errors["cellNumber"] = "Enter a valid cell number.";

            if (string.IsNullOrWhiteSpace(evt.Email))
                errors["email"] = "Email is required.";
            else if (!EmailRegex.IsMatch(evt.Email))
                errors["email"] = "Enter a valid email address.";

            if (string.IsNullOrWhiteSpace(evt.Password))
                errors["password"] = "Password is required.";
            else if (evt.Password.Length < 8)
                errors["password"] = "Password must be at least 8 characters.";

            if (string.IsNullOrWhiteSpace(evt.ConfirmPassword))
                errors["confirmPassword"] = "Please confirm your password.";
            else if (evt.ConfirmPassword != evt.Password)
                errors["confirmPassword"] = "Passwords do not match.";

            if (errors.Count > 0)
            {
                PublishFieldErrors(errors);
                return;
            }

            if (_authApi == null)
            {
                PublishFailure("Authentication is not configured.");
                return;
            }

            _requestInProgress = true;
            Debug.Log($"{LogPrefix} Registration request started.");
            _authApi.Register(new NAS.Core.Auth.Dtos.CustomerRegisterRequest
            {
                firstName = evt.FirstName.Trim(),
                lastName = evt.LastName.Trim(),
                cellNumber = evt.CellNumber.Trim(),
                email = evt.Email.Trim(),
                password = evt.Password
            }, HandleApiResult);
        }

        private void HandleApiResult(ApiResult<NAS.Core.Auth.Dtos.CustomerAuthResponse> result)
        {
            _requestInProgress = false;

            if (!result.Success)
            {
                Debug.LogWarning($"{LogPrefix} Authentication request failed. Status: {result.Error.StatusCode}, Code: {result.Error.ErrorCode ?? "unknown"}, Trace: {result.Error.TraceId ?? "none"}.");
                PublishFailure(result.Error.UserMessage);
                return;
            }

            try
            {
                _session.Apply(result.Value);
                Debug.Log($"{LogPrefix} Authentication succeeded for customer ID {_session.CurrentUser?.id}.");
                EventBus.Publish(new AuthSucceededEvent(_session.CurrentUser, _session.AccessToken));
            }
            catch (System.Exception exception)
            {
                Debug.LogException(exception);
                PublishFailure("The authentication response was invalid.");
            }
        }

        public void Logout()
        {
            Debug.Log($"{LogPrefix} Logout requested.");
            _session?.Logout();
        }

        private static void PublishFailure(string reason)
        {
            Debug.LogWarning($"{LogPrefix} Auth flow stopped: {reason}");
            EventBus.Publish(new AuthFailedEvent(reason));
        }
    

private static void PublishFieldErrors(Dictionary<string, string> errors)
        {
            Debug.LogWarning($"{LogPrefix} Validation failed: {string.Join(", ", errors.Values)}");
            EventBus.Publish(new AuthFailedEvent(null, errors));
        }
}
}
