using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Middleware.Server;
using FWO.Services;
using NUnit.Framework;

namespace FWO.Test
{
    [TestFixture]
    public class RuleNotificationBodyBaseTest
    {
        [Test]
        public void BuildRuleTextBodyUsesIntroTextAndAppendsTableHeaders()
        {
            SimulatedGlobalConfig globalConfig = CreateGlobalConfig();
            RuleNotificationBodyHarness harness = new(globalConfig);
            FwoOwner owner = new() { Name = "Owner A", ExtAppId = "APP-1" };

            string body = harness.BuildTextBody(
                owner,
                $"Rules for {Placeholder.APPNAME}/{Placeholder.APPID} during {Placeholder.TIME_INTERVAL}{Placeholder.RULE_TABLE}",
                "2026-07",
                [],
                ["Extra"]);

            Assert.That(body, Does.Contain("Rules for Owner A/APP-1 during 2026-07"));
            Assert.That(body, Does.Contain("Uid | Name | Source | Destination | Service | Change-ID | Last Hit | Extra"));
        }

        [Test]
        public void BuildRuleHtmlBodyWrapsFrameAndKeepsReportBody()
        {
            SimulatedGlobalConfig globalConfig = CreateGlobalConfig();
            RuleNotificationBodyHarness harness = new(globalConfig);
            FwoOwner owner = new() { Name = "Owner A", ExtAppId = "APP-1" };

            string body = harness.BuildHtmlBody(
                owner,
                $"Rules for {Placeholder.APPNAME}/{Placeholder.APPID} during {Placeholder.TIME_INTERVAL}{Placeholder.RULE_TABLE}",
                "2026-07",
                [],
                ["Extra"],
                "Frame Title");

            Assert.That(body, Does.Contain("Rules for Owner A/APP-1 during 2026-07"));
            Assert.That(body, Does.Contain(NotificationTableBodyBuilder.HtmlTableStyleBlock));
            Assert.That(body, Does.Contain("<h2>Frame Title</h2>"));
            Assert.That(body, Does.Contain("<p>Owners: Owner A</p>"));
            Assert.That(body, Does.Contain("Uid"));
            Assert.That(body, Does.Contain("Extra"));
        }

        private static SimulatedGlobalConfig CreateGlobalConfig()
        {
            SimulatedGlobalConfig globalConfig = new();
            globalConfig.DummyTranslate["uid"] = "Uid";
            globalConfig.DummyTranslate["name"] = "Name";
            globalConfig.DummyTranslate["source"] = "Source";
            globalConfig.DummyTranslate["destination"] = "Destination";
            globalConfig.DummyTranslate["service"] = "Service";
            globalConfig.DummyTranslate["change_id"] = "Change-ID";
            globalConfig.DummyTranslate["last_hit"] = "Last Hit";
            globalConfig.DummyTranslate["generated_on"] = "Generated on";
            globalConfig.DummyTranslate["owners"] = "Owners";
            globalConfig.DummyTranslate["tableofcontent"] = "Table of content";
            return globalConfig;
        }

        private sealed class RuleNotificationBodyHarness(GlobalConfig globalConfig) : RuleNotificationBodyBase(globalConfig)
        {
            public string BuildTextBody(FwoOwner owner, string bodyTemplate, string timeIntervalText, IEnumerable<Rule> rules, IEnumerable<string>? extraHeaders)
            {
                return BuildRuleTextBody(owner, bodyTemplate, timeIntervalText, rules, extraHeaders);
            }

            public string BuildHtmlBody(FwoOwner owner, string bodyTemplate, string timeIntervalText, IEnumerable<Rule> rules, IEnumerable<string>? extraHeaders, string? frameTitle)
            {
                return BuildRuleHtmlBody(owner, bodyTemplate, timeIntervalText, rules, extraHeaders, frameTitle: frameTitle);
            }
        }
    }
}
