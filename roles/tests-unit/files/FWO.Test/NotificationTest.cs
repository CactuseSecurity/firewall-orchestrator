using NUnit.Framework;
using NUnit.Framework.Legacy;
using FWO.Data;
using FWO.Middleware.Server;
using FWO.Report;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class NotificationTest
    {
        readonly NotificationTestApiConn apiConnection = new();
        readonly SimulatedGlobalConfig globalConfig = new(){ UseDummyEmailAddress = true, DummyEmailAddress = "x@y.de"};

        [Test]
        public async Task TestInterfaceRequestNotification()
        {
            List<UserGroup> ownerGroups = [];
            NotificationService notificationService = await NotificationService.CreateAsync(NotificationClient.InterfaceRequest, globalConfig, apiConnection, ownerGroups);
            FwoOwner owner = new();

            int emailsSent = await notificationService.SendNotifications(owner, DateTime.Now.AddDays(-8), "email text");
            ClassicAssert.AreEqual(2, emailsSent);
            ClassicAssert.AreEqual(2, await notificationService.UpdateNotificationsLastSent());

            notificationService.Notifications[0].LastSent = DateTime.Now.AddDays(-1);
            emailsSent = await notificationService.SendNotifications(owner, DateTime.Now.AddDays(-8), "email text");
            ClassicAssert.AreEqual(1, emailsSent);
            ClassicAssert.AreEqual(1, await notificationService.UpdateNotificationsLastSent());

            notificationService.Notifications[0].LastSent = DateTime.Now.AddDays(-1);
            notificationService.Notifications[1].LastSent = DateTime.Now.AddDays(-8);
            emailsSent = await notificationService.SendNotifications(owner, DateTime.Now.AddDays(-15), "email text");
            ClassicAssert.AreEqual(0, emailsSent);
            ClassicAssert.AreEqual(0, await notificationService.UpdateNotificationsLastSent());

            notificationService.Notifications[1].InitialOffsetAfterDeadline = 7;
            emailsSent = await notificationService.SendNotifications(owner, DateTime.Now.AddDays(-15), "email text");
            ClassicAssert.AreEqual(1, emailsSent);
        }

        [Test]
        public async Task TestRecertNotification()
        {
            List<UserGroup> ownerGroups = [];
            NotificationService notificationService = await NotificationService.CreateAsync(NotificationClient.Recertification, globalConfig, apiConnection, ownerGroups);
            FwoOwner owner = new(){ NextRecertDate = DateTime.Now.AddDays(21)};

            int emailsSent = await notificationService.SendNotifications(owner, null, "email text", new ReportRecertEvent(new(""), new(globalConfig), Basics.ReportType.RecertificationEvent){});
            ClassicAssert.AreEqual(1, emailsSent);

            notificationService.Notifications.FirstOrDefault()!.LastSent = DateTime.Now;
            emailsSent = await notificationService.SendNotifications(owner, null, "email text");
            ClassicAssert.AreEqual(0, emailsSent);

            notificationService.Notifications.FirstOrDefault()!.LastSent = DateTime.Now.AddDays(-7);
            owner.NextRecertDate = DateTime.Now.AddDays(-7);
            emailsSent = await notificationService.SendNotifications(owner, null, "email text");
            ClassicAssert.AreEqual(1, emailsSent);

            notificationService.Notifications.FirstOrDefault()!.LastSent = DateTime.Now.AddDays(-7);
            owner.NextRecertDate = DateTime.Now.AddDays(-14);
            emailsSent = await notificationService.SendNotifications(owner, null, "email text");
            ClassicAssert.AreEqual(0, emailsSent);
        }
    }
}
