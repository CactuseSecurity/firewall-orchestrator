using FWO.Data;
using FWO.Services;
using NUnit.Framework;
using static FWO.Basics.Placeholder;

namespace FWO.Test
{
    [TestFixture]
    internal class NotificationPlaceholderResolverTest
    {
        [Test]
        public void ReplaceNotificationPlaceholders_UsesRequestingOwnerWhenRequesterNameIsMissing()
        {
            FwoOwner application = new()
            {
                Name = "Selected",
                ExtAppId = "APP-1"
            };
            FwoOwner requestingOwner = new()
            {
                Name = "Requester",
                ExtAppId = "APP-2"
            };

            string text = NotificationPlaceholderResolver.ReplaceNotificationPlaceholders(
                REQUESTER,
                new NotificationPlaceholderResolver.NotificationPlaceholderValues
                {
                    Application = application,
                    RequestingOwner = requestingOwner
                });

            Assert.That(text, Is.EqualTo("Requester"));
        }

        [Test]
        public void ReplaceNotificationPlaceholders_RendersHtmlLinkWithoutLinkName()
        {
            FwoOwner application = new()
            {
                Name = "Selected",
                ExtAppId = "APP-1"
            };
            string interfaceUrl = "https://ui.example.test/networkmodelling/APP-1/99";

            string text = NotificationPlaceholderResolver.ReplaceNotificationPlaceholders(
                INTERFACE_LINK,
                new NotificationPlaceholderResolver.NotificationPlaceholderValues
                {
                    Application = application,
                    InterfaceLinkText = "Interface Request",
                    InterfaceLinkUrl = interfaceUrl
                },
                renderHtmlLinks: true);

            Assert.That(text, Is.EqualTo($"<a target=\"_blank\" href=\"{interfaceUrl}\">Interface Request</a>"));
        }
    }
}
