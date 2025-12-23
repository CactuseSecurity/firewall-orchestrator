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

            int emailsSent = await notificationService.SendNotifications(owner, DateTime.Now.AddDays(-3), "email text");
            ClassicAssert.AreEqual(1, emailsSent);

            notificationService.Notifications.FirstOrDefault()!.LastSent = DateTime.Now.AddDays(-1);
            emailsSent = await notificationService.SendNotifications(owner, DateTime.Now.AddDays(-3), "email text");
            ClassicAssert.AreEqual(0, emailsSent);
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
