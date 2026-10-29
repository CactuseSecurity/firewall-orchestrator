using System.Collections;
using System.Reflection;
using FWO.Data;
using FWO.Data.Report;
using FWO.Services.RuleTreeBuilder;
using FWO.Ui.Pages.Reporting.Reports;
using NUnit.Framework;
using static FWO.Test.WorkflowConfigurationComponentTestSupport;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class FancyRulesReportTest
    {
        private const int kManagementId = 1;
        private const int kDeviceId = 2;
        private const int kSiblingRuleCount = 12;
        private const string kLayerName = "Layer";

        [Test]
        public void OnParametersSet_RebuildsRows_WhenReportIsRegeneratedWithEqualIdsAndCounts()
        {
            RuleTreeBuilder ruleTreeBuilder = new();
            FancyRulesReport component = CreateComponent(ruleTreeBuilder);
            SetReport(component, ruleTreeBuilder, CreateRuleTree(CreateRuleNode("OLD", 1, "1")));
            Invoke(component, "OnParametersSet");

            SetReport(component, ruleTreeBuilder, CreateRuleTree(CreateRuleNode("NEW", 1, "1")));
            Invoke(component, "OnParametersSet");

            object row = GetRows(component).Single();
            Assert.That(GetValue<string>(row, "DisplayName"), Is.EqualTo("NEW"));
        }

        [Test]
        public void OnParametersSet_KeepsRows_WhenReportIsUnchanged()
        {
            RuleTreeBuilder ruleTreeBuilder = new();
            FancyRulesReport component = CreateComponent(ruleTreeBuilder);
            SetReport(component, ruleTreeBuilder, CreateRuleTree(CreateRuleNode("rule", 1, "1")));
            Invoke(component, "OnParametersSet");
            object firstRow = GetRows(component).Single();

            Invoke(component, "OnParametersSet");

            Assert.That(GetRows(component).Single(), Is.SameAs(firstRow));
        }

        [TestCase(true, false)]
        [TestCase(false, true)]
        public void StructuralHeaders_AreRenderedAsHeadersAndNotCountedAsRules(bool isOrderedLayerHeader, bool isPolicyHeader)
        {
            RuleTreeItem header = new()
            {
                Data = new Rule { SectionHeader = kLayerName, DisplayOrderNumber = 1 },
                IsOrderedLayerHeader = isOrderedLayerHeader,
                IsPolicyHeader = isPolicyHeader
            };
            header.Children.Add(CreateRuleNode("rule", 2, "1.1"));
            RuleTreeBuilder ruleTreeBuilder = new();
            FancyRulesReport component = CreateComponent(ruleTreeBuilder);
            SetReport(component, ruleTreeBuilder, CreateRuleTree(header));

            Invoke(component, "OnParametersSet");

            object headerRow = GetRows(component).Single();
            Assert.Multiple(() =>
            {
                Assert.That(GetValue<bool>(headerRow, "IsStructuralHeader"), Is.True);
                Assert.That(GetValue<string>(headerRow, "DisplayName"), Is.Empty);
                Assert.That(GetValue<string>(headerRow, "SectionHeaderTitle"), Is.EqualTo(kLayerName));
                Assert.That(CountRealRuleDescendants(headerRow), Is.EqualTo(1));
            });
        }

        [Test]
        public void NumberColumn_SortsNumericallyByDisplayOrder()
        {
            RuleTreeItem[] siblings = Enumerable.Range(1, kSiblingRuleCount).Select(index => CreateRuleNode($"rule{index}", index, $"1.{index}")).ToArray();
            RuleTreeBuilder ruleTreeBuilder = new();
            FancyRulesReport component = CreateComponent(ruleTreeBuilder);
            SetReport(component, ruleTreeBuilder, CreateRuleTree(siblings));
            Invoke(component, "OnParametersSet");

            object numberColumn = GetColumn(component, "number");
            Delegate valueSelector = GetValue<Delegate>(numberColumn, "ValueSelector");
            Delegate sortValueSelector = GetValue<Delegate>(numberColumn, "SortValueSelector");
            List<string> sortedNumbers = [.. GetRows(component)
                .OrderBy(row => (IComparable?)sortValueSelector.DynamicInvoke(row))
                .Select(row => (string)valueSelector.DynamicInvoke(row)!)];

            List<string> expectedNumbers = [.. Enumerable.Range(1, kSiblingRuleCount).Select(index => $"1.{index}")];
            Assert.That(sortedNumbers, Is.EqualTo(expectedNumbers));
        }

        private static FancyRulesReport CreateComponent(RuleTreeBuilder ruleTreeBuilder)
        {
            FancyRulesReport component = new();
            SetProperty(component, "userConfig", new SimulatedUserConfig());
            SetProperty(component, "ruleTreeBuilder", ruleTreeBuilder);
            Invoke(component, "OnInitialized");
            return component;
        }

        private static void SetReport(FancyRulesReport component, RuleTreeBuilder ruleTreeBuilder, RuleTreeItem ruleTree)
        {
            DeviceReport device = new() { Id = kDeviceId, Name = "device" };
            device.AddRule(new Rule());
            ManagementReport management = new() { Id = kManagementId, Devices = new List<DeviceReport> { device }.ToArray() };
            SetProperty(component, nameof(FancyRulesReport.Managements), new List<ManagementReport> { management });
            ruleTreeBuilder.RuleTreeCache[(kManagementId, kDeviceId)] = ruleTree;
        }

        private static RuleTreeItem CreateRuleTree(params RuleTreeItem[] children)
        {
            RuleTreeItem root = new();
            root.Children.AddRange(children);
            return root;
        }

        private static RuleTreeItem CreateRuleNode(string name, int displayOrderNumber, string displayOrderNumberString)
        {
            return new RuleTreeItem
            {
                Data = new Rule { Name = name, DisplayOrderNumber = displayOrderNumber, DisplayOrderNumberString = displayOrderNumberString },
                IsRule = true
            };
        }

        private static List<object> GetRows(FancyRulesReport component)
        {
            IDictionary rowsByDevice = GetField<IDictionary>(component, "_ruleRowsByDevice");
            return [.. ((IEnumerable)rowsByDevice[(kManagementId, kDeviceId)]!).Cast<object>()];
        }

        private static object GetColumn(FancyRulesReport component, string key)
        {
            IEnumerable columns = GetField<IEnumerable>(component, "_ruleColumns");
            return columns.Cast<object>().Single(column => GetValue<string>(column, "Key") == key);
        }

        private static int CountRealRuleDescendants(object row)
        {
            MethodInfo method = typeof(FancyRulesReport).GetMethod("CountRealRuleDescendants", BindingFlags.Static | BindingFlags.NonPublic)
                ?? throw new MissingMethodException(nameof(FancyRulesReport), "CountRealRuleDescendants");
            return (int)method.Invoke(null, new List<object?> { row }.ToArray())!;
        }

        private static T GetValue<T>(object instance, string propertyName)
        {
            PropertyInfo property = instance.GetType().GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                ?? throw new MissingMemberException(instance.GetType().FullName, propertyName);
            return (T)property.GetValue(instance)!;
        }
    }
}
