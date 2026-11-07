using System;
using System.Collections.Generic;

namespace NAS.Core
{
    /// <summary>
    /// Which of a dealership's customer-facing features work, as the customer's app needs to know it (NAS_Backend `PlatformFeatures`: the owner switches
    /// them per dealership; the server refuses an action whose feature is off whatever the app shows, so hiding is convenience, not security).
    /// No Unity, no UI: unit-tested.
    ///
    /// Rules, all in one place:
    ///   - AR is always on. Whatever the server says, the app never hides it (it is the product itself).
    ///   - Anything not mentioned is on: a feature the server does not list (an older server, a feature added later) is not hidden.
    ///   - Nothing known (the call failed, or has not answered yet) means everything on: the customer keeps the full pilot rather than losing a
    ///     feature because of a bad connection. The server still refuses what really is off.
    /// </summary>
    public sealed class DealershipFeatureSet
    {
        public const string Ar = "ar";
        public const string Finance = "finance";
        public const string TestDrive = "test_drive";

        /// <summary>The errorCode of the 403 the server answers when an action belongs to a feature the dealership does not have (NAS_Backend DealershipFeatures.ErrorCode).</summary>
        public const string FeatureDisabledErrorCode = "feature_disabled";

        public static readonly DealershipFeatureSet AllOn = new DealershipFeatureSet(Array.Empty<string>());

        private readonly HashSet<string> _off;

        private DealershipFeatureSet(IEnumerable<string> off)
        {
            _off = new HashSet<string>(off, StringComparer.Ordinal);
            _off.Remove(Ar);
        }

        /// <summary>From (feature key, works) pairs as the server sent them. Null or empty gives everything on.</summary>
        public static DealershipFeatureSet From(IEnumerable<KeyValuePair<string, bool>> states)
        {
            if (states == null) return AllOn;
            var off = new List<string>();
            foreach (var state in states)
                if (!state.Value && !string.IsNullOrEmpty(state.Key)) off.Add(state.Key);
            return off.Count == 0 ? AllOn : new DealershipFeatureSet(off);
        }

        public bool IsEnabled(string key) => string.IsNullOrEmpty(key) || !_off.Contains(key);

        /// <summary>
        /// This set with one more feature off: for when the server has just refused an action with 403 feature_disabled, which proves it is off whatever an earlier answer
        /// (or a failed one) said. AR cannot be switched off, and an unknown or empty key changes nothing.
        /// </summary>
        public DealershipFeatureSet WithOff(string key)
        {
            if (string.IsNullOrEmpty(key) || key == Ar || _off.Contains(key)) return this;
            var off = new List<string>(_off) { key };
            return new DealershipFeatureSet(off);
        }

        /// <summary>
        /// Whether an answer about <paramref name="answeredDealershipId"/> still belongs to the customer: they are still looking at that dealership and are still signed in.
        /// (A token that was refreshed meanwhile is fine: features belong to the dealership, not to the token.)
        /// </summary>
        public static bool AnswerStillApplies(int? selectedDealershipId, int answeredDealershipId, string accessToken) =>
            selectedDealershipId.HasValue && selectedDealershipId.Value == answeredDealershipId && !string.IsNullOrEmpty(accessToken);

        /// <summary>The estimate (the finance calculator) after placing a car.</summary>
        public bool EstimateAvailable => IsEnabled(Finance);

        /// <summary>The "Request test drive" button in the AR view.</summary>
        public bool TestDriveAvailable => IsEnabled(TestDrive);
    }
}
