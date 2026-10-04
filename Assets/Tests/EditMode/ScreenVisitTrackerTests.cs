using System;
using System.Collections.Generic;
using System.Linq;
using NAS.Core;
using NUnit.Framework;

namespace NAS.Tests
{
    public class ScreenVisitTrackerTests
    {
        private DateTime _now;
        private List<ScreenVisitTracker.Visit> _visits;
        private ScreenVisitTracker _tracker;

        [SetUp]
        public void SetUp()
        {
            _now = new DateTime(2026, 10, 4, 12, 0, 0, DateTimeKind.Utc);
            _visits = new List<ScreenVisitTracker.Visit>();
            _tracker = new ScreenVisitTracker(_visits.Add, () => _now);
        }

        private void Advance(double seconds) => _now = _now.AddSeconds(seconds);

        [Test]
        public void MovingToAnotherScreenRecordsTimeOnTheOneLeft()
        {
            _tracker.Show(ScreenNames.CarSelection);
            Advance(42);
            _tracker.Show(ScreenNames.ArViewport);

            Assert.AreEqual(1, _visits.Count);
            Assert.AreEqual(ScreenNames.CarSelection, _visits[0].Screen);
            Assert.AreEqual(42, (_visits[0].EndedAt - _visits[0].StartedAt).TotalSeconds, 0.001);
            Assert.AreEqual(ScreenNames.ArViewport, _tracker.CurrentScreen);
        }

        [Test]
        public void NothingIsSentUntilTheCustomerLeavesTheScreen()
        {
            _tracker.Show(ScreenNames.Login);
            Advance(30);
            Assert.IsEmpty(_visits);
        }

        [Test]
        public void ShowingTheSameScreenAgainDoesNotStartANewVisit()
        {
            _tracker.Show(ScreenNames.CarSelection);
            Advance(10);
            _tracker.Show(ScreenNames.CarSelection); // the router re-rendered the card
            Advance(10);
            _tracker.Show(ScreenNames.Estimator);

            Assert.AreEqual(1, _visits.Count);
            Assert.AreEqual(20, (_visits[0].EndedAt - _visits[0].StartedAt).TotalSeconds, 0.001);
        }

        [Test]
        public void AScreenThatFlashesByIsNotAVisit()
        {
            _tracker.Show(ScreenNames.Login);
            Advance(0.2);
            _tracker.Show(ScreenNames.CarSelection);
            Advance(5);
            _tracker.Show(ScreenNames.Estimator);

            Assert.AreEqual(1, _visits.Count);
            Assert.AreEqual(ScreenNames.CarSelection, _visits[0].Screen);
        }

        [Test]
        public void BackgroundedTimeIsNotCountedAsTimeOnScreen()
        {
            _tracker.Show(ScreenNames.Estimator);
            Advance(20);
            _tracker.Pause();          // app backgrounded: the visit is closed and sent now
            Advance(600);              // ten minutes in the background
            _tracker.Resume();
            Advance(15);
            _tracker.Show(ScreenNames.CarSelection);

            Assert.AreEqual(2, _visits.Count);
            Assert.AreEqual(ScreenNames.Estimator, _visits[0].Screen);
            Assert.AreEqual(20, (_visits[0].EndedAt - _visits[0].StartedAt).TotalSeconds, 0.001);
            Assert.AreEqual(ScreenNames.Estimator, _visits[1].Screen);   // reopened on resume
            Assert.AreEqual(15, (_visits[1].EndedAt - _visits[1].StartedAt).TotalSeconds, 0.001);
        }

        [Test]
        public void AVisitIsSentAtPauseSoAForceQuitLosesNothingBeforeIt()
        {
            _tracker.Show(ScreenNames.CarSelection);
            Advance(90);
            _tracker.Pause();

            Assert.AreEqual(1, _visits.Count);
            Assert.AreEqual(90, (_visits[0].EndedAt - _visits[0].StartedAt).TotalSeconds, 0.001);
        }

        [Test]
        public void IfTheScreenChangesWhileBackgroundedTheNewOneIsReopenedOnResume()
        {
            _tracker.Show(ScreenNames.CarSelection);
            Advance(10);
            _tracker.Pause();
            _tracker.Show(ScreenNames.Estimator);   // changed while paused
            Advance(300);
            _tracker.Resume();
            Advance(8);
            _tracker.Stop();

            Assert.AreEqual(2, _visits.Count);
            Assert.AreEqual(ScreenNames.CarSelection, _visits[0].Screen);
            Assert.AreEqual(ScreenNames.Estimator, _visits[1].Screen);
            Assert.AreEqual(8, (_visits[1].EndedAt - _visits[1].StartedAt).TotalSeconds, 0.001);
        }

        [Test]
        public void QuittingClosesTheCurrentScreenAndDoesNotReopenIt()
        {
            _tracker.Show(ScreenNames.ArViewport);
            Advance(25);
            _tracker.Stop();
            _tracker.Resume();
            Advance(30);
            _tracker.Stop();

            Assert.AreEqual(1, _visits.Count);
            Assert.AreEqual(25, (_visits[0].EndedAt - _visits[0].StartedAt).TotalSeconds, 0.001);
            Assert.IsNull(_tracker.CurrentScreen);
        }

        [Test]
        public void PausingWithNothingShownDoesNothing()
        {
            _tracker.Pause();
            _tracker.Resume();
            _tracker.Stop();
            Assert.IsEmpty(_visits);
        }

        [Test]
        public void EveryVisitGetsItsOwnIdSoARetryCanBeRecognisedButTwoVisitsAreNot()
        {
            _tracker.Show(ScreenNames.Login);
            Advance(5);
            _tracker.Show(ScreenNames.CarSelection);
            Advance(5);
            _tracker.Show(ScreenNames.Estimator);
            Advance(5);
            _tracker.Stop();

            Assert.AreEqual(3, _visits.Count);
            Assert.AreEqual(3, _visits.Select(v => v.ClientId).Distinct().Count());
            Assert.IsTrue(_visits.All(v => !string.IsNullOrEmpty(v.ClientId)));
        }

        [Test]
        public void ScreenNamesAreSnakeCaseWhichIsWhatTheBackendAccepts()
        {
            var names = new[]
            {
                ScreenNames.Splash, ScreenNames.Login, ScreenNames.Register, ScreenNames.DealershipSelection,
                ScreenNames.CarSelection, ScreenNames.ArViewport, ScreenNames.Estimator
            };
            foreach (var name in names)
                StringAssert.IsMatch("^[a-z][a-z0-9_]{0,49}$", name);
            Assert.AreEqual(names.Length, names.Distinct().Count());
        }
    }
}
