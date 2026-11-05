using System.Net;
using FWO.Basics;
using FWO.Data;
using FWO.Data.Report;
using FWO.Report;
using FWO.Report.Filter;
using FWO.Ui.Display;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Tests the NAT rule changes report: report type classification, query construction, change display and html export.
    /// </summary>
    [TestFixture]
    [Parallelizable]
    internal class NatChangesReportTest
    {
        private const string kPayload = "<img src=x onerror=console.log(1)>\"'&amp;";
        private const string kRawElement = "<img";
        private const string kNatRuleFilter = "nat_rule:{_eq:true}, xlate_rule:{_is_null:false}";
        private const string kAccessRuleFilter = "access_rule:{_eq:true}";
        private static readonly string EncodedPayload = WebUtility.HtmlEncode(kPayload);
        private static readonly string[] TranslatedHeaders = ["<th>Translated Source</th>", "<th>Translated Destination</th>", "<th>Translated Services</th>"];
        private static readonly RuleChange[] NoRuleChanges = [];
        private static readonly List<NotificationDeadline> NoDeadlineOnly = [NotificationDeadline.None];

        private readonly SimulatedUserConfig userConfig = new();

        [Test]
        public void NatChanges_IsChangeReportWithTimeFilter()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ReportType.NatChanges.IsChangeReport(), Is.True);
                Assert.That(ReportType.NatChanges.HasTimeFilter(), Is.True);
                Assert.That(ReportType.NatChanges.IsDeviceRelatedReport(), Is.True);
                Assert.That(ReportType.NatChanges.IsResolvedReport(), Is.False);
                Assert.That(ReportType.NatChanges.SupportsCsvExport(), Is.False);
                Assert.That(ReportType.NatChanges.SupportsHtmlExport(), Is.True);
            });
        }

        [Test]
        public void ReportTypeSelection_ListsNatChangesAfterOtherChangeReports()
        {
            List<ReportType> selection = ReportTypeGroups.ReportTypeSelection();

            Assert.That(selection.IndexOf(ReportType.NatChanges), Is.EqualTo(selection.IndexOf(ReportType.ResolvedChangesTech) + 1));
        }

        [Test]
        public void Compile_NatChanges_SelectsNatRuleChangesWithTranslation()
        {
            DynGraphqlQuery query = Compiler.Compile(CreateTemplate(ReportType.NatChanges, false));

            Assert.Multiple(() =>
            {
                Assert.That(query.FullQuery, Does.Contain(kNatRuleFilter));
                Assert.That(query.FullQuery, Does.Not.Contain(kAccessRuleFilter));
                Assert.That(query.FullQuery, Does.Contain("...natRuleOverviewChangesOld"));
                Assert.That(query.FullQuery, Does.Contain("...natRuleOverviewChangesNew"));
                Assert.That(query.FullQuery, Does.Contain("fragment natRuleOverviewChangesNew on firewall_rule"));
                Assert.That(query.FullQuery, Does.Contain("translate: ruleByXlateRule"));
                Assert.That(query.FullQuery, Does.Contain("$import_id_new: bigint"));
            });
        }

        [Test]
        public void Compile_DetailedNatChanges_UsesDetailedNatFragments()
        {
            DynGraphqlQuery query = Compiler.Compile(CreateTemplate(ReportType.NatChanges, true));

            Assert.Multiple(() =>
            {
                Assert.That(query.FullQuery, Does.Contain("...natRuleDetailsChangesOld"));
                Assert.That(query.FullQuery, Does.Contain("...natRuleDetailsChangesNew"));
                Assert.That(query.FullQuery, Does.Contain("fragment natRuleDetailsChangesOld on firewall_rule"));
            });
        }

        [Test]
        public void Compile_Changes_KeepsSelectingAccessRuleChanges()
        {
            DynGraphqlQuery query = Compiler.Compile(CreateTemplate(ReportType.Changes, false));

            Assert.Multiple(() =>
            {
                Assert.That(query.FullQuery, Does.Contain(kAccessRuleFilter));
                Assert.That(query.FullQuery, Does.Not.Contain(kNatRuleFilter));
                Assert.That(query.FullQuery, Does.Contain("...ruleOverviewChangesNew"));
                Assert.That(query.FullQuery, Does.Not.Contain("natRuleOverviewChanges"));
                Assert.That(query.FullQuery, Does.Contain("changelog_objects: changelog_objects("));
            });
        }

        [Test]
        public void Compile_ReportTypeFilterNatChanges_IsAccepted()
        {
            ReportTemplate template = CreateTemplate(ReportType.NatChanges, false);
            template.Filter = "type=natchanges";

            Assert.DoesNotThrow(() => Compiler.Compile(template));
        }

        [Test]
        public void ConstructReport_NatChanges_CreatesChangesReport()
        {
            ReportBase report = ReportBase.ConstructReport(CreateTemplate(ReportType.NatChanges, false), userConfig);

            Assert.That(report, Is.TypeOf<ReportChanges>());
            Assert.That(report.ReportType, Is.EqualTo(ReportType.NatChanges));
        }

        [Test]
        public void DisplayTranslated_AddedRule_ShowsNewTranslationAsAdded()
        {
            RuleChangeDisplayHtml display = new(userConfig);
            RuleChange ruleChange = new() { ChangeAction = 'I', NewRule = CreateNatRule("newSrc", "newDst", "newSvc") };

            Assert.Multiple(() =>
            {
                Assert.That(display.DisplayTranslatedSource(ruleChange, OutputLocation.export, ReportType.NatChanges), Does.Contain("newSrc").And.Contain(GlobalConst.kStyleAdded));
                Assert.That(display.DisplayTranslatedDestination(ruleChange, OutputLocation.export, ReportType.NatChanges), Does.Contain("newDst").And.Contain(GlobalConst.kStyleAdded));
                Assert.That(display.DisplayTranslatedServices(ruleChange, OutputLocation.export, ReportType.NatChanges), Does.Contain("newSvc").And.Contain(GlobalConst.kStyleAdded));
            });
        }

        [Test]
        public void DisplayTranslated_DeletedRule_ShowsOldTranslationAsDeleted()
        {
            RuleChangeDisplayHtml display = new(userConfig);
            RuleChange ruleChange = new() { ChangeAction = 'D', OldRule = CreateNatRule("oldSrc", "oldDst", "oldSvc") };

            Assert.Multiple(() =>
            {
                Assert.That(display.DisplayTranslatedSource(ruleChange, OutputLocation.export, ReportType.NatChanges), Does.Contain("oldSrc").And.Contain(GlobalConst.kStyleDeleted));
                Assert.That(display.DisplayTranslatedDestination(ruleChange, OutputLocation.export, ReportType.NatChanges), Does.Contain("oldDst").And.Contain(GlobalConst.kStyleDeleted));
                Assert.That(display.DisplayTranslatedServices(ruleChange, OutputLocation.export, ReportType.NatChanges), Does.Contain("oldSvc").And.Contain(GlobalConst.kStyleDeleted));
            });
        }

        [Test]
        public void DisplayTranslated_ModifiedRule_ShowsDifference()
        {
            RuleChangeDisplayHtml display = new(userConfig);
            RuleChange ruleChange = new()
            {
                ChangeAction = 'C',
                OldRule = CreateNatRule("oldSrc", "sameDst", "sameSvc"),
                NewRule = CreateNatRule("newSrc", "sameDst", "sameSvc")
            };

            string source = display.DisplayTranslatedSource(ruleChange, OutputLocation.export, ReportType.NatChanges);
            string destination = display.DisplayTranslatedDestination(ruleChange, OutputLocation.export, ReportType.NatChanges);
            string services = display.DisplayTranslatedServices(ruleChange, OutputLocation.export, ReportType.NatChanges);

            Assert.Multiple(() =>
            {
                Assert.That(source, Does.Contain("deleted: ").And.Contain("oldSrc"));
                Assert.That(source, Does.Contain("added: ").And.Contain("newSrc"));
                Assert.That(destination, Does.Contain("sameDst").And.Not.Contain("deleted: ").And.Not.Contain("added: "));
                Assert.That(services, Does.Contain("sameSvc").And.Not.Contain("deleted: ").And.Not.Contain("added: "));
            });
        }

        [Test]
        public void DisplayTranslated_ModifiedNegation_ShowsDifference()
        {
            RuleChangeDisplayHtml display = new(userConfig);
            Rule newRule = CreateNatRule("src", "dst", "svc");
            newRule.NatData.TranslatedSourceNegated = true;
            RuleChange ruleChange = new() { ChangeAction = 'C', OldRule = CreateNatRule("src", "dst", "svc"), NewRule = newRule };

            string source = display.DisplayTranslatedSource(ruleChange, OutputLocation.export, ReportType.NatChanges);

            Assert.That(source, Does.Contain("deleted: ").And.Contain("added: ").And.Contain("not"));
        }

        [TestCase('I')]
        [TestCase('D')]
        [TestCase('C')]
        public void DisplayTranslated_EncodesObjectAndServiceNames(char changeAction)
        {
            RuleChangeDisplayHtml display = new(userConfig);
            RuleChange ruleChange = new()
            {
                ChangeAction = changeAction,
                OldRule = changeAction == 'C' ? CreateNatRule("oldSrc", "oldDst", "oldSvc") : CreateNatRule(kPayload, kPayload, kPayload),
                NewRule = CreateNatRule(kPayload, kPayload, kPayload)
            };

            AssertEncoded(display.DisplayTranslatedSource(ruleChange, OutputLocation.export, ReportType.NatChanges));
            AssertEncoded(display.DisplayTranslatedDestination(ruleChange, OutputLocation.export, ReportType.NatChanges));
            AssertEncoded(display.DisplayTranslatedServices(ruleChange, OutputLocation.export, ReportType.NatChanges));
        }

        [Test]
        public void ExportToHtml_NatChanges_ContainsTranslatedColumns()
        {
            RuleChange[] ruleChanges = [new RuleChange { ChangeAction = 'I', ChangeImport = new ChangeImport(), NewRule = CreateNatRule("transSrc", "transDst", "transSvc") }];
            ReportChanges report = CreateReport(ReportType.NatChanges, ruleChanges);

            string html = report.ExportToHtml();

            Assert.Multiple(() =>
            {
                foreach (string header in TranslatedHeaders)
                {
                    Assert.That(html, Does.Contain(header));
                }
                Assert.That(html, Does.Contain("transSrc"));
                Assert.That(html, Does.Contain("transDst"));
                Assert.That(html, Does.Contain("transSvc"));
            });
        }

        [Test]
        public void ExportToHtml_NatChangesWithoutChanges_SpansAllColumns()
        {
            ReportChanges report = CreateReport(ReportType.NatChanges, NoRuleChanges);

            string html = report.ExportToHtml();

            Assert.That(html, Does.Contain("<td colspan=\"17\">"));
        }

        [Test]
        public void ExportToHtml_Changes_HasNoTranslatedColumns()
        {
            ReportChanges report = CreateReport(ReportType.Changes, NoRuleChanges);

            string html = report.ExportToHtml();

            Assert.Multiple(() =>
            {
                foreach (string header in TranslatedHeaders)
                {
                    Assert.That(html, Does.Not.Contain(header));
                }
                Assert.That(html, Does.Contain("<td colspan=\"14\">"));
            });
        }

        [Test]
        public void ImportNatChangeNotifications_OfferNoDeadline()
        {
            Assert.That(FwoNotification.OfferedDeadlineOptions(NotificationClient.ImportNatChange), Is.EqualTo(NoDeadlineOnly));
        }

        private static void AssertEncoded(string html)
        {
            Assert.Multiple(() =>
            {
                Assert.That(html, Does.Not.Contain(kRawElement));
                Assert.That(html, Does.Contain(EncodedPayload));
            });
        }

        private static ReportTemplate CreateTemplate(ReportType reportType, bool detailed)
        {
            ReportTemplate template = new() { Filter = "", Detailed = detailed };
            template.ReportParams.ReportType = (int)reportType;
            return template;
        }

        private ReportChanges CreateReport(ReportType reportType, RuleChange[] ruleChanges)
        {
            ReportData reportData = new();
            reportData.ManagementData.Add(new ManagementReport { Id = 1, Name = "TestMgt", RuleChanges = ruleChanges });
            return new ReportChanges(new DynGraphqlQuery("TestFilter"), userConfig, reportType, new TimeFilter(), false)
            {
                ReportData = reportData
            };
        }

        private static Rule CreateNatRule(string translatedSourceName, string translatedDestinationName, string translatedServiceName)
        {
            return new Rule
            {
                Id = 1,
                MgmtId = 1,
                Name = "natRule",
                NatRule = true,
                XlateRule = 2,
                NatData = new NatData
                {
                    TranslatedFroms = [new NetworkLocation(new NetworkUser(), CreateHost(1, translatedSourceName, "10.0.0.1/32"))],
                    TranslatedTos = [new NetworkLocation(new NetworkUser(), CreateHost(2, translatedDestinationName, "10.0.0.2/32"))],
                    TranslatedServices = [new ServiceWrapper { Content = new NetworkService { Id = 1, Name = translatedServiceName, Type = new NetworkServiceType { Name = "simple" } } }]
                }
            };
        }

        private static NetworkObject CreateHost(long id, string name, string ip)
        {
            return new NetworkObject { Id = id, Name = name, IP = ip, IpEnd = ip, Type = new NetworkObjectType { Name = ObjectType.Host } };
        }
    }
}
