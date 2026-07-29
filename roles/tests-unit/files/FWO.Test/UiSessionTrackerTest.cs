using FWO.Ui.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    [FixtureLifeCycle(LifeCycle.InstancePerTestCase)]
    internal class UiSessionTrackerTest
    {
        private DateTime clock = new(2026, 7, 29, 10, 0, 0, DateTimeKind.Utc);

        private UiSessionTracker CreateTracker()
        {
            return new UiSessionTracker(() => clock);
        }

        [Test]
        public void Register_AddsOpenSession()
        {
            UiSessionTracker tracker = CreateTracker();

            tracker.Register("session1");
            UiSessionOverview overview = tracker.GetOverview();

            Assert.Multiple(() =>
            {
                Assert.That(overview.OpenSessions, Is.EqualTo(1));
                Assert.That(overview.AuthenticatedSessions, Is.EqualTo(0));
                Assert.That(overview.LoggedInUsers, Is.EqualTo(0));
                Assert.That(overview.Sessions[0].SessionId, Is.EqualTo("session1"));
                Assert.That(overview.Sessions[0].OpenedAt, Is.EqualTo(clock));
            });
        }

        [Test]
        public void Register_IgnoresEmptySessionId()
        {
            UiSessionTracker tracker = CreateTracker();

            tracker.Register("");
            tracker.Register("   ");

            Assert.That(tracker.GetOverview().OpenSessions, Is.EqualTo(0));
        }

        [Test]
        public void Unregister_RemovesSession()
        {
            UiSessionTracker tracker = CreateTracker();
            tracker.Register("session1");

            tracker.Unregister("session1");

            Assert.That(tracker.GetOverview().OpenSessions, Is.EqualTo(0));
        }

        [Test]
        public void Unregister_UnknownSessionIsIgnored()
        {
            UiSessionTracker tracker = CreateTracker();
            tracker.Register("session1");

            tracker.Unregister("unknown");

            Assert.That(tracker.GetOverview().OpenSessions, Is.EqualTo(1));
        }

        [Test]
        public void SetUser_MarksSessionAuthenticated()
        {
            UiSessionTracker tracker = CreateTracker();
            tracker.Register("session1");

            tracker.SetUser("session1", "tim", "uid=tim,ou=operator");
            UiSessionOverview overview = tracker.GetOverview();

            Assert.Multiple(() =>
            {
                Assert.That(overview.AuthenticatedSessions, Is.EqualTo(1));
                Assert.That(overview.LoggedInUsers, Is.EqualTo(1));
                Assert.That(overview.Sessions[0].UserName, Is.EqualTo("tim"));
                Assert.That(overview.Sessions[0].UserDn, Is.EqualTo("uid=tim,ou=operator"));
                Assert.That(overview.Sessions[0].Authenticated, Is.True);
            });
        }

        [Test]
        public void SetUser_CountsOneUserWithSeveralSessionsOnce()
        {
            UiSessionTracker tracker = CreateTracker();
            tracker.Register("session1");
            tracker.Register("session2");
            tracker.Register("session3");

            tracker.SetUser("session1", "tim");
            tracker.SetUser("session2", "TIM");
            tracker.SetUser("session3", "auditor");
            UiSessionOverview overview = tracker.GetOverview();

            Assert.Multiple(() =>
            {
                Assert.That(overview.OpenSessions, Is.EqualTo(3));
                Assert.That(overview.AuthenticatedSessions, Is.EqualTo(3));
                Assert.That(overview.LoggedInUsers, Is.EqualTo(2));
            });
        }

        [Test]
        public void SetUser_WithEmptyNameMarksSessionAnonymousAgain()
        {
            UiSessionTracker tracker = CreateTracker();
            tracker.Register("session1");
            tracker.SetUser("session1", "tim");

            tracker.SetUser("session1", null);
            UiSessionOverview overview = tracker.GetOverview();

            Assert.Multiple(() =>
            {
                Assert.That(overview.OpenSessions, Is.EqualTo(1));
                Assert.That(overview.AuthenticatedSessions, Is.EqualTo(0));
                Assert.That(overview.LoggedInUsers, Is.EqualTo(0));
            });
        }

        [Test]
        public void SetUser_UnknownSessionIsIgnored()
        {
            UiSessionTracker tracker = CreateTracker();

            tracker.SetUser("unknown", "tim");

            Assert.That(tracker.GetOverview().OpenSessions, Is.EqualTo(0));
        }

        [Test]
        public void SetConnected_UpdatesConnectionStateAndActivity()
        {
            UiSessionTracker tracker = CreateTracker();
            tracker.Register("session1");
            DateTime openedAt = clock;

            clock = clock.AddMinutes(5);
            tracker.SetConnected("session1", true);
            UiSessionOverview connected = tracker.GetOverview();

            tracker.SetConnected("session1", false);
            UiSessionOverview disconnected = tracker.GetOverview();

            Assert.Multiple(() =>
            {
                Assert.That(connected.ConnectedSessions, Is.EqualTo(1));
                Assert.That(connected.Sessions[0].OpenedAt, Is.EqualTo(openedAt));
                Assert.That(connected.Sessions[0].LastActivity, Is.EqualTo(openedAt.AddMinutes(5)));
                Assert.That(disconnected.ConnectedSessions, Is.EqualTo(0));
            });
        }

        [Test]
        public void GetOverview_OrdersSessionsByOpeningTime()
        {
            UiSessionTracker tracker = CreateTracker();
            tracker.Register("second");
            clock = clock.AddMinutes(-10);
            tracker.Register("first");

            UiSessionOverview overview = tracker.GetOverview();

            Assert.Multiple(() =>
            {
                Assert.That(overview.Sessions[0].SessionId, Is.EqualTo("first"));
                Assert.That(overview.Sessions[1].SessionId, Is.EqualTo("second"));
            });
        }

        [Test]
        public void Tracker_UsesSystemClockByDefault()
        {
            UiSessionTracker tracker = new();
            DateTime before = DateTime.UtcNow;

            tracker.Register("session1");
            UiSession session = tracker.GetOverview().Sessions[0];

            Assert.That(session.OpenedAt, Is.InRange(before, DateTime.UtcNow));
        }
    }
}
