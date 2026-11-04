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

        private static string NameCondition(string comparison)
        {
            return Normalize("{\"_or\":[{\"title\":" + comparison + "},"
                + "{\"reqtasks\":{\"title\":" + comparison + "}},"
                + "{\"reqtasks\":{\"impltasks\":{\"title\":" + comparison + "}}},"
                + "{\"reqtasks\":{\"approvals\":{\"approver_group\":" + comparison + "}}}]}");
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
        public void Build_Search_Text_MatchesNameRequesterBundleOnAnyLevel()
        {
            string contains = """{"_ilike":"%fw%"}""";

            string condition = ExtraCondition(Build(Request(search: " fw ")));

            Assert.That(condition, Is.EqualTo(Normalize($$$"""{"_or":[{{{NameCondition(contains)}}},{"uiuser":{"uiuser_username":{{{contains}}}}},{"reqtasks":{"additional_info":{{{contains}}}}}]}""")));
        }

        [Test]
        public void Build_Search_Numeric_AddsIdAndTaskNumberEquals()
        {
            JsonArray alternatives = JsonNode.Parse(ExtraCondition(Build(Request(search: "42"))))!["_or"]!.AsArray();

            Assert.Multiple(() =>
            {
                Assert.That(alternatives, Has.Count.EqualTo(5));
                Assert.That(alternatives[3]!.ToJsonString(), Is.EqualTo(Normalize("""{"id":{"_eq":42}}""")));
                Assert.That(alternatives[4]!.ToJsonString(), Is.EqualTo(Normalize("""{"_or":[{"reqtasks":{"task_number":{"_eq":42}}},{"reqtasks":{"impltasks":{"task_number":{"_eq":42}}}}]}""")));
            });
        }

        [Test]
        public void Build_Search_StateName_MatchesStatesOnAnyLevel()
        {
            JsonArray alternatives = JsonNode.Parse(ExtraCondition(Build(Request(search: "impl"))))!["_or"]!.AsArray();

            Assert.That(alternatives.Select(alternative => alternative!.ToJsonString()),
                Does.Contain(Normalize("""{"_or":[{"state_id":{"_in":[200,210]}},{"reqtasks":{"state_id":{"_in":[200,210]}}},{"reqtasks":{"impltasks":{"state_id":{"_in":[200,210]}}}},{"reqtasks":{"approvals":{"state_id":{"_in":[200,210]}}}}]}""")));
        }

        [Test]
        public void Escape_WildcardCharacters()
        {
            string condition = ExtraCondition(Build(Request(filter: new FancyColumnFilter("requester", FancyColumnFilterOperator.Contains, @"50%_a\b"))));

            Assert.That(condition, Is.EqualTo(Normalize("""{"uiuser":{"uiuser_username":{"_ilike":"%50\\%\\_a\\\\b%"}}}""")));
        }

        [TestCase(FancyColumnFilterOperator.Contains, """{"_ilike":"%fw%"}""")]
        [TestCase(FancyColumnFilterOperator.Equals, """{"_ilike":"fw"}""")]
        [TestCase(FancyColumnFilterOperator.StartsWith, """{"_ilike":"fw%"}""")]
        [TestCase(FancyColumnFilterOperator.EndsWith, """{"_ilike":"%fw"}""")]
        [TestCase(FancyColumnFilterOperator.IsNotEmpty, """{"_is_null":false,"_neq":""}""")]
        public void NameFilter_PositiveOperator_MatchesAnyLevel(FancyColumnFilterOperator filterOperator, string comparison)
        {
            string condition = ExtraCondition(Build(Request(filter: new FancyColumnFilter("name", filterOperator, "fw"))));

            Assert.That(condition, Is.EqualTo(NameCondition(comparison)));
        }

        [Test]
        public void NotContains_IsNegationOfAnyLevelMatch()
        {
            string condition = ExtraCondition(Build(Request(filter: new FancyColumnFilter("name", FancyColumnFilterOperator.NotContains, "fw"))));

            Assert.That(condition, Is.EqualTo(Negated(NameCondition("""{"_ilike":"%fw%"}"""))));
        }

        [Test]
        public void NotEquals_IsNegation()
        {
            string condition = ExtraCondition(Build(Request(filter: new FancyColumnFilter("name", FancyColumnFilterOperator.NotEquals, "fw"))));

            Assert.That(condition, Is.EqualTo(Negated(NameCondition("""{"_ilike":"fw"}"""))));
        }

        [Test]
        public void IsEmpty_IsNegationOfIsNotEmpty()
        {
            string condition = ExtraCondition(Build(Request(filter: new FancyColumnFilter("name", FancyColumnFilterOperator.IsEmpty))));

            Assert.That(condition, Is.EqualTo(Negated(NameCondition("""{"_is_null":false,"_neq":""}"""))));
        }

        [Test]
        public void StateFilter_ResolvesNamesToIds()
        {
            string condition = ExtraCondition(Build(Request(filter: new FancyColumnFilter("state", FancyColumnFilterOperator.Contains, "impl"))));

            Assert.That(condition, Is.EqualTo(Normalize("""{"_or":[{"state_id":{"_in":[200,210]}},{"reqtasks":{"state_id":{"_in":[200,210]}}},{"reqtasks":{"impltasks":{"state_id":{"_in":[200,210]}}}},{"reqtasks":{"approvals":{"state_id":{"_in":[200,210]}}}}]}""")));
        }

        [Test]
        public void IdFilter_Numeric_EqualsId()
        {
            string condition = ExtraCondition(Build(Request(filter: new FancyColumnFilter("id", FancyColumnFilterOperator.Equals, "7"))));

            Assert.That(condition, Is.EqualTo(Normalize("""{"id":{"_eq":7}}""")));
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

        [TestCase(FancyColumnFilterOperator.Equals, """{"_ilike":"%\"bundle-4\"%"}""")]
        [TestCase(FancyColumnFilterOperator.Contains, """{"_ilike":"%bundle-4%"}""")]
        [TestCase(FancyColumnFilterOperator.IsNotEmpty, """{"_ilike":"%\"FlowBundleId\"%"}""")]
        public void BundleFilter_MatchesStoredAdditionalInfo(FancyColumnFilterOperator filterOperator, string comparison)
        {
            string condition = ExtraCondition(Build(Request(filter: new FancyColumnFilter("flow_bundle_id", filterOperator, "bundle-4"))));

            Assert.That(condition, Is.EqualTo(Normalize($$$"""{"reqtasks":{"additional_info":{{{comparison}}}}}""")));
        }

        [Test]
        public void Filter_UnknownColumn_AddsNoCondition()
        {
            WorkflowTicketQuery query = Build(Request(filter: new FancyColumnFilter("type", FancyColumnFilterOperator.Contains, "x")));

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
