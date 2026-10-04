using System.Collections.Generic;
using NAS.Core.Models;
using UnityEngine;

namespace NAS.Core.Events
{
    // ---- Auth flow -------------------------------------------------------

    /// <summary>Raised by LoginCardController when the user taps "Log in".</summary>
    public readonly struct LoginRequestedEvent
    {
        public readonly string Email;
        public readonly string Password;
        public LoginRequestedEvent(string email, string password)
        {
            Email = email;
            Password = password;
        }
    }

    /// <summary>Raised by RegisterCardController when the user taps "Register".</summary>
    public readonly struct RegisterRequestedEvent
    {
        public readonly string FirstName;
        public readonly string LastName;
        public readonly string CellNumber;
        public readonly string Email;
        public readonly string Password;
        public readonly string ConfirmPassword;

        public RegisterRequestedEvent(string firstName, string lastName, string cellNumber, string email, string password, string confirmPassword)
        {
            FirstName = firstName;
            LastName = lastName;
            CellNumber = cellNumber;
            Email = email;
            Password = password;
            ConfirmPassword = confirmPassword;
        }
    }

    /// <summary>
    /// Raised by AuthController once login or registration succeeds. This is
    /// the raw fact - GameManager is the only thing that should subscribe to
    /// it directly. Anything else that needs to react to a completed login
    /// (e.g. to navigate) should subscribe to SessionAuthenticatedEvent
    /// instead, which GameManager publishes AFTER CurrentUser/AccessToken are
    /// already set. Subscribing to this one directly risks reading
    /// GameManager.Instance before it's actually updated - Unity doesn't
    /// guarantee GameManager's handler runs first just because two scripts
    /// both subscribed to the same event (this caused a real bug: a vehicle
    /// catalog request went out with a null token because the screen that
    /// triggered it read GameManager.AccessToken before GameManager's own
    /// handler for this same event had set it).
    /// </summary>
    public readonly struct AuthSucceededEvent
    {
        public readonly User User;
        public readonly string AccessToken;
        public AuthSucceededEvent(User user, string accessToken)
        {
            User = user;
            AccessToken = accessToken;
        }
    }

    /// <summary>
    /// Raised by GameManager right after it applies an AuthSucceededEvent to
    /// its own CurrentUser/AccessToken. Subscribe to THIS, not
    /// AuthSucceededEvent, if you need to react to login and might read
    /// GameManager.Instance state while doing so - see AuthSucceededEvent's
    /// doc comment for why.
    /// </summary>
    public readonly struct SessionAuthenticatedEvent
    {
        public readonly User User;
        public readonly string AccessToken;
        public SessionAuthenticatedEvent(User user, string accessToken)
        {
            User = user;
            AccessToken = accessToken;
        }
    }

    /// <summary>Raised by AuthController when login or registration fails validation/the backend.</summary>
    public readonly struct AuthFailedEvent
    {
        /// <summary>General/non-field-specific error (e.g. from the backend: wrong credentials, network error).</summary>
        public readonly string Reason;
        /// <summary>Per-field validation errors (e.g. "email" -> "Enter a valid email address."). Null for a general/backend failure instead.</summary>
        public readonly IReadOnlyDictionary<string, string> FieldErrors;

        public AuthFailedEvent(string reason, IReadOnlyDictionary<string, string> fieldErrors = null)
        {
            Reason = reason;
            FieldErrors = fieldErrors;
        }
    }

    // ---- Navigation --------------------------------------------------
    // Cards raise these when they want to move to another screen but shouldn't
    // know that ParentPageController (or anything else) is the thing listening.

    public readonly struct NavigateToRegisterRequestedEvent { }
    public readonly struct NavigateToLoginRequestedEvent { }

    /// <summary>Raised by SplashScreenController when the user taps "Get Started" on the splash card.</summary>
    public readonly struct SplashDismissedEvent { }

    // ---- Car selection flow -------------------------------------------------------

    /// <summary>
    /// Raised by CarSelectionScreenController when the user picks a car and
    /// starts AR. The raw fact - GameManager is the only thing that should
    /// subscribe to it directly (same reasoning as AuthSucceededEvent above).
    /// Anything else reacting to car selection should subscribe to
    /// SessionCarSelectedEvent instead.
    /// </summary>
    public readonly struct CarSelectedEvent
    {
        public readonly VehicleInfo Vehicle;
        public CarSelectedEvent(VehicleInfo vehicle) => Vehicle = vehicle;
    }

