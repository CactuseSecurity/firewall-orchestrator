using FWO.Basics;
using FWO.Data;
using FWO.Data.Labelling;
using FWO.Data.Report;
using FWO.Report;
using FWO.Report.Filter;
using FWO.Test.Mocks;
using FWO.Ui.Display;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FWO.Test
{
    /// <summary>
    /// Display of rule labels in rule-based reports: source / destination labels as ORed AND-groups and the labels
    /// of the rule as a whole, depending on the global label display settings.
    /// </summary>
    [TestFixture]
    internal class RuleLabelExportTest
    {
        private const string kPayload = "<script>alert(1)</script>\" onmouseover=\"x' ";
        private static readonly List<Dictionary<string, List<string>>> kTwoGroups =
        [
            new() { ["AppRole"] = ["AR1"], ["Stage"] = ["Prod"] },
            new() { ["AppRole"] = ["AR2"] }
        ];
        private static readonly List<Dictionary<string, List<string>>> kOneGroup = [new() { ["Stage"] = ["Prod", "Test"] }];
        private static readonly List<Dictionary<string, List<string>>> kPayloadGroup = [new() { [kPayload] = [kPayload] }];
        private static readonly List<Dictionary<string, List<string>>> kQuoteGroup = [new() { ["Stage"] = ["a\"b"] }];
        private static readonly List<string> kTwoGroupsObjectNames = ["AppRole: AR1 & Stage: Prod", "AppRole: AR2"];
        private static readonly List<string> kOneGroupObjectNames = ["Stage: Prod & Stage: Test"];
        private const string kAndLabelObjectHtml = "AppRole: AR1 &amp; Stage: Prod";
        private const string kLabelMember = "label-member-host";
        private const string kOtherGroup = "Other: X & Y";
        private const string kPlainHost = "plain-host";
        private static readonly List<RuleLabelAssignment> kAppLabels = [AppLabel("APP-1"), AppLabel("APP-2"), AppLabel("APP-1")];

        [Test]
        public void FormatSideLabels_JoinsLabelsAndGroups()
        {
            Assert.Multiple(() =>
            {
                Assert.That(RuleLabelFormatter.FormatSideLabels(kTwoGroups, RuleLabelFormatter.kHtmlAnd, RuleLabelFormatter.kHtmlOr),
                    Is.EqualTo("(AppRole: AR1 ∩ Stage: Prod) ∪ (AppRole: AR2)"));
                Assert.That(RuleLabelFormatter.FormatSideLabels(kOneGroup, RuleLabelFormatter.kHtmlAnd, RuleLabelFormatter.kHtmlOr),
                    Is.EqualTo("Stage: Prod ∩ Stage: Test"));
                Assert.That(RuleDisplayCsv.FormatSideLabelsCsv(kTwoGroups), Is.EqualTo("(AppRole: AR1 AND Stage: Prod) OR (AppRole: AR2)"));
                Assert.That(RuleLabelFormatter.FormatSideLabels(null, RuleLabelFormatter.kHtmlAnd, RuleLabelFormatter.kHtmlOr), Is.Empty);
            });
        }

        [Test]
        public void FormatRuleLabels_ListsDistinctLabels()
        {
            Assert.That(RuleLabelFormatter.FormatRuleLabels(kAppLabels), Is.EqualTo("App: APP-1, App: APP-2"));
            Assert.That(RuleLabelFormatter.FormatRuleLabels(null), Is.Empty);
        }

        [Test]
        public void Json_KeepsTheGroupStructure()
        {
            Assert.Multiple(() =>
            {
                Assert.That(RuleLabelFormatter.SideLabelsToJson(kTwoGroups), Is.EqualTo("[{\"AppRole\":\"AR1\",\"Stage\":\"Prod\"},{\"AppRole\":\"AR2\"}]"));
                Assert.That(RuleLabelFormatter.SideLabelsToJson(null), Is.EqualTo("null"));
                Assert.That(RuleLabelFormatter.RuleLabelsToJson(kAppLabels), Is.EqualTo("{\"App\":[\"APP-1\",\"APP-2\"]}"));
                Assert.That(RuleLabelFormatter.RuleLabelsToJson(null), Is.EqualTo("{}"));
            });
        }

        [Test]
        public void Html_EncodesLabelsFromImports()
        {
            Rule rule = new() { SourceLabels = kPayloadGroup, DestinationLabels = kPayloadGroup, RuleLabels = [new() { Label = new() { Value = kPayload, Key = new() { Name = kPayload } } }] };
            RuleDisplayHtml display = new(new SimulatedUserConfig());

            List<string> outputs =
            [
                RuleDisplayHtml.DisplaySourceLabels(rule), RuleDisplayHtml.DisplayDestinationLabels(rule), RuleDisplayHtml.DisplayRuleLabels(rule),
                display.DisplaySourceLabelsCell(rule), display.DisplayDestinationLabelsCell(rule), display.DisplayRuleLabelsCell(rule)
            ];

            Assert.Multiple(() =>
            {
                foreach (string output in outputs)
                {
                    Assert.That(output, Does.Not.Contain("<script>"));
                    Assert.That(output, Does.Not.Contain("\" onmouseover"));
                    Assert.That(output, Does.Not.Contain("x'"));
                    Assert.That(output, Does.Contain("&lt;script&gt;"));
                }
            });
        }

        [TestCase(RuleLabelDisplayMode.LabelColumns, true, false, true)]
        [TestCase(RuleLabelDisplayMode.LabelsReplaceObjects, false, true, true)]
        [TestCase(RuleLabelDisplayMode.LabelsWithObjects, false, true, true)]
        [TestCase(RuleLabelDisplayMode.ObjectsOnly, false, false, false)]
        public void Html_FollowsTheDisplayMode(RuleLabelDisplayMode mode, bool columns, bool inCell, bool fields)
        {
            Rule rule = new() { SourceLabels = kTwoGroups, DestinationLabels = kOneGroup };
            RuleDisplayHtml display = new(new SimulatedUserConfig { RuleLabelDisplayMode = mode, ShowRuleLevelLabels = false });

            string source = display.DisplaySource(rule, OutputLocation.export, ReportType.Rules);
            string destination = display.DisplayDestination(rule, OutputLocation.export, ReportType.Rules);

            Assert.Multiple(() =>
            {
                Assert.That(display.DisplaySourceLabelsHeader().Length > 0, Is.EqualTo(columns));
                Assert.That(display.DisplayDestinationLabelsCell(rule).Contains("Stage: Prod"), Is.EqualTo(columns));
                Assert.That(source.Contains("AppRole: AR1 ∩ Stage: Prod"), Is.EqualTo(inCell));
                Assert.That(destination.Contains("Stage: Prod ∩ Stage: Test"), Is.EqualTo(inCell));
                Assert.That(display.ShowSideLabelFields, Is.EqualTo(fields));
                Assert.That(display.DisplayRuleLabelsHeader(), Is.Empty);
                Assert.That(display.LabelColumnCount, Is.EqualTo(columns ? 2 : 0));
            });
        }

        [Test]
        public void LabelObjectNames_FollowTheNamingOfTheImporter()
        {
            Assert.Multiple(() =>
            {
                Assert.That(RuleLabelFormatter.LabelObjectNames(kTwoGroups), Is.EquivalentTo(kTwoGroupsObjectNames));
                Assert.That(RuleLabelFormatter.LabelObjectNames(kOneGroup), Is.EquivalentTo(kOneGroupObjectNames));
                Assert.That(RuleLabelFormatter.LabelObjectNames(null), Is.Empty);
            });
        }

        [TestCase(RuleLabelDisplayMode.LabelsReplaceObjects, false)]
        [TestCase(RuleLabelDisplayMode.LabelsWithObjects, true)]
        [TestCase(RuleLabelDisplayMode.LabelColumns, true)]
        public void Html_ReplacesOnlyTheGroupsBuiltFromTheLabels(RuleLabelDisplayMode mode, bool labelObjectsShown)
        {
            RuleDisplayHtml display = new(new SimulatedUserConfig { RuleLabelDisplayMode = mode, ShowRuleLevelLabels = false });

            string source = display.DisplaySource(RuleWithLabelObjects(), OutputLocation.export, ReportType.Rules);

            Assert.Multiple(() =>
            {
                Assert.That(source.Contains(kAndLabelObjectHtml), Is.EqualTo(labelObjectsShown));
                Assert.That(source, Does.Contain(kPlainHost));
                Assert.That(source, Does.Contain("Other: X &amp; Y"));
                // a host named like a label is no label object
                Assert.That(source, Does.Contain("Stage: Prod"));
            });
        }

        [TestCase(RuleLabelDisplayMode.LabelsReplaceObjects, false)]
        [TestCase(RuleLabelDisplayMode.LabelsWithObjects, true)]
        public void Html_ResolvedReportsLeaveOutTheMembersOfReplacedLabelObjects(RuleLabelDisplayMode mode, bool membersShown)
        {
            RuleDisplayHtml display = new(new SimulatedUserConfig { RuleLabelDisplayMode = mode, ShowRuleLevelLabels = false });

            string source = display.DisplaySource(RuleWithLabelObjects(), OutputLocation.export, ReportType.ResolvedRules);

            Assert.Multiple(() =>
            {
                Assert.That(source.Contains(kLabelMember), Is.EqualTo(membersShown));
                Assert.That(source, Does.Contain(kPlainHost));
                Assert.That(source, Does.Contain("AppRole: AR1 ∩ Stage: Prod"));
            });
        }

        [Test]
        public void Html_AppRulesLeaveOutReplacedLabelObjectsOfTheDisregardedObjects()
        {
            RuleDisplayHtml display = new(new SimulatedUserConfig { RuleLabelDisplayMode = RuleLabelDisplayMode.LabelsReplaceObjects, ShowRuleLevelLabels = false });
            Rule rule = RuleWithLabelObjects();
            rule.DisregardedFroms = [.. rule.Froms];
            rule.Froms = [Location(kPlainHost, ObjectType.Host)];
            rule.ShowDisregarded = true;

            string source = display.DisplaySource(rule, OutputLocation.export, ReportType.AppRules);

            Assert.Multiple(() =>
            {
                Assert.That(source, Does.Not.Contain(kAndLabelObjectHtml));
                Assert.That(source, Does.Contain("Other: X &amp; Y"));
            });
        }

        [Test]
        public void Html_ReplacedLabelsAreEncoded()
        {
            RuleDisplayHtml display = new(new SimulatedUserConfig { RuleLabelDisplayMode = RuleLabelDisplayMode.LabelsReplaceObjects, ShowRuleLevelLabels = false });
            Rule rule = new()
            {
                SourceLabels = kPayloadGroup,
                Froms = [Location($"{kPayload}: {kPayload}", ObjectType.Group), Location(kPlainHost, ObjectType.Host)]
            };

            string source = display.DisplaySource(rule, OutputLocation.export, ReportType.Rules);

            Assert.Multiple(() =>
            {
                Assert.That(source, Does.Not.Contain("<script>"));
                Assert.That(source, Does.Not.Contain("\" onmouseover"));
                Assert.That(source, Does.Contain("&lt;script&gt;"));
                Assert.That(source, Does.Contain(kPlainHost));
            });
        }

        [Test]
        public void Csv_AddsEscapedLabelFieldsUnlessObjectsOnly()
        {
            Rule rule = new() { SourceLabels = kQuoteGroup, RuleLabels = kAppLabels };
            RuleDisplayCsv withLabels = new(new SimulatedUserConfig { RuleLabelDisplayMode = RuleLabelDisplayMode.LabelsWithObjects });
            RuleDisplayCsv objectsOnly = new(new SimulatedUserConfig { RuleLabelDisplayMode = RuleLabelDisplayMode.ObjectsOnly, ShowRuleLevelLabels = false });

            Assert.Multiple(() =>
            {
                Assert.That(withLabels.DisplaySourceLabelsCsvHeader(), Is.EqualTo("\"source-labels\","));
                Assert.That(withLabels.DisplaySourceLabelsCsv(rule), Is.EqualTo("\"Stage: a\"\"b\","));
                Assert.That(withLabels.DisplayDestinationLabelsCsv(rule), Is.EqualTo("\"\","));
                Assert.That(withLabels.DisplayRuleLabelsCsv(rule), Is.EqualTo("\"App: APP-1, App: APP-2\","));
                Assert.That(objectsOnly.DisplaySourceLabelsCsvHeader() + objectsOnly.DisplaySourceLabelsCsv(rule), Is.Empty);
                Assert.That(objectsOnly.DisplayRuleLabelsCsvHeader() + objectsOnly.DisplayRuleLabelsCsv(rule), Is.Empty);
            });
        }

        [TestCase("=HYPERLINK(\"x\")")]
        [TestCase("+cmd")]
        [TestCase("-cmd")]
        [TestCase("@SUM(A1)")]
        public void Csv_NeutralizesFormulasInLabels(string formulaKey)
        {
            List<Dictionary<string, List<string>>> formulaGroup = [new() { [formulaKey] = ["v"] }];
            Rule rule = new() { SourceLabels = formulaGroup, DestinationLabels = formulaGroup, RuleLabels = [new() { Label = new() { Value = "v", Key = new() { Name = formulaKey } } }] };
            RuleChange inserted = new() { ChangeAction = 'I', OldRule = new(), NewRule = rule };
            RuleDisplayCsv display = new(new SimulatedUserConfig());
            RuleChangeDisplayCsv changeDisplay = new(new SimulatedUserConfig());

            List<string> outputs =
            [
                display.DisplaySourceLabelsCsv(rule), display.DisplayDestinationLabelsCsv(rule), display.DisplayRuleLabelsCsv(rule),
                changeDisplay.DisplaySourceLabelsCsv(inserted), changeDisplay.DisplayDestinationLabelsCsv(inserted), changeDisplay.DisplayRuleLabelsCsv(inserted)
            ];

            Assert.Multiple(() =>
            {
                foreach (string output in outputs)
                {
                    Assert.That(output, Does.StartWith("\"'"));
                }
            });
        }

        [Test]
        public void Json_RuleObjectContainsTheLabels()
        {
            Rule rule = new() { Name = "r1", SourceLabels = kTwoGroups, RuleLabels = kAppLabels };
            RuleDisplayJson display = new(new SimulatedUserConfig());

            JObject json = ParseRuleObject(display.DisplayRuleJsonObject(rule, ReportType.ResolvedRules));

            Assert.Multiple(() =>
            {
                Assert.That(json["source labels"]?[1]?["AppRole"]?.ToString(), Is.EqualTo("AR2"));
                Assert.That(json["destination labels"]?.Type, Is.EqualTo(JTokenType.Null));
                Assert.That(json["rule labels"]?["App"]?.Count(), Is.EqualTo(2));
            });
        }

        [Test]
        public void Json_ObjectsOnlyLeavesOutSideLabels()
        {
            Rule rule = new() { Name = "r1", SourceLabels = kTwoGroups };
            RuleDisplayJson display = new(new SimulatedUserConfig { RuleLabelDisplayMode = RuleLabelDisplayMode.ObjectsOnly, ShowRuleLevelLabels = false });

            JObject json = ParseRuleObject(display.DisplayRuleJsonObject(rule, ReportType.ResolvedRules));

            Assert.That(json.ContainsKey("source labels") || json.ContainsKey("rule labels"), Is.False);
        }

        [Test]
        public void Changes_ShowOldNewOrDifference()
        {
            SimulatedUserConfig userConfig = new();
            RuleChangeDisplayHtml html = new(userConfig);
            RuleChangeDisplayCsv csv = new(userConfig);
            RuleChangeDisplayJson json = new(userConfig);
            Rule oldRule = new() { SourceLabels = kOneGroup };
            Rule newRule = new() { SourceLabels = kTwoGroups };
            RuleChange deleted = new() { ChangeAction = 'D', OldRule = oldRule, NewRule = newRule };
            RuleChange changed = new() { ChangeAction = 'C', OldRule = oldRule, NewRule = newRule };
            RuleChange unchanged = new() { ChangeAction = 'C', OldRule = oldRule, NewRule = oldRule };

            Assert.Multiple(() =>
            {
                Assert.That(html.DisplayLabelChange(deleted, RuleDisplayHtml.DisplaySourceLabels), Does.Contain("Stage: Prod ∩ Stage: Test").And.Not.Contain("AR2"));
                Assert.That(html.DisplaySourceLabelsCell(changed), Does.Contain("Stage: Test").And.Contain("AppRole: AR2"));
                Assert.That(csv.DisplaySourceLabelsCsv(changed), Does.Contain("Stage: Prod AND Stage: Test").And.Contain("(AppRole: AR1 AND Stage: Prod) OR (AppRole: AR2)"));
                Assert.That(ParseRuleObject(json.DisplaySourceLabels(changed))["source labels"]?["deleted"]?[0]?["Stage"]?.Count(), Is.EqualTo(2));
                Assert.That(ParseRuleObject(json.DisplaySourceLabels(unchanged))["source labels"]?[0]?["Stage"]?.Count(), Is.EqualTo(2));
                Assert.That(ParseRuleObject(json.DisplayRuleLabels(deleted))["rule labels"]?.Type, Is.EqualTo(JTokenType.Object));
            });
        }

        [Test]
        public void RulesReport_ExportsLabelColumnsByDefault()
        {
            Rule rule = new() { Id = 1, Uid = "uid1", Name = "LabelledRule", Action = "accept", Track = "none", SourceLabels = kTwoGroups, DestinationLabels = kOneGroup, RuleLabels = kAppLabels };
            MockReportRules report = BuildRulesReport(rule);

            string csv = report.ExportToCsv();
            string html = report.ExportToHtml();

            Assert.Multiple(() =>
            {
                Assert.That(csv, Does.Contain("\"rule-name\",\"rule-labels\",\"source-zone\",\"source\",\"source-labels\",\"destination-zone\",\"destination\",\"destination-labels\""));
                Assert.That(csv, Does.Contain("\"(AppRole: AR1 AND Stage: Prod) OR (AppRole: AR2)\""));
                Assert.That(csv, Does.Contain("\"Stage: Prod AND Stage: Test\""));
                Assert.That(csv, Does.Contain("\"App: APP-1, App: APP-2\""));
                Assert.That(html, Does.Contain("<td>(AppRole: AR1 ∩ Stage: Prod) ∪ (AppRole: AR2)</td>"));
                Assert.That(html, Does.Contain("<td>App: APP-1, App: APP-2</td>"));
            });
        }

        [Test]
        public void LabelColumns_AreHiddenWhileNoRuleHasLabels()
        {
            SimulatedUserConfig userConfig = new();
            RuleDisplayHtml html = new(userConfig);
            RuleDisplayCsv csv = new(userConfig);
            RuleDisplayJson json = new(userConfig);
            Rule plainRule = new() { Name = "plain" };
            List<Rule> plainRules = [plainRule];
            html.SetLabelPresence(plainRules);
            csv.SetLabelPresence(plainRules);
            json.SetLabelPresence(plainRules);

            Assert.Multiple(() =>
            {
                Assert.That(html.DisplaySourceLabelsHeader() + html.DisplayDestinationLabelsHeader() + html.DisplayRuleLabelsHeader(), Is.Empty);
                Assert.That(html.LabelColumnCount, Is.Zero);
                Assert.That(csv.DisplaySourceLabelsCsvHeader() + csv.DisplayDestinationLabelsCsvHeader() + csv.DisplayRuleLabelsCsvHeader(), Is.Empty);
                Assert.That(ParseRuleObject(json.DisplayRuleJsonObject(plainRule, ReportType.ResolvedRules)).Properties().Select(property => property.Name),
                    Has.None.Contains("labels"));
            });
        }

        [Test]
        public void LabelColumns_AreShownPerKindOfLabel()
        {
            RuleDisplayHtml html = new(new SimulatedUserConfig());
            List<Rule> rules = [new() { Name = "plain" }, new() { Name = "labelled", SourceLabels = kOneGroup }];
            html.SetLabelPresence(rules);

            Assert.Multiple(() =>
            {
                Assert.That(html.DisplaySourceLabelsHeader(), Is.Not.Empty);
                Assert.That(html.DisplayDestinationLabelsHeader(), Is.Empty);
                Assert.That(html.DisplayRuleLabelsHeader(), Is.Empty);
                Assert.That(html.LabelColumnCount, Is.EqualTo(1));
            });
        }

        [Test]
        public void LabelColumns_OfChangesConsiderOldAndNewRules()
        {
            RuleChangeDisplayHtml html = new(new SimulatedUserConfig());
            List<RuleChange> changes = [new() { ChangeAction = 'D', OldRule = new() { RuleLabels = kAppLabels }, NewRule = new() }];
            html.SetLabelPresence(changes);

            Assert.That(html.DisplayRuleLabelsHeader(), Is.Not.Empty);
            Assert.That(html.DisplaySourceLabelsHeader(), Is.Empty);
        }

        [Test]
        public void RuleWithoutRuleLabels_IsSerializedWithoutThem()
        {
            Rule rule = new() { Name = "plain", RuleLabels = [] };

            Assert.That(rule.RuleLabels, Is.Null);
            Assert.That(System.Text.Json.JsonSerializer.Serialize(rule), Does.Not.Contain("rule_labels"));
            Assert.That(Newtonsoft.Json.JsonConvert.SerializeObject(rule), Does.Not.Contain("rule_labels"));
        }

        private static MockReportRules BuildRulesReport(Rule rule)
        {
            MockReportRules.RulebaseId = 0;
            MockReportRules.RuleId = 0;
            RulebaseReport rulebase = MockReportRules.CreateRulebaseReport(numberOfRules: 1);
            rule.RulebaseId = rulebase.Id;
            rulebase.Rules = [rule];
            MockReportRules report = new(new DynGraphqlQuery("TestFilter") { ReportTimeString = "2023-04-20T17:50:04" }, new SimulatedUserConfig(), ReportType.ResolvedRules);
            ManagementReport management = report.ReportData.ManagementData.First();
            management.Name = "TestMgt";
            management.Rulebases = [rulebase];
            management.Devices.First().RulebaseLinks = [new() { IsInitial = true, NextRulebaseId = rulebase.Id }];
            report.TryBuildMockRuleTree();
            return report;
        }

        private static JObject ParseRuleObject(string jsonFragment)
        {
            string trimmed = jsonFragment.Trim().TrimEnd(',');
            return JObject.Parse(trimmed.StartsWith('{') ? trimmed : $"{{{trimmed}}}");
        }

        private static Rule RuleWithLabelObjects()
        {
            NetworkLocation andLabelObject = Location("AppRole: AR1 & Stage: Prod", ObjectType.Group);
            andLabelObject.Object.ObjectGroupFlats = [new() { Object = new() { Name = kLabelMember, Type = new() { Name = ObjectType.Host } } }];
            return new()
            {
                SourceLabels = kTwoGroups,
                Froms =
                [
                    andLabelObject, Location("AppRole: AR2", ObjectType.Group), Location(kOtherGroup, ObjectType.Group),
                    Location("Stage: Prod", ObjectType.Host), Location(kPlainHost, ObjectType.Host)
                ]
            };
        }

        private static NetworkLocation Location(string name, string type)
        {
            return new(new NetworkUser(), new NetworkObject { Name = name, Type = new() { Name = type } });
        }

        private static RuleLabelAssignment AppLabel(string app)
        {
            return new() { Label = new() { Value = app, Key = new() { Name = "App" } } };
        }
    }
}
