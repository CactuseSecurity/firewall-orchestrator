using FWO.Data;
using FWO.Services;
using NUnit.Framework;
using System.Net;
using System.Reflection;
using static FWO.Basics.Placeholder;

namespace FWO.Test
{
    [TestFixture]
    internal class NotificationPlaceholderResolverTest
    {
        private const string kUiHostName = "https://fwo.example.test";
        private const string kUiLink = "https://fwo.example.test/networkmodelling/APP-1/99";
        private const string kForeignLink = "https://example.invalid/login";
        private static readonly List<string> kMarkupPayloads =
        [
            "<a href=\"https://example.invalid\">Login</a>",
            "x\" onmouseover=\"alert(1)",
            "it's <b>bold</b> & 'quoted'"
        ];
        private static readonly List<string> kTextPlaceholders =
        [
            APPNAME, APPID, REQUESTING_APPNAME, REQUESTING_APPID, REQUESTER, REQUESTDATE, INTERFACE_NAME, NEW_INTERFACE_NAME, REASON, USER_NAME
        ];
        private static readonly List<string> kOwnerPlaceholders = [APPNAME, APPID, TIME_INTERVAL];

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
                isHtmlBody: true);

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
                isHtmlBody: true);

            Assert.That(text, Is.EqualTo("<a target=\"_blank\" href=\"https://example.test/interface?id=&quot;&amp;mode=full\">Interface: x&quot;&gt;&lt;script&gt;alert(1)&lt;/script&gt;</a>"));
        }
        [TestCaseSource(nameof(kMarkupPayloads))]
        public void ReplaceNotificationPlaceholders_EncodesEveryValueInHtmlBodies(string payload)
        {
            string template = string.Join("|", kTextPlaceholders);

            string html = NotificationPlaceholderResolver.ReplaceNotificationPlaceholders(template, CreateValues(payload), isHtmlBody: true);
            string plain = NotificationPlaceholderResolver.ReplaceNotificationPlaceholders(template, CreateValues(payload));

            Assert.That(html.Split('|'), Is.All.EqualTo(WebUtility.HtmlEncode(payload)));
            Assert.That(plain.Split('|'), Is.All.EqualTo(payload));
        }

        [TestCaseSource(nameof(kMarkupPayloads))]
        public void ReplaceOwnerPlaceholders_EncodesEveryValueInHtmlBodies(string payload)
        {
            string template = string.Join("|", kOwnerPlaceholders);
            FwoOwner owner = new() { Name = payload, ExtAppId = payload };

            string html = NotificationPlaceholderResolver.ReplaceOwnerPlaceholders(template, owner, payload, isHtmlBody: true);
            string plain = NotificationPlaceholderResolver.ReplaceOwnerPlaceholders(template, owner, payload);

            Assert.That(html.Split('|'), Is.All.EqualTo(WebUtility.HtmlEncode(payload)));
            Assert.That(plain.Split('|'), Is.All.EqualTo(payload));
        }

        [Test]
        public void RestrictCallerPlaceholderData_KeepsOnlyWhatTheCallerMayDecide()
        {
            NotificationPlaceholderData callerData = new()
            {
                InterfaceName = "Interface 1",
                InterfaceLinkUrl = kForeignLink,
                NewInterfaceLinkUrl = kUiLink,
                Content = "rejected because",
                Reason = "forged reason",
                RequesterName = "forged requester",
                UserName = "forged user",
                RequestDate = "01.02.2026"
            };

            NotificationPlaceholderData? restricted = NotificationPlaceholderResolver.RestrictCallerPlaceholderData(callerData, "caller", kUiHostName, out string? error);

            Assert.That(error, Is.Null);
            Assert.That(restricted, Is.Not.Null);
            Assert.Multiple(() =>
            {
                Assert.That(restricted!.InterfaceName, Is.EqualTo("Interface 1"));
                Assert.That(restricted.InterfaceLinkUrl, Is.Empty);
                Assert.That(restricted.NewInterfaceLinkUrl, Is.EqualTo(kUiLink));
                Assert.That(restricted.Content, Is.EqualTo("rejected because"));
                Assert.That(restricted.Reason, Is.Empty);
                Assert.That(restricted.RequesterName, Is.Empty);
                Assert.That(restricted.UserName, Is.EqualTo("caller"));
                Assert.That(restricted.RequestDate, Is.EqualTo("01.02.2026"));
            });
        }

        [Test]
        public void RestrictCallerPlaceholderData_RejectsOversizedValuesAndAcceptsNone()
        {
            NotificationPlaceholderData longName = new() { InterfaceName = new string('n', NotificationPlaceholderResolver.kMaxCallerTextLength + 1) };
            NotificationPlaceholderData longContent = new() { Content = new string('c', NotificationPlaceholderResolver.kMaxCallerContentLength + 1) };
            NotificationPlaceholderData maximum = new()
            {
                InterfaceName = new string('n', NotificationPlaceholderResolver.kMaxCallerTextLength),
                Content = new string('c', NotificationPlaceholderResolver.kMaxCallerContentLength)
            };

            Assert.Multiple(() =>
            {
                Assert.That(NotificationPlaceholderResolver.RestrictCallerPlaceholderData(longName, "caller", kUiHostName, out string? nameError), Is.Null);
                Assert.That(nameError, Does.Contain(NotificationPlaceholderResolver.kMaxCallerTextLength.ToString()));
                Assert.That(NotificationPlaceholderResolver.RestrictCallerPlaceholderData(longContent, "caller", kUiHostName, out string? contentError), Is.Null);
                Assert.That(contentError, Does.Contain(NotificationPlaceholderResolver.kMaxCallerContentLength.ToString()));
                Assert.That(NotificationPlaceholderResolver.RestrictCallerPlaceholderData(maximum, "caller", kUiHostName, out string? maximumError), Is.Not.Null);
                Assert.That(maximumError, Is.Null);
                Assert.That(NotificationPlaceholderResolver.RestrictCallerPlaceholderData(null, "caller", kUiHostName, out string? noneError), Is.Null);
                Assert.That(noneError, Is.Null);
            });
        }

        [TestCase(kUiLink, kUiHostName, true)]
        [TestCase("https://FWO.example.test/x", kUiHostName, true)]
        [TestCase(kForeignLink, kUiHostName, false)]
        [TestCase("https://fwo.example.test.example.invalid/x", kUiHostName, false)]
        [TestCase("https://fwo.example.test@example.invalid/x", kUiHostName, false)]
        [TestCase("https://fwo.example.test:8443/x", kUiHostName, false)]
        [TestCase("javascript:alert(1)", kUiHostName, false)]
        [TestCase("/networkmodelling/APP-1/99", kUiHostName, false)]
        [TestCase("https://fwo.example.test/fwo/networkmodelling", "https://fwo.example.test/fwo/", true)]
        [TestCase("https://fwo.example.test/fwox/networkmodelling", "https://fwo.example.test/fwo", false)]
        [TestCase("", kUiHostName, false)]
        [TestCase(kUiLink, "", false)]
        public void IsUiLink_AcceptsOnlyLinksIntoTheConfiguredUi(string url, string uiHostName, bool expected)
        {
            Assert.That(NotificationPlaceholderResolver.IsUiLink(url, uiHostName), Is.EqualTo(expected));
        }

        [TestCase(NotificationLayout.HtmlInBody, true)]
        [TestCase(NotificationLayout.SimpleText, false)]
        public void BuildWorkflowActionBody_EncodesCallerContentForHtmlBodies(NotificationLayout layout, bool encoded)
        {
            const string kContent = "<a href=\"https://example.invalid\">Login</a>";
            FwoNotification notification = new() { Layout = layout, EmailBody = "Reason: " + CONTENT };
            MethodInfo buildBody = typeof(EmailHelper).GetMethod("BuildWorkflowActionBody", BindingFlags.NonPublic | BindingFlags.Static)
                ?? throw new MissingMethodException(nameof(EmailHelper), "BuildWorkflowActionBody");

            object?[] arguments = [notification, null, new NotificationPlaceholderData { Content = kContent }];

            string body = (string)buildBody.Invoke(null, arguments)!;

            Assert.That(body, Is.EqualTo("Reason: " + (encoded ? WebUtility.HtmlEncode(kContent) : kContent)));
        }

        private static NotificationPlaceholderResolver.NotificationPlaceholderValues CreateValues(string payload)
        {
            return new NotificationPlaceholderResolver.NotificationPlaceholderValues
            {
                Application = new FwoOwner { Name = payload, ExtAppId = payload },
                RequestingOwner = new FwoOwner { Name = payload, ExtAppId = payload },
                RequesterName = payload,
                RequestDate = payload,
                InterfaceName = payload,
                NewInterfaceName = payload,
                Reason = payload,
                UserName = payload
            };
        }
    }
}