    /// <summary>
    /// Raised by GameManager right after it applies a CarSelectedEvent to its
    /// own SelectedCar. Subscribe to THIS, not CarSelectedEvent, if you need
    /// to react to car selection and might read GameManager.Instance state
    /// while doing so.
    /// </summary>
    public readonly struct SessionCarSelectedEvent
    {
        public readonly VehicleInfo Vehicle;
        public SessionCarSelectedEvent(VehicleInfo vehicle) => Vehicle = vehicle;
    }

    // ---- Dealership selection -----------------------------------------------------
    // The customer chooses which dealership they're buying from (QR-code
    // scanning is a possible v2). Same raw/Session* split as car selection:
    // GameManager is the only direct subscriber to the raw event.

    /// <summary>
    /// Raised by DealershipSelectionController when the user picks a dealership.
    /// The raw fact - GameManager is the only thing that should subscribe to it
    /// directly; everything else should use SessionDealershipSelectedEvent.
    /// </summary>
    public readonly struct DealershipSelectedEvent
    {
        public readonly DealershipInfo Dealership;
        public DealershipSelectedEvent(DealershipInfo dealership) => Dealership = dealership;
    }

    /// <summary>
    /// Raised by GameManager right after it applies a DealershipSelectedEvent to
    /// its own SelectedDealership (and cleared any car chosen from another
    /// dealership). Subscribe to THIS if you need to read GameManager state.
    /// </summary>
    public readonly struct SessionDealershipSelectedEvent
    {
        public readonly DealershipInfo Dealership;
        public SessionDealershipSelectedEvent(DealershipInfo dealership) => Dealership = dealership;
    }

    /// <summary>Raised by the car selection screen's "Change dealership" control - ParentPageController reacts by showing the dealership list again.</summary>
    public readonly struct ChangeDealershipRequestedEvent { }

    // ---- AR scene lifecycle -------------------------------------------------------
    // AR Scene is loaded additively exactly once (see GameManager.IsArSceneLoaded)
    // and never reloaded after that - these two drive showing/hiding it on
    // every entry/exit after the first, instead of a SceneManager.LoadScene
    // reload (which was destroying and recreating ARSession/XROrigin every
    // time, causing a black camera feed on the second+ AR entry).

    /// <summary>Raised by ParentPageController when AR Scene is already loaded and the user is entering AR again (second+ visit this app session).</summary>
    public readonly struct EnterArRequestedEvent { }

    /// <summary>Raised by ArViewportController when the user backs out or confirms, to hand control back to ParentPageController without reloading either scene.</summary>
    public readonly struct ExitArRequestedEvent { }

    /// <summary>Raised by AR flow controllers when the user backs out to the estimator card.</summary>
    public readonly struct ReturnToEstimatorRequestedEvent { }

    /// <summary>Raised by ObjectPlacerController right after it instantiates the placed car in the AR scene.</summary>
    public readonly struct CarPlacedEvent
    {
        public readonly GameObject Instance;
        public CarPlacedEvent(GameObject instance) => Instance = instance;
    }

    /// <summary>Raised by ArViewportController when the user taps a paint swatch in the Customize sheet.</summary>
    public readonly struct PaintColorSelectedEvent
    {
        public readonly string HexCode;
        public PaintColorSelectedEvent(string hexCode) => HexCode = hexCode;
    }

    /// <summary>Raised by ArViewportController whenever the Customize sheet opens/closes, so CarManipulationController can suspend touch manipulation while the user is tapping swatches instead of dragging the car underneath them.</summary>
    public readonly struct CustomizeSheetToggledEvent
    {
        public readonly bool IsOpen;
        public CustomizeSheetToggledEvent(bool isOpen) => IsOpen = isOpen;
    }

    /// <summary>Raised by ArViewportController when the user first touches down on the rotation slider, so CarManipulationController can suspend raw touch reposition/pinch reading for the duration of the drag - otherwise the same finger touch is also read as a reposition drag on the car underneath (most visible once the slider hits -180/180 and stops producing RotationSliderChangedEvents, but the finger is still moving).</summary>
    public readonly struct RotationSliderGrabbedEvent { }

    /// <summary>Raised by ArViewportController's rotation slider on every value change while the user is dragging it - applied live to the placed car by CarManipulationController.</summary>
    public readonly struct RotationSliderChangedEvent
    {
        public readonly float Degrees;
        public RotationSliderChangedEvent(float degrees) => Degrees = degrees;
    }

