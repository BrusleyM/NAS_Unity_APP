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
            EventBus.Subscribe<VerifyEmailRequestedEvent>(HandleVerifyEmailRequested);
            EventBus.Subscribe<ResendVerificationRequestedEvent>(HandleResendVerificationRequested);
            EventBus.Subscribe<PasswordResetCodeRequestedEvent>(HandlePasswordResetCodeRequested);
            EventBus.Subscribe<ResetPasswordRequestedEvent>(HandleResetPasswordRequested);
        }

        private void OnDisable()
        {
            EventBus.Unsubscribe<LoginRequestedEvent>(HandleLoginRequested);
            EventBus.Unsubscribe<RegisterRequestedEvent>(HandleRegisterRequested);
            EventBus.Unsubscribe<LogoutRequestedEvent>(HandleLogoutRequested);
            EventBus.Unsubscribe<VerifyEmailRequestedEvent>(HandleVerifyEmailRequested);
            EventBus.Unsubscribe<ResendVerificationRequestedEvent>(HandleResendVerificationRequested);
            EventBus.Unsubscribe<PasswordResetCodeRequestedEvent>(HandlePasswordResetCodeRequested);
            EventBus.Unsubscribe<ResetPasswordRequestedEvent>(HandleResetPasswordRequested);
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

            var identifierError = ProfileValidation.ValidateLoginIdentifier(evt.Email);
            if (identifierError != null)
                errors["email"] = identifierError;

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
            var identifier = evt.Email.Trim();
            Debug.Log($"{LogPrefix} Login request started.");
            _authApi.Login(new NAS.Core.Auth.Dtos.CustomerLoginRequest
            {
                email = identifier,
                password = evt.Password
            }, result => HandleLoginResult(identifier, result));
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
            }, result => HandleRegisterResult(evt.Email.Trim(), result));
        }

        private void HandleLoginResult(string identifier, ApiResult<NAS.Core.Auth.Dtos.CustomerAuthResponse> result)
        {
            if (!result.Success && result.Error.ErrorCode == "email_not_verified")
            {
                // Right password, but the address was never verified: send a fresh code and take them to the code screen.
                _requestInProgress = false;
                Debug.Log($"{LogPrefix} Sign-in needs email verification.");
                BeginVerification(identifier, AccountRecovery.LooksLikeEmail(identifier) ? identifier : string.Empty, sendNewCode: true);
                return;
            }
            HandleApiResult(result);
        }

        private void HandleRegisterResult(string email, ApiResult<NAS.Core.Auth.Dtos.CustomerAuthResponse> result)
        {
            if (result.Success && result.Value != null && result.Value.emailVerificationRequired)
            {
                // The server has emailed the code and issued no token yet.
                _requestInProgress = false;
                Debug.Log($"{LogPrefix} Registered; email verification required.");
                BeginVerification(email, email, sendNewCode: false);
                return;
            }
            HandleApiResult(result);
        }

        private void BeginVerification(string identifier, string email, bool sendNewCode)
        {
            // Best effort: if this fails the code screen's own "Resend code" is one tap away.
            if (sendNewCode && _authApi != null)
                _authApi.ResendVerification(new AccountIdentifierRequest { email = identifier }, _ => { });
            EventBus.Publish(new EmailVerificationRequiredEvent(identifier, email));
        }

        // ---- Verify email -------------------------------------------------------------------

        private void HandleVerifyEmailRequested(VerifyEmailRequestedEvent evt)
        {
            if (_requestInProgress) return;

            var codeError = ProfileValidation.ValidateCode(evt.Code);
            if (codeError != null)
            {
                PublishFieldErrors(new Dictionary<string, string> { ["code"] = codeError });
                return;
            }
            if (_authApi == null) { PublishFailure("Authentication is not configured."); return; }

            _requestInProgress = true;
            _authApi.VerifyEmail(new VerifyEmailRequest
            {
                email = evt.Identifier.Trim(),
                code = ProfileValidation.NormalizeCode(evt.Code)
            }, result =>
            {
                if (!result.Success) { _requestInProgress = false; PublishCodeFailure(result.Error); return; }
                HandleApiResult(result);   // the answer is a normal sign-in: token and customer
            });
        }

        private void HandleResendVerificationRequested(ResendVerificationRequestedEvent evt)
        {
            if (_requestInProgress || _authApi == null) return;
            _requestInProgress = true;
            _authApi.ResendVerification(new AccountIdentifierRequest { email = evt.Identifier.Trim() }, result =>
            {
                _requestInProgress = false;
                if (!result.Success) { PublishFailure(result.Error.UserMessage); return; }
                EventBus.Publish(new CodeSentEvent(evt.Identifier));
            });
        }

        // ---- Forgot password ----------------------------------------------------------------

        private void HandlePasswordResetCodeRequested(PasswordResetCodeRequestedEvent evt)
        {
            if (_requestInProgress) return;

            var identifierError = ProfileValidation.ValidateLoginIdentifier(evt.Identifier);
            if (identifierError != null)
            {
                PublishFieldErrors(new Dictionary<string, string> { ["email"] = identifierError });
                return;
            }
            if (_authApi == null) { PublishFailure("Authentication is not configured."); return; }

            _requestInProgress = true;
            var identifier = evt.Identifier.Trim();
            _authApi.ForgotPassword(new AccountIdentifierRequest { email = identifier }, result =>
            {
                _requestInProgress = false;
                if (!result.Success) { PublishFailure(result.Error.UserMessage); return; }
                // Same answer whether or not the account exists, so this never says which.
                EventBus.Publish(new PasswordResetCodeSentEvent(identifier));
            });
        }

        private void HandleResetPasswordRequested(ResetPasswordRequestedEvent evt)
        {
            if (_requestInProgress) return;

            var errors = ProfileValidation.ValidatePasswordReset(evt.Code, evt.NewPassword, evt.ConfirmPassword);
            if (errors.Count > 0)
            {
                PublishFieldErrors(errors);
                return;
            }
            if (_authApi == null) { PublishFailure("Authentication is not configured."); return; }

            _requestInProgress = true;
            _authApi.ResetPassword(new ResetPasswordRequest
            {
                email = evt.Identifier.Trim(),
                code = ProfileValidation.NormalizeCode(evt.Code),
                newPassword = evt.NewPassword
            }, result =>
            {
                _requestInProgress = false;
                if (!result.Success) { PublishCodeFailure(result.Error); return; }
                Debug.Log($"{LogPrefix} Password reset completed.");
                EventBus.Publish(new PasswordResetCompletedEvent());
            });
        }

        // A wrong, used-up or expired code belongs on the code box, whatever screen it was typed on.
        private static void PublishCodeFailure(ApiError error)
        {
            Debug.LogWarning($"{LogPrefix} Code request failed. Status {error.StatusCode}, code {error.ErrorCode ?? "unknown"}, trace {error.TraceId ?? "none"}.");
            if (error.ErrorCode == "code_invalid" || error.ErrorCode == "code_expired" || error.ErrorCode == "too_many_attempts")
                PublishFieldErrors(new Dictionary<string, string> { ["code"] = error.UserMessage });
            else
                PublishFailure(error.UserMessage);
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
