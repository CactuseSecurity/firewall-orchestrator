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

        [Test]
        public void ReplaceNotificationPlaceholders_EncodesHtmlLinkAttributesAndText()
        {
            string text = NotificationPlaceholderResolver.ReplaceNotificationPlaceholders(
                INTERFACE_LINK,
                new NotificationPlaceholderResolver.NotificationPlaceholderValues
                {
                    InterfaceLinkText = "Interface",
                    InterfaceLinkName = "x\"><script>alert(1)</script>",
                    InterfaceLinkUrl = "https://example.test/interface?id=\"&mode=full"
                },
                renderHtmlLinks: true);

            Assert.That(text, Is.EqualTo("<a target=\"_blank\" href=\"https://example.test/interface?id=&quot;&amp;mode=full\">Interface: x&quot;&gt;&lt;script&gt;alert(1)&lt;/script&gt;</a>"));
        }
    }
}
