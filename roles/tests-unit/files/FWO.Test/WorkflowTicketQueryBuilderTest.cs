using FWO.Data.Workflow;
using FWO.Ui.Services;
using NUnit.Framework;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace FWO.Test
{
    [TestFixture]
    [Parallelizable]
    internal class WorkflowTicketQueryBuilderTest
    {
        private const int kPageSize = 20;
        private const string kStateCondition = """{"_or":[{"state_id":{"_in":[200,210,600]}},{"reqtasks":{"state_id":{"_in":[200,210,600]}}},{"reqtasks":{"impltasks":{"state_id":{"_in":[200,210,600]}}}},{"reqtasks":{"approvals":{"state_id":{"_in":[200,210,600]}}}}]}""";
        private const string kTaskTypeCondition = """{"reqtasks":{"task_type":{"_in":["access"]}}}""";
        private const string kIdCondition7 = """{"_or":[{"id":{"_eq":7}},{"reqtasks":{"id":{"_eq":7}}},{"reqtasks":{"impltasks":{"id":{"_eq":7}}}},{"reqtasks":{"approvals":{"id":{"_eq":7}}}}]}""";

        private static readonly List<WfTaskType> kTaskTypes = [WfTaskType.access];
        private static readonly List<int> kStateIds = [200, 210, 600];
        private static readonly Dictionary<int, string> kStateNames = new() { [200] = "To Implement", [210] = "In Implementation", [600] = "Done" };
        private static readonly List<FancyColumnFilter> kNoFilters = [];

        private static FancyItemsRequest Request(string search = "", string? sortKey = null, bool ascending = true, FancyColumnFilter? filter = null, int page = 1)
        {
            List<FancyColumnFilter> filters = filter == null ? kNoFilters : [filter];
            return new FancyItemsRequest(search, sortKey, ascending, filters, page, kPageSize);
        }

        private static WorkflowTicketQuery Build(FancyItemsRequest request)
        {
            return WorkflowTicketQueryBuilder.Build(request, kTaskTypes, kStateIds, kStateNames);
        }

        private static string Normalize(string json)
        {
            return JsonNode.Parse(json)!.ToJsonString();
        }

        private static string ToJson(object value)
        {
            return JsonSerializer.Serialize(value);
        }

        /// <summary>
        /// Returns the condition the request added after the task type and state dropdown conditions.
        /// </summary>
        private static string ExtraCondition(WorkflowTicketQuery query)
        {
            JsonArray conditions = JsonNode.Parse(ToJson(query.Where))!["_and"]!.AsArray();
            Assert.That(conditions, Has.Count.EqualTo(3));
            return conditions[2]!.ToJsonString();
        }

        private static string StateCondition(string stateIds)
        {
            string stateIn = "{\"state_id\":{\"_in\":[" + stateIds + "]}}";
            return Normalize("{\"_or\":[" + stateIn + ","
                + "{\"reqtasks\":" + stateIn + "},"
                + "{\"reqtasks\":{\"impltasks\":" + stateIn + "}},"
                + "{\"reqtasks\":{\"approvals\":" + stateIn + "}}]}");
        }

        private static string Negated(string condition)
        {
            return Normalize("{\"_not\":" + condition + "}");
        }

        [Test]
        public void Build_DefaultRequest_OrdersByIdDescAndPagesWithLimitPlusOne()
        {
            WorkflowTicketQuery query = Build(Request(page: 3));

            Assert.Multiple(() =>
            {
                Assert.That(query.Limit, Is.EqualTo(21));
                Assert.That(query.Offset, Is.EqualTo(40));
                Assert.That(ToJson(query.OrderBy), Is.EqualTo(Normalize("""[{"id":"desc"}]""")));
            });
        }

        [Test]
        public void Build_TaskTypesAndStates_ProduceDropdownConditions()
        {
            WorkflowTicketQuery query = Build(Request());

            Assert.That(ToJson(query.Where), Is.EqualTo(Normalize($$$"""{"_and":[{{{kTaskTypeCondition}}},{{{kStateCondition}}}]}""")));
        }

        [Test]
        public void Build_SearchText_IsIgnored()
        {
            WorkflowTicketQuery query = Build(Request(search: "fw"));

            Assert.That(ToJson(query.Where), Is.EqualTo(Normalize($$$"""{"_and":[{{{kTaskTypeCondition}}},{{{kStateCondition}}}]}""")));
        }

        [TestCase(FancyColumnFilterOperator.Contains, "impl", "200,210")]
        [TestCase(FancyColumnFilterOperator.Equals, "done", "600")]
        [TestCase(FancyColumnFilterOperator.StartsWith, "to", "200")]
        [TestCase(FancyColumnFilterOperator.EndsWith, "implementation", "210")]
        [TestCase(FancyColumnFilterOperator.IsNotEmpty, "", "200,210,600")]
        public void StateFilter_PositiveOperator_ResolvesNamesToIdsOnAnyLevel(FancyColumnFilterOperator filterOperator, string value, string stateIds)
        {
            string condition = ExtraCondition(Build(Request(filter: new FancyColumnFilter("state", filterOperator, value))));

            Assert.That(condition, Is.EqualTo(StateCondition(stateIds)));
        }

        [Test]
        public void NotContains_IsNegationOfAnyLevelMatch()
        {
            string condition = ExtraCondition(Build(Request(filter: new FancyColumnFilter("state", FancyColumnFilterOperator.NotContains, "impl"))));

            Assert.That(condition, Is.EqualTo(Negated(StateCondition("200,210"))));
        }

        [Test]
        public void NotEquals_IsNegation()
        {
            string condition = ExtraCondition(Build(Request(filter: new FancyColumnFilter("id", FancyColumnFilterOperator.NotEquals, "7"))));

            Assert.That(condition, Is.EqualTo(Negated(kIdCondition7)));
        }

        [Test]
        public void IsEmpty_IsNegationOfIsNotEmpty()
        {
            string condition = ExtraCondition(Build(Request(filter: new FancyColumnFilter("state", FancyColumnFilterOperator.IsEmpty))));

            Assert.That(condition, Is.EqualTo(Negated(StateCondition("200,210,600"))));
        }

        [Test]
        public void IdFilter_IsNotEmpty_MatchesAll()
        {
            string condition = ExtraCondition(Build(Request(filter: new FancyColumnFilter("id", FancyColumnFilterOperator.IsNotEmpty))));

            Assert.That(condition, Is.EqualTo("{}"));
        }

        [Test]
        public void IdFilter_Numeric_EqualsIdOnAnyLevel()
        {
            string condition = ExtraCondition(Build(Request(filter: new FancyColumnFilter("id", FancyColumnFilterOperator.Equals, "7"))));

            Assert.That(condition, Is.EqualTo(Normalize(kIdCondition7)));
        }

        [Test]
        public void IdFilter_NonNumeric_EqualsMatchesNothing()
        {
            string condition = ExtraCondition(Build(Request(filter: new FancyColumnFilter("id", FancyColumnFilterOperator.Equals, "abc"))));

            Assert.That(condition, Is.EqualTo(Normalize("""{"id":{"_is_null":true}}""")));
        }

        [Test]
        public void TaskNumberFilter_MatchesRequestAndImplementationTasks()
        {
            string condition = ExtraCondition(Build(Request(filter: new FancyColumnFilter("task_number", FancyColumnFilterOperator.Equals, "2"))));

            Assert.That(condition, Is.EqualTo(Normalize("""{"_or":[{"reqtasks":{"task_number":{"_eq":2}}},{"reqtasks":{"impltasks":{"task_number":{"_eq":2}}}}]}""")));
        }

        [TestCase("type")]
        [TestCase("name")]
        [TestCase("requester")]
        [TestCase("flow_bundle_id")]
        public void Filter_TextOrUnknownColumn_AddsNoCondition(string columnKey)
        {
            WorkflowTicketQuery query = Build(Request(filter: new FancyColumnFilter(columnKey, FancyColumnFilterOperator.Contains, "x")));

            Assert.That(JsonNode.Parse(ToJson(query.Where))!["_and"]!.AsArray(), Has.Count.EqualTo(2));
        }

        [TestCase("id", true, """[{"id":"asc"}]""")]
        [TestCase("name", true, """[{"title":"asc"},{"id":"desc"}]""")]
        [TestCase("requester", false, """[{"id":"desc"}]""")]
        [TestCase("state", true, """[{"state_id":"asc"},{"id":"desc"}]""")]
        [TestCase("start_time", false, """[{"date_created":"desc"},{"id":"desc"}]""")]
        [TestCase("end_time", true, """[{"date_completed":"asc"},{"id":"desc"}]""")]
        [TestCase("type", true, """[{"id":"desc"}]""")]
        public void Sort_EachKey_MapsToOrderByWithIdTieBreaker(string sortKey, bool ascending, string expected)
        {
            WorkflowTicketQuery query = Build(Request(sortKey: sortKey, ascending: ascending));

            Assert.That(ToJson(query.OrderBy), Is.EqualTo(Normalize(expected)));
        }
    }
}
