using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report.Filter;
using FWO.Ui.Display;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Covers the last hit display of rule reports: the hit of the gateway a rule is listed below, the texts for a
    /// missing hit, the fallback to the latest hit across all gateways, and the data the display reads.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class RuleDisplayHtmlGatewayLastHitTest
    {
        private const int kHitGatewayId = 51;
        private const int kOtherGatewayId = 52;
        private const string kNoHitsKey = "no_rule_hits";
        private const string kUnsupportedKey = "rule_hits_unsupported";
        private const string kMarkupPayload = "<img src=x onerror=\"alert('x')\">";
        private const string kRuleMetadataJson =
            "{\"rule_metadata_id\":1,\"rule_last_hit\":\"2026-11-01T10:00:00\",\"rule_hits\":["
            + "{\"dev_id\":51,\"rule_last_hit\":\"2026-11-01T11:00:00+01:00\",\"device\":{\"dev_name\":\"gw-a\"}},"
            + "{\"dev_id\":52,\"rule_last_hit\":\"2025-10-01T08:30:00+00:00\"}]}";

        private const string kManagementJson = "{\"id\":5,\"deviceType\":{\"id\":9,\"supportsRuleHits\":true}}";
        private const string kDeviceJson = "{\"id\":51,\"importedRuleHits\":[{\"dev_id\":51}]}";
        private const int kManagementId = 5;
        private const int kSuperManagementId = 4;
        private const string kImportedRuleHitsSelection = "importedRuleHits: rule_hits(limit: 1)";
        private const string kDeviceTypeSelection = "supportsRuleHits: dev_typ_supports_rule_hits";

        private static readonly DateTime kAggregatedLastHit = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);
        private static readonly DateTime kGatewayLastHit = new(2025, 10, 1, 8, 30, 0, DateTimeKind.Utc);

        [Test]
        public void WithoutGatewayTheLatestHitAcrossAllGatewaysIsShown()
        {
            RuleDisplayHtml display = new(new SimulatedUserConfig());

            Assert.Multiple(() =>
            {
                Assert.That(display.DisplayLastHit(CreateMetadata(), null), Is.EqualTo("2026-11-01"));
                Assert.That(display.DisplayLastHit(new RuleMetadata(), null), Is.Empty);
            });
        }

        [Test]
        public void BelowAGatewayItsOwnHitIsShown()
        {
            RuleDisplayHtml display = new(new SimulatedUserConfig());

            string html = display.DisplayLastHit(CreateMetadata(), new GatewayHitContext(kHitGatewayId, true));

            Assert.That(html, Is.EqualTo("2025-10-01"));
        }

        [Test]
        public void AMissingHitIsToldApartFromAManagementWithoutHits()
        {
            RuleDisplayHtml display = new(new SimulatedUserConfig());
            RuleMetadata metadata = CreateMetadata();

            Assert.Multiple(() =>
            {
                Assert.That(display.DisplayLastHit(metadata, new GatewayHitContext(kOtherGatewayId, true)), Is.EqualTo(kNoHitsKey));
                Assert.That(display.DisplayLastHit(metadata, new GatewayHitContext(kOtherGatewayId, false)), Is.EqualTo(kUnsupportedKey));
                Assert.That(display.DisplayLastHit(metadata, new GatewayHitContext(kHitGatewayId, false)), Is.EqualTo(kUnsupportedKey),
                    "a management that does not report hits has no trustworthy hit per gateway");
            });
        }

        [TestCase(true)]
        [TestCase(false)]
        public void TextsForAMissingHitAreEncoded(bool supportsRuleHits)
        {
            PayloadUserConfig userConfig = new();
            RuleDisplayHtml display = new(userConfig);

            string html = display.DisplayLastHit(new RuleMetadata(), new GatewayHitContext(kOtherGatewayId, supportsRuleHits));

            Assert.Multiple(() =>
            {
                Assert.That(html, Does.Not.Contain("<"));
                Assert.That(html, Does.Not.Contain("\""));
                Assert.That(html, Does.Not.Contain("'"));
                Assert.That(html, Does.Contain("&lt;img"));
            });
        }

        [Test]
        public void GatewayContextIsOnlyUsedWhereRulesAreListedPerGatewayWithKnownSupport()
        {
            DeviceReport device = CreateGatewayWithImportedHits(kHitGatewayId);
            ManagementReport supporting = CreateSupportingManagement();
            ManagementReport notSupporting = new() { Id = kManagementId, DeviceType = new DeviceType() };
            ManagementReport withoutDeviceType = new() { Id = kManagementId };
            Rule rule = new() { MgmtId = kManagementId };

            Assert.Multiple(() =>
            {
                Assert.That(GatewayHitContext.For(supporting, device, rule), Is.EqualTo(new GatewayHitContext(kHitGatewayId, true)));
                Assert.That(GatewayHitContext.For(notSupporting, device, rule), Is.EqualTo(new GatewayHitContext(kHitGatewayId, false)));
                Assert.That(GatewayHitContext.For(supporting, device, rule, true), Is.Null);
                Assert.That(GatewayHitContext.For(withoutDeviceType, device, rule), Is.Null);
            });
        }

        [Test]
        public void AGatewayWithoutAnyImportedHitShowsTheLatestHitAcrossAllGatewaysInsteadOfNoHits()
        {
            RuleDisplayHtml display = new(new SimulatedUserConfig());
            ManagementReport supporting = CreateSupportingManagement();
            ManagementReport notSupporting = new() { Id = kManagementId, DeviceType = new DeviceType() };
            DeviceReport gatewayWithoutHits = new() { Id = kOtherGatewayId };
            Rule rule = new() { MgmtId = kManagementId, Metadata = CreateMetadata() };

            GatewayHitContext? context = GatewayHitContext.For(supporting, gatewayWithoutHits, rule);

            Assert.Multiple(() =>
            {
                Assert.That(context, Is.Null);
                Assert.That(display.DisplayLastHit(rule.Metadata, context), Is.EqualTo("2026-11-01"));
                Assert.That(display.DisplayLastHit(rule.Metadata, context), Is.Not.EqualTo(kNoHitsKey));
                Assert.That(GatewayHitContext.For(notSupporting, gatewayWithoutHits, rule), Is.EqualTo(new GatewayHitContext(kOtherGatewayId, false)),
                    "a management that does not report hits stays unsupported");
            });
        }

        [Test]
        public void ARuleOfAnotherManagementIsOnlyShownPerGatewayWhenItHasAHitThere()
        {
            RuleDisplayHtml display = new(new SimulatedUserConfig());
            ManagementReport supporting = CreateSupportingManagement();
            Rule globalRule = new() { MgmtId = kSuperManagementId, Metadata = CreateMetadata() };
            Rule ownRule = new() { MgmtId = kManagementId, Metadata = CreateMetadata() };
            Rule ruleWithoutManagementId = new() { Metadata = CreateMetadata() };
            DeviceReport hitGateway = CreateGatewayWithImportedHits(kHitGatewayId);
            DeviceReport otherGateway = CreateGatewayWithImportedHits(kOtherGatewayId);

            GatewayHitContext? unknownHit = GatewayHitContext.For(supporting, otherGateway, globalRule);

            Assert.Multiple(() =>
            {
                Assert.That(unknownHit, Is.Null);
                Assert.That(display.DisplayLastHit(globalRule.Metadata, unknownHit), Is.EqualTo("2026-11-01"));
                Assert.That(GatewayHitContext.For(supporting, hitGateway, globalRule), Is.EqualTo(new GatewayHitContext(kHitGatewayId, true)));
                Assert.That(display.DisplayLastHit(ownRule.Metadata, GatewayHitContext.For(supporting, otherGateway, ownRule)), Is.EqualTo(kNoHitsKey));
                Assert.That(display.DisplayLastHit(ruleWithoutManagementId.Metadata, GatewayHitContext.For(supporting, otherGateway, ruleWithoutManagementId)),
                    Is.EqualTo(kNoHitsKey));
            });
        }

        [Test]
        public void ImportedRuleHitsOfAGatewayAreReadFromTheApiButNotExported()
        {
            DeviceReport newtonsoft = Newtonsoft.Json.JsonConvert.DeserializeObject<DeviceReport>(kDeviceJson)!;
            DeviceReport systemText = System.Text.Json.JsonSerializer.Deserialize<DeviceReport>(kDeviceJson)!;

            Assert.Multiple(() =>
            {
                Assert.That(newtonsoft.HasImportedRuleHits(), Is.True);
                Assert.That(systemText.HasImportedRuleHits(), Is.True);
                Assert.That(new DeviceReport().HasImportedRuleHits(), Is.False);
                Assert.That(Newtonsoft.Json.JsonConvert.SerializeObject(newtonsoft), Does.Not.Contain("importedRuleHits"));
                Assert.That(System.Text.Json.JsonSerializer.Serialize(systemText), Does.Not.Contain("importedRuleHits"));
            });
        }

        [Test]
        public void RuleReportQueriesSelectWhatTheGatewayContextReads()
        {
            string legacyRulesQuery = CompileReportQuery(ReportType.ResolvedRules).FullQuery;
            string standardStructureQuery = CompileReportQuery(ReportType.Rules).StandardRulesStructureQuery;
            string natStructureQuery = CompileReportQuery(ReportType.NatRules).StandardRulesStructureQuery;

            Assert.Multiple(() =>
            {
                Assert.That(RuleQueries.ruleOverviewFragments, Does.Contain("mgm_id"));
                Assert.That(RuleQueries.ruleDetailsForReportFragments, Does.Contain("mgm_id"));
                Assert.That(legacyRulesQuery, Does.Contain(kImportedRuleHitsSelection), "legacy rules query");
                Assert.That(legacyRulesQuery, Does.Contain(kDeviceTypeSelection), "legacy rules query");
                Assert.That(standardStructureQuery, Does.Contain(kImportedRuleHitsSelection), "standard rules structure query");
                Assert.That(standardStructureQuery, Does.Contain(kDeviceTypeSelection), "standard rules structure query");
                Assert.That(natStructureQuery, Does.Contain(kImportedRuleHitsSelection), "NAT rules structure query");
                Assert.That(natStructureQuery, Does.Contain(kDeviceTypeSelection), "NAT rules structure query");
            });
        }

        [Test]
        public void RuleHitsAreReadByBothSerializersAndNormalizedToUtc()
        {
            RuleMetadata newtonsoft = Newtonsoft.Json.JsonConvert.DeserializeObject<RuleMetadata>(kRuleMetadataJson)!;
            RuleMetadata systemText = System.Text.Json.JsonSerializer.Deserialize<RuleMetadata>(kRuleMetadataJson)!;

            Assert.Multiple(() =>
            {
                Assert.That(newtonsoft.RuleHits, Has.Count.EqualTo(2));
                Assert.That(systemText.RuleHits, Has.Count.EqualTo(2));
                Assert.That(newtonsoft.GetGatewayLastHit(kHitGatewayId), Is.EqualTo(kAggregatedLastHit));
                Assert.That(systemText.GetGatewayLastHit(kHitGatewayId), Is.EqualTo(kAggregatedLastHit));
                Assert.That(systemText.GetGatewayLastHit(kHitGatewayId)?.Kind, Is.EqualTo(DateTimeKind.Utc));
                Assert.That(systemText.RuleHits[0].Gateway?.Name, Is.EqualTo("gw-a"));
                Assert.That(systemText.RuleHits[1].Gateway, Is.Null);
                Assert.That(systemText.GetGatewayLastHit(kOtherGatewayId), Is.EqualTo(kGatewayLastHit));
                Assert.That(new RuleMetadata().GetGatewayLastHit(kHitGatewayId), Is.Null);
            });
        }

        [Test]
        public void ManagementDeviceTypeIsReadFromTheApiButNotExported()
        {
            ManagementReport newtonsoft = Newtonsoft.Json.JsonConvert.DeserializeObject<ManagementReport>(kManagementJson)!;
            ManagementReport systemText = System.Text.Json.JsonSerializer.Deserialize<ManagementReport>(kManagementJson)!;

            Assert.Multiple(() =>
            {
                Assert.That(newtonsoft.DeviceType?.SupportsRuleHits, Is.True);
                Assert.That(systemText.DeviceType?.SupportsRuleHits, Is.True);
                Assert.That(Newtonsoft.Json.JsonConvert.SerializeObject(newtonsoft), Does.Not.Contain("deviceType"));
                Assert.That(System.Text.Json.JsonSerializer.Serialize(systemText), Does.Not.Contain("deviceType"));
            });
        }

        [Test]
        public void DeviceTypeCopyKeepsTheRuleHitSupport()
        {
            DeviceType copy = new(new DeviceType { Id = 9, SupportsRuleHits = true });

            Assert.That(copy.SupportsRuleHits, Is.True);
        }

        [Test]
        public void RuleReportFragmentsSelectTheHitsPerGateway()
        {
            Assert.Multiple(() =>
            {
                Assert.That(RuleQueries.ruleOverviewFragments, Does.Contain("rule_hits"));
                Assert.That(RuleQueries.ruleDetailsForReportFragments, Does.Contain("rule_hits"));
                Assert.That(DeviceQueries.getDeviceTypeDetails, Does.Contain("supportsRuleHits: dev_typ_supports_rule_hits"));
            });
        }

        private static DynGraphqlQuery CompileReportQuery(ReportType reportType)
        {
            ReportTemplate template = new();
            template.ReportParams.ReportType = (int)reportType;
            return Compiler.Compile(template);
        }

        private static ManagementReport CreateSupportingManagement()
        {
            return new ManagementReport { Id = kManagementId, DeviceType = new DeviceType { SupportsRuleHits = true } };
        }

        private static DeviceReport CreateGatewayWithImportedHits(int deviceId)
        {
            DeviceReport device = new() { Id = deviceId };
            device.ImportedRuleHits.Add(new RuleHit { DeviceId = deviceId });
            return device;
        }

        private static RuleMetadata CreateMetadata()
        {
            RuleMetadata metadata = new() { LastHit = kAggregatedLastHit };
            metadata.RuleHits.Add(new RuleHit { DeviceId = kHitGatewayId, LastHit = kGatewayLastHit });
            return metadata;
        }

        /// <summary>
        /// Returns markup for every text, as a manipulated text table would.
        /// </summary>
        private sealed class PayloadUserConfig : SimulatedUserConfig
        {
            public override string GetText(string key)
            {
                return kMarkupPayload;
            }
        }
    }
}
