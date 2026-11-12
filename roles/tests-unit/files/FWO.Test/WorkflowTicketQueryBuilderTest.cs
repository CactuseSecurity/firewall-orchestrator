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
        private static readonly List<string> kNameFields = ["title", "title", "title", "approver_group"];
        private static readonly List<string> kStartFields = ["date_created", "start", "start", "date_opened"];
        private static readonly List<string> kEndFields = ["date_completed", "stop", "stop", "approval_date"];

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

        /// <summary>
        /// Builds the expected condition comparing the given fields of ticket, request task, implementation task and approval.
        /// </summary>
        private static string AnyLevelCondition(List<string> fields, string comparison)
        {
            return Normalize("{\"_or\":["
                + "{\"" + fields[0] + "\":" + comparison + "},"
                + "{\"reqtasks\":{\"" + fields[1] + "\":" + comparison + "}},"
                + "{\"reqtasks\":{\"impltasks\":{\"" + fields[2] + "\":" + comparison + "}}},"
                + "{\"reqtasks\":{\"approvals\":{\"" + fields[3] + "\":" + comparison + "}}}]}");
        }

        private static string NameCondition(string comparison)
        {
            return AnyLevelCondition(kNameFields, comparison);
        }

        private static string DateCondition(List<string> fields, string comparison)
        {
            return AnyLevelCondition(fields, comparison);
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

        [TestCase(FancyColumnFilterOperator.Contains, "7")]
        [TestCase(FancyColumnFilterOperator.NotContains, "7")]
        [TestCase(FancyColumnFilterOperator.StartsWith, "7")]
        [TestCase(FancyColumnFilterOperator.EndsWith, "7")]
        [TestCase(FancyColumnFilterOperator.IsEmpty, null)]
        [TestCase(FancyColumnFilterOperator.IsNotEmpty, null)]
        public void IdFilter_UnsupportedOperator_AddsNoCondition(FancyColumnFilterOperator filterOperator, string? value)
        {
            WorkflowTicketQuery query = Build(Request(filter: new FancyColumnFilter("id", filterOperator, value)));

            Assert.That(ToJson(query.Where), Is.EqualTo(Normalize($$$"""{"_and":[{{{kTaskTypeCondition}}},{{{kStateCondition}}}]}""")));
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

        [TestCase(FancyColumnFilterOperator.Contains, """{"_ilike":"%ali%"}""")]
        [TestCase(FancyColumnFilterOperator.Equals, """{"_ilike":"ali"}""")]
        [TestCase(FancyColumnFilterOperator.StartsWith, """{"_ilike":"ali%"}""")]
        [TestCase(FancyColumnFilterOperator.EndsWith, """{"_ilike":"%ali"}""")]
        [TestCase(FancyColumnFilterOperator.IsNotEmpty, """{"_is_null":false,"_neq":""}""")]
        public void RequesterFilter_PositiveOperator_ComparesUserName(FancyColumnFilterOperator filterOperator, string comparison)
        {
            string condition = ExtraCondition(Build(Request(filter: new FancyColumnFilter("requester", filterOperator, " ali "))));

            Assert.That(condition, Is.EqualTo(Normalize($$$"""{"uiuser":{"uiuser_username":{{{comparison}}}}}""")));
        }

        [TestCase(FancyColumnFilterOperator.NotContains, """{"_ilike":"%ali%"}""")]
        [TestCase(FancyColumnFilterOperator.NotEquals, """{"_ilike":"ali"}""")]
        [TestCase(FancyColumnFilterOperator.IsEmpty, """{"_is_null":false,"_neq":""}""")]
        public void RequesterFilter_NegatedOperator_NegatesCondition(FancyColumnFilterOperator filterOperator, string comparison)
        {
            string condition = ExtraCondition(Build(Request(filter: new FancyColumnFilter("requester", filterOperator, "ali"))));

            Assert.That(condition, Is.EqualTo(Negated($$$"""{"uiuser":{"uiuser_username":{{{comparison}}}}}""")));
        }

        [Test]
        public void RequesterFilter_EscapesLikeWildcards()
        {
            string condition = ExtraCondition(Build(Request(filter: new FancyColumnFilter("requester", FancyColumnFilterOperator.Contains, @"50%_a\b"))));

            Assert.That(condition, Is.EqualTo(Normalize("""{"uiuser":{"uiuser_username":{"_ilike":"%50\\%\\_a\\\\b%"}}}""")));
        }

        [TestCase("type")]
        [TestCase("name")]
        [TestCase("task_number")]
        [TestCase("flow_bundle_id")]
        public void Filter_TextOrUnknownColumn_AddsNoCondition(string columnKey)
        {
            WorkflowTicketQuery query = Build(Request(filter: new FancyColumnFilter(columnKey, FancyColumnFilterOperator.Contains, "x")));

            Assert.That(JsonNode.Parse(ToJson(query.Where))!["_and"]!.AsArray(), Has.Count.EqualTo(2));
        }

        [TestCase(FancyColumnFilterOperator.Equals, """{"_ilike":"rule"}""")]
        [TestCase(FancyColumnFilterOperator.StartsWith, """{"_ilike":"rule%"}""")]
        public void NameFilter_SupportedOperator_ComparesTitlesOnAllLevels(FancyColumnFilterOperator filterOperator, string comparison)
        {
            string condition = ExtraCondition(Build(Request(filter: new FancyColumnFilter("name", filterOperator, " rule "))));

            Assert.That(condition, Is.EqualTo(NameCondition(comparison)));
        }

        [Test]
        public void NameFilter_NotEquals_NegatesEqualsCondition()
        {
            string condition = ExtraCondition(Build(Request(filter: new FancyColumnFilter("name", FancyColumnFilterOperator.NotEquals, "rule"))));

            Assert.That(condition, Is.EqualTo(Negated(NameCondition("""{"_ilike":"rule"}"""))));
        }

        [TestCase(FancyColumnFilterOperator.EndsWith)]
        [TestCase(FancyColumnFilterOperator.NotContains)]
        [TestCase(FancyColumnFilterOperator.IsEmpty)]
        [TestCase(FancyColumnFilterOperator.IsNotEmpty)]
        public void NameFilter_UnsupportedOperator_AddsNoCondition(FancyColumnFilterOperator filterOperator)
        {
            WorkflowTicketQuery query = Build(Request(filter: new FancyColumnFilter("name", filterOperator, "rule")));

            Assert.That(JsonNode.Parse(ToJson(query.Where))!["_and"]!.AsArray(), Has.Count.EqualTo(2));
        }

        [TestCase(FancyColumnFilterOperator.Equals, """{"_gte":"2026-11-10","_lt":"2026-11-11"}""")]
        [TestCase(FancyColumnFilterOperator.GreaterThan, """{"_gte":"2026-11-11"}""")]
        [TestCase(FancyColumnFilterOperator.GreaterThanOrEqual, """{"_gte":"2026-11-10"}""")]
        [TestCase(FancyColumnFilterOperator.LessThan, """{"_lt":"2026-11-10"}""")]
        [TestCase(FancyColumnFilterOperator.LessThanOrEqual, """{"_lt":"2026-11-11"}""")]
        [TestCase(FancyColumnFilterOperator.IsNotEmpty, """{"_is_null":false}""")]
        public void StartTimeFilter_ComparesDayOnAllLevels(FancyColumnFilterOperator filterOperator, string comparison)
        {
            string condition = ExtraCondition(Build(Request(filter: new FancyColumnFilter("start_time", filterOperator, "2026-11-10"))));

            Assert.That(condition, Is.EqualTo(DateCondition(kStartFields, comparison)));
        }

        [TestCase(FancyColumnFilterOperator.NotEquals, """{"_gte":"2026-11-10","_lt":"2026-11-11"}""")]
        [TestCase(FancyColumnFilterOperator.IsEmpty, """{"_is_null":false}""")]
        public void EndTimeFilter_NegatedOperator_NegatesConditionOnAllLevels(FancyColumnFilterOperator filterOperator, string comparison)
        {
            string condition = ExtraCondition(Build(Request(filter: new FancyColumnFilter("end_time", filterOperator, "2026-11-10"))));

            Assert.That(condition, Is.EqualTo(Negated(DateCondition(kEndFields, comparison))));
        }

        [TestCase("2026-11-01", "2026-11-10")]
        [TestCase("2026-11-10", "2026-11-01")]
        public void DateFilter_Between_IncludesBothDaysInAnyOrder(string value, string valueTo)
        {
            FancyColumnFilter filter = new("end_time", FancyColumnFilterOperator.Between, value, valueTo);

            string condition = ExtraCondition(Build(Request(filter: filter)));

            Assert.That(condition, Is.EqualTo(DateCondition(kEndFields, """{"_gte":"2026-11-01","_lt":"2026-11-11"}""")));
        }

        [Test]
        public void DateFilter_LastRepresentableDay_EndsAtInfinity()
        {
            string condition = ExtraCondition(Build(Request(filter: new FancyColumnFilter("start_time", FancyColumnFilterOperator.LessThanOrEqual, "9999-12-31"))));

            Assert.That(condition, Is.EqualTo(DateCondition(kStartFields, """{"_lt":"infinity"}""")));
        }

        [TestCase(FancyColumnFilterOperator.Equals, "10.11.2026", null)]
        [TestCase(FancyColumnFilterOperator.Equals, "", null)]
        [TestCase(FancyColumnFilterOperator.Between, "2026-11-01", null)]
        [TestCase(FancyColumnFilterOperator.Between, "2026-11-01", "x")]
        [TestCase(FancyColumnFilterOperator.Contains, "2026-11-01", null)]
        public void DateFilter_InvalidValueOrOperator_AddsNoCondition(FancyColumnFilterOperator filterOperator, string value, string? valueTo)
        {
            WorkflowTicketQuery query = Build(Request(filter: new FancyColumnFilter("start_time", filterOperator, value, valueTo)));

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
