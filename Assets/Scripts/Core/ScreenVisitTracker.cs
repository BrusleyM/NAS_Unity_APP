using System;

namespace NAS.Core
{
    /// <summary>The screens whose time is recorded. snake_case - these go over the wire as-is.</summary>
    public static class ScreenNames
    {
        public const string Splash = "splash";
        public const string Login = "login";
        public const string Register = "register";
        public const string DealershipSelection = "dealership_selection";
        public const string CarSelection = "car_selection";
        public const string ArViewport = "ar_viewport";
        public const string Estimator = "estimator";
    }

    /// <summary>
    /// Times how long the customer spends on each screen. A second counter beside
    /// the overall session time: the screen times add up to time actually spent on
    /// screens, so the gap to the session time is idle/background time - and when
    /// the app is force-quit the visits up to the last screen change still exist.
    ///
    /// Pure logic (no Unity, no network) so it can be unit-tested: the owner tells it
    /// which screen is showing and when the app pauses/resumes/quits, and it calls
    /// onVisit for each finished stretch. While the app is paused the screen is
    /// closed (otherwise backgrounded time would count as screen time) and reopened
    /// on resume.
    /// </summary>
    public sealed class ScreenVisitTracker
    {
        public readonly struct Visit
        {
            public readonly string ClientId;
            public readonly string Screen;
            public readonly DateTime StartedAt;
            public readonly DateTime EndedAt;

            public Visit(string clientId, string screen, DateTime startedAt, DateTime endedAt)
            {
                ClientId = clientId;
                Screen = screen;
                StartedAt = startedAt;
                EndedAt = endedAt;
            }
        }

        // A screen that flashes by (e.g. re-routed straight on) isn't a visit.
        public const double MinSeconds = 0.5;

        private readonly Action<Visit> _onVisit;
        private readonly Func<DateTime> _now;
        private string _current;
        private DateTime _startedAt;
        // Set while paused: the screen to reopen on resume.
        private string _resumeScreen;

        public string CurrentScreen => _current;

        public ScreenVisitTracker(Action<Visit> onVisit, Func<DateTime> now = null)
        {
            _onVisit = onVisit ?? throw new ArgumentNullException(nameof(onVisit));
            _now = now ?? (() => DateTime.UtcNow);
        }

        /// <summary>A screen became visible. Closes the previous one.</summary>
        public void Show(string screen)
        {
            if (string.IsNullOrEmpty(screen)) return;

            // Changed screen while the app was in the background: it will be the one reopened.
            if (_current == null && _resumeScreen != null)
            {
                _resumeScreen = screen;
                return;
            }
            // Re-showing the same screen (the router re-renders cards) is not a new visit.
            if (_current == screen) return;

            Close();
            _current = screen;
            _startedAt = _now();
        }

        /// <summary>The app went to the background: close the current screen, remember it.</summary>
        public void Pause()
        {
            if (_current == null) return;
            var screen = _current;
            Close();
            _resumeScreen = screen;
        }

        /// <summary>The app came back: reopen the screen that was showing.</summary>
        public void Resume()
        {
            if (_resumeScreen == null) return;
            _current = _resumeScreen;
            _resumeScreen = null;
            _startedAt = _now();
        }

        /// <summary>The app is quitting: close the current screen for good.</summary>
        public void Stop()
        {
            Close();
            _resumeScreen = null;
        }

        private void Close()
        {
            if (_current == null) return;
            var end = _now();
            var screen = _current;
            _current = null;
            if ((end - _startedAt).TotalSeconds < MinSeconds) return;
            _onVisit(new Visit(Guid.NewGuid().ToString(), screen, _startedAt, end));
        }
    }
}
