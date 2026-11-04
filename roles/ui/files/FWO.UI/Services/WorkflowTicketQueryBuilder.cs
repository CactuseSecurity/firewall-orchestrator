using FWO.Data.Workflow;
using System.Globalization;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Variables of the paged workflow ticket query: Hasura where and order_by expressions, limit and offset.
    /// </summary>
    public sealed record WorkflowTicketQuery(Dictionary<string, object> Where, List<Dictionary<string, object>> OrderBy, int Limit, int Offset);

    /// <summary>
    /// Translates the state of the workflow monitoring tree table (search, column filters, sort, page) and the
    /// page's task type and state dropdowns into the variables of the paged ticket query.
    /// A search or column filter matches if any level of a ticket (ticket, request task, implementation task, approval)
    /// matches; negated operators mean that no level matches.
    /// </summary>
    public static class WorkflowTicketQueryBuilder
    {
        private const string kAnd = "_and";
        private const string kOr = "_or";
        private const string kNot = "_not";
        private const string kIn = "_in";
        private const string kEq = "_eq";
        private const string kIlike = "_ilike";
        private const string kReqTasks = "reqtasks";
        private const string kImplTasks = "impltasks";
        private const string kApprovals = "approvals";
        private const string kStateId = "state_id";
        private const string kTitle = "title";
        private const string kTaskNumber = "task_number";
        private const string kAdditionalInfo = "additional_info";
        private const string kAscending = "asc";
        private const string kDescending = "desc";
        private const string kWildcard = "%";
        private const string kQuote = "\"";
        private const string kFlowBundleIdKey = "\"FlowBundleId\"";

        private static readonly Dictionary<string, object> kNoMatchCondition = new() { ["id"] = new Dictionary<string, object> { ["_is_null"] = true } };
        private static readonly Dictionary<string, object> kMatchAllCondition = [];
        private static readonly Dictionary<string, object> kDefaultOrder = new() { ["id"] = kDescending };

        /// <summary>
        /// Builds the query variables for one page.
        /// </summary>
        /// <param name="request">State of the table: search, column filters, sort and page.</param>
        /// <param name="taskTypes">Task types selected in the page dropdown.</param>
        /// <param name="stateIds">State ids selected in the page dropdown (all states if nothing is selected).</param>
        /// <param name="stateNames">Names of all states, used to resolve the state column filter.</param>
        /// <returns>The where, order_by, limit and offset variables. Limit is the page size plus one to detect a next page.</returns>
        public static WorkflowTicketQuery Build(FancyItemsRequest request, IReadOnlyCollection<WfTaskType> taskTypes,
            IReadOnlyCollection<int> stateIds, IReadOnlyDictionary<int, string> stateNames)
        {
            List<object> conditions =
            [
                Nested(kReqTasks, Field("task_type", Comparison(kIn, taskTypes.Select(taskType => taskType.ToString()).ToList()))),
                StateCondition(stateIds)
            ];

            Dictionary<string, object>? searchCondition = SearchCondition(request.SearchText, stateNames);
            if (searchCondition != null)
            {
                conditions.Add(searchCondition);
            }

            foreach (FancyColumnFilter filter in request.ColumnFilters)
            {
                Dictionary<string, object>? filterCondition = FilterCondition(filter, stateNames);
                if (filterCondition != null)
                {
                    conditions.Add(filterCondition);
                }
            }

            int pageSize = request.PageSize > 0 ? request.PageSize : int.MaxValue - 1;
            int offset = request.PageSize > 0 ? (Math.Max(request.Page, 1) - 1) * request.PageSize : 0;
            return new WorkflowTicketQuery(new Dictionary<string, object> { [kAnd] = conditions }, OrderBy(request), pageSize + 1, offset);
        }

        /// <summary>
        /// Escapes the LIKE wildcards and the escape character itself, so user input matches literally.
        /// </summary>
        public static string EscapeLikePattern(string value)
        {
            return value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        }

        /// <summary>
        /// Sorts by a column of the ticket itself. Sorting through a relationship (e.g. the requester) is not offered:
        /// Hasura then builds the details of all tickets before applying the limit, which is as slow as loading everything.
        /// </summary>
        private static List<Dictionary<string, object>> OrderBy(FancyItemsRequest request)
        {
            string direction = request.SortAscending ? kAscending : kDescending;
            Dictionary<string, object>? order = request.SortColumnKey switch
            {
                "id" => new() { ["id"] = direction },
                "name" => new() { [kTitle] = direction },
                "state" => new() { [kStateId] = direction },
                "start_time" => new() { ["date_created"] = direction },
                "end_time" => new() { ["date_completed"] = direction },
                _ => null
            };

            if (order == null)
            {
                return [kDefaultOrder];
            }

            return request.SortColumnKey == "id" ? [order] : [order, kDefaultOrder];
        }

        private static Dictionary<string, object>? SearchCondition(string searchText, IReadOnlyDictionary<int, string> stateNames)
        {
            string search = searchText.Trim();
            if (search.Length == 0)
            {
                return null;
            }

            Dictionary<string, object> contains = Comparison(kIlike, kWildcard + EscapeLikePattern(search) + kWildcard);
            List<object> alternatives = [NameCondition(contains), RequesterCondition(contains), BundleCondition(contains)];
            List<int> matchingStateIds = MatchingStateIds(FancyColumnFilterOperator.Contains, search, stateNames);
            if (matchingStateIds.Count > 0)
            {
                alternatives.Add(StateCondition(matchingStateIds));
            }
            if (long.TryParse(search, NumberStyles.Integer, CultureInfo.InvariantCulture, out long number))
            {
                alternatives.Add(Field("id", Comparison(kEq, number)));
                alternatives.Add(TaskNumberCondition(number));
            }

            return new Dictionary<string, object> { [kOr] = alternatives };
        }

        private static Dictionary<string, object>? FilterCondition(FancyColumnFilter filter, IReadOnlyDictionary<int, string> stateNames)
        {
            FancyColumnFilterOperator positiveOperator = PositiveOperator(filter.Operator);
            string value = filter.Value?.Trim() ?? "";
            Dictionary<string, object>? positiveCondition = filter.Key switch
            {
                "name" => NameCondition(TextComparison(positiveOperator, value)),
                "requester" => RequesterCondition(TextComparison(positiveOperator, value)),
                "flow_bundle_id" => BundleCondition(BundleComparison(positiveOperator, value)),
                "state" => StateCondition(MatchingStateIds(positiveOperator, value, stateNames)),
                "id" => NumberCondition(positiveOperator, value, number => Field("id", Comparison(kEq, number))),
                "task_number" => NumberCondition(positiveOperator, value, TaskNumberCondition),
                _ => null
            };

            if (positiveCondition == null)
            {
                return null;
            }

            return positiveOperator == filter.Operator ? positiveCondition : new Dictionary<string, object> { [kNot] = positiveCondition };
        }

        private static FancyColumnFilterOperator PositiveOperator(FancyColumnFilterOperator filterOperator)
        {
            return filterOperator switch
            {
                FancyColumnFilterOperator.NotEquals => FancyColumnFilterOperator.Equals,
                FancyColumnFilterOperator.NotContains => FancyColumnFilterOperator.Contains,
                FancyColumnFilterOperator.IsEmpty => FancyColumnFilterOperator.IsNotEmpty,
                _ => filterOperator
            };
        }

        private static Dictionary<string, object> TextComparison(FancyColumnFilterOperator positiveOperator, string value)
        {
            string escaped = EscapeLikePattern(value);
            return positiveOperator switch
            {
                FancyColumnFilterOperator.Equals => Comparison(kIlike, escaped),
                FancyColumnFilterOperator.StartsWith => Comparison(kIlike, escaped + kWildcard),
                FancyColumnFilterOperator.EndsWith => Comparison(kIlike, kWildcard + escaped),
                FancyColumnFilterOperator.IsNotEmpty => new Dictionary<string, object> { ["_is_null"] = false, ["_neq"] = "" },
                _ => Comparison(kIlike, kWildcard + escaped + kWildcard)
            };
        }

        /// <summary>
        /// The flow bundle id is stored inside the JSON string of the request task's additional info, so its value
        /// is matched between the JSON quotes.
        /// </summary>
        private static Dictionary<string, object> BundleComparison(FancyColumnFilterOperator positiveOperator, string value)
        {
            string escaped = EscapeLikePattern(value);
            string pattern = positiveOperator switch
            {
                FancyColumnFilterOperator.Equals => kWildcard + kQuote + escaped + kQuote + kWildcard,
                FancyColumnFilterOperator.StartsWith => kWildcard + kQuote + escaped + kWildcard,
                FancyColumnFilterOperator.EndsWith => kWildcard + escaped + kQuote + kWildcard,
                FancyColumnFilterOperator.IsNotEmpty => kWildcard + kFlowBundleIdKey + kWildcard,
                _ => kWildcard + escaped + kWildcard
            };
            return Comparison(kIlike, pattern);
        }

        private static List<int> MatchingStateIds(FancyColumnFilterOperator positiveOperator, string value, IReadOnlyDictionary<int, string> stateNames)
        {
            return [.. stateNames.Where(state => StateNameMatches(positiveOperator, state.Value, value)).Select(state => state.Key).Order()];
        }

        private static bool StateNameMatches(FancyColumnFilterOperator positiveOperator, string stateName, string value)
        {
            return positiveOperator switch
            {
                FancyColumnFilterOperator.Equals => stateName.Equals(value, StringComparison.OrdinalIgnoreCase),
                FancyColumnFilterOperator.StartsWith => stateName.StartsWith(value, StringComparison.OrdinalIgnoreCase),
                FancyColumnFilterOperator.EndsWith => stateName.EndsWith(value, StringComparison.OrdinalIgnoreCase),
                FancyColumnFilterOperator.IsNotEmpty => stateName.Length > 0,
                _ => stateName.Contains(value, StringComparison.OrdinalIgnoreCase)
            };
        }

        /// <summary>
        /// Numeric columns only compare for equality; non-numeric input matches no ticket, "is not empty" matches every ticket.
        /// </summary>
        private static Dictionary<string, object> NumberCondition(FancyColumnFilterOperator positiveOperator, string value,
            Func<long, Dictionary<string, object>> equalsCondition)
        {
            if (positiveOperator == FancyColumnFilterOperator.IsNotEmpty)
            {
                return kMatchAllCondition;
            }

            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long number) ? equalsCondition(number) : kNoMatchCondition;
        }

        private static Dictionary<string, object> StateCondition(IReadOnlyCollection<int> stateIds)
        {
            Dictionary<string, object> stateIn = Field(kStateId, Comparison(kIn, stateIds.ToList()));
            List<object> alternatives =
            [
                stateIn,
                Nested(kReqTasks, stateIn),
                Nested(kReqTasks, Nested(kImplTasks, stateIn)),
                Nested(kReqTasks, Nested(kApprovals, stateIn))
            ];
            return new Dictionary<string, object> { [kOr] = alternatives };
        }

        private static Dictionary<string, object> NameCondition(Dictionary<string, object> comparison)
        {
            List<object> alternatives =
            [
                Field(kTitle, comparison),
                Nested(kReqTasks, Field(kTitle, comparison)),
                Nested(kReqTasks, Nested(kImplTasks, Field(kTitle, comparison))),
                Nested(kReqTasks, Nested(kApprovals, Field("approver_group", comparison)))
            ];
            return new Dictionary<string, object> { [kOr] = alternatives };
        }

        private static Dictionary<string, object> RequesterCondition(Dictionary<string, object> comparison)
        {
            return Nested("uiuser", Field("uiuser_username", comparison));
        }

        private static Dictionary<string, object> BundleCondition(Dictionary<string, object> comparison)
        {
            return Nested(kReqTasks, Field(kAdditionalInfo, comparison));
        }

        private static Dictionary<string, object> TaskNumberCondition(long taskNumber)
        {
            Dictionary<string, object> taskNumberEquals = Field(kTaskNumber, Comparison(kEq, taskNumber));
            List<object> alternatives = [Nested(kReqTasks, taskNumberEquals), Nested(kReqTasks, Nested(kImplTasks, taskNumberEquals))];
            return new Dictionary<string, object> { [kOr] = alternatives };
        }

        private static Dictionary<string, object> Comparison(string comparisonOperator, object value)
        {
            return new Dictionary<string, object> { [comparisonOperator] = value };
        }

        private static Dictionary<string, object> Field(string fieldName, Dictionary<string, object> comparison)
        {
            return new Dictionary<string, object> { [fieldName] = comparison };
        }

        private static Dictionary<string, object> Nested(string relationship, Dictionary<string, object> condition)
        {
            return new Dictionary<string, object> { [relationship] = condition };
        }
    }
}