    /// <summary>Raised by ArViewportController when the user releases the rotation slider, so CarManipulationController knows one rotation "gesture" ended (for threshold-based interaction counting, same idea as lifting a finger off a touch drag) and can resume raw touch reading.</summary>
    public readonly struct RotationSliderReleasedEvent { }

    /// <summary>Raised by ArViewportController when the user first touches down on the vertical-offset slider - same reasoning as RotationSliderGrabbedEvent, for the same "raw touch reading fights the slider" problem.</summary>
    public readonly struct VerticalOffsetSliderGrabbedEvent { }

    /// <summary>Raised by ArViewportController's vertical-offset slider on every value change while the user is dragging it - applied live to the placed car's local Y by CarManipulationController, correcting for anchor/tracking height error without touching X/Z drag or rotation.</summary>
    public readonly struct VerticalOffsetSliderChangedEvent
    {
        public readonly float OffsetMeters;
        public VerticalOffsetSliderChangedEvent(float offsetMeters) => OffsetMeters = offsetMeters;
    }

    /// <summary>Raised by ArViewportController when the user releases the vertical-offset slider - same reasoning as RotationSliderReleasedEvent.</summary>
    public readonly struct VerticalOffsetSliderReleasedEvent { }

    /// <summary>Raised by ArViewportController's reset-position button - CarManipulationController clears any drag/vertical-offset correction on the placed car, snapping its local position back to exactly where its anchor sits. Rotation and scale are untouched - each already has its own dedicated control.</summary>
    public readonly struct CarPositionResetRequestedEvent { }

    /// <summary>Raised by CarManipulationController whenever repositionCount/scaleCount/rotationCount change, so ObjectPlacerController can fold them into the ArSession telemetry it owns sending.</summary>
    public readonly struct GestureCountsUpdatedEvent
    {
        public readonly int RepositionCount;
        public readonly int ScaleCount;
        public readonly int RotationCount;
        public GestureCountsUpdatedEvent(int repositionCount, int scaleCount, int rotationCount)
        {
            RepositionCount = repositionCount;
            ScaleCount = scaleCount;
            RotationCount = rotationCount;
        }
    }

    /// <summary>Raised by CarManipulationController whenever the placed car's scale multiplier changes, so ArViewportController can show the live real:virtual ratio next to the "Pinch to scale" hint.</summary>
    public readonly struct CarScaleChangedEvent
    {
        public readonly float Multiplier;
        public CarScaleChangedEvent(float multiplier) => Multiplier = multiplier;
    }

    // ---- Estimator flow -------------------------------------------------------

    /// <summary>Raised by EstimatorCardController right before it calls the submit-estimate API, so EstimateConfirmationOverlayController can show its spinner state.</summary>
    public readonly struct EstimateSubmissionStartedEvent { }

    /// <summary>Raised by EstimatorCardController when the user sends the estimate to the dealer.</summary>
    public readonly struct EstimateSubmittedEvent
    {
        public readonly VehicleInfo Vehicle;
        public readonly float FinancedAmount;
        public readonly float MonthlyPayment;
        public EstimateSubmittedEvent(VehicleInfo vehicle, float financedAmount, float monthlyPayment)
        {
            Vehicle = vehicle;
            FinancedAmount = financedAmount;
            MonthlyPayment = monthlyPayment;
        }
    }

    /// <summary>Raised by EstimatorCardController if the submit-estimate API call fails, so EstimateConfirmationOverlayController hides its overlay and lets the existing inline error label do its job.</summary>
    public readonly struct EstimateSubmissionFailedEvent { }

    /// <summary>Raised by EstimateConfirmationOverlayController's "Back to car selection" button - ParentPageController reacts by showing the car selection screen, same as its other pure-router event handlers.</summary>
    public readonly struct ReturnToCarSelectionRequestedEvent { }

    // ---- Global loading overlay -------------------------------------------------------
    // LoadingOverlayController lives on a child of the persistent Game Manager
    // GameObject (DontDestroyOnLoad) - any async process anywhere in the app,
    // regardless of which scene is currently loaded, can show/hide the same
    // overlay by raising these instead of each scene needing its own loading UI.

    /// <summary>Raised by an async process that should block interaction behind a loading overlay until it finishes.</summary>
    public readonly struct LoadingStartedEvent
    {
        public readonly string Message;
        public LoadingStartedEvent(string message = null) => Message = message;
    }

    /// <summary>Raised once the async process from a matching LoadingStartedEvent finishes (success or failure) to hide the overlay again.</summary>
    public readonly struct LoadingFinishedEvent { }
}
