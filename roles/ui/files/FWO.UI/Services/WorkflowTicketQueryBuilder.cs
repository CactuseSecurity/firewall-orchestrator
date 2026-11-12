using FWO.Data.Workflow;
using System.Globalization;

namespace FWO.Ui.Services
{
    /// <summary>
    /// Variables of the paged workflow ticket query: Hasura where and order_by expressions, limit and offset.
    /// </summary>
    public sealed record WorkflowTicketQuery(Dictionary<string, object> Where, List<Dictionary<string, object>> OrderBy, int Limit, int Offset);

    /// <summary>
    /// Translates the state of the workflow monitoring tree table (column filters, sort, page) and the
    /// page's task type and state dropdowns into the variables of the paged ticket query.
    /// The state and id columns compare ids, so the database can answer them quickly; the id column only supports
    /// "equals" and "does not equal", other operators on it are ignored. The requester column
    /// compares the user name of the ticket's requester only, a lookup by primary key in the small user table.
    /// The name column compares the titles (the approver group of approvals) with "equals" and "starts with" only;
    /// the start and end time columns compare the day of the dates with the date operators.
    /// Free text search and "contains" filters over all levels are not offered, as they are too slow on many tickets.
    /// A filter matches if any level of a ticket (ticket, request task, implementation task, approval)
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
        private const string kId = "id";
        private const string kStateId = "state_id";
        private const string kTitle = "title";
        private const string kAscending = "asc";
        private const string kDescending = "desc";
        private const string kWildcard = "%";
        private const string kGte = "_gte";
        private const string kLt = "_lt";
        private const string kDayFormat = "yyyy-MM-dd";
        private const string kAfterLastDay = "infinity";

        private static readonly LevelFields kNameFields = new(kTitle, kTitle, kTitle, "approver_group");
        private static readonly LevelFields kStartFields = new("date_created", "start", "start", "date_opened");
        private static readonly LevelFields kEndFields = new("date_completed", "stop", "stop", "approval_date");

        private static readonly Dictionary<string, object> kNoMatchCondition = new() { ["id"] = new Dictionary<string, object> { ["_is_null"] = true } };
        private static readonly Dictionary<string, object> kDefaultOrder = new() { ["id"] = kDescending };

        /// <summary>
        /// Builds the query variables for one page.
        /// </summary>
        /// <param name="request">State of the table: column filters, sort and page. The search text is ignored.</param>
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

        private static Dictionary<string, object>? FilterCondition(FancyColumnFilter filter, IReadOnlyDictionary<int, string> stateNames)
        {
            FancyColumnFilterOperator positiveOperator = PositiveOperator(filter.Operator);
            string value = filter.Value?.Trim() ?? "";
            Dictionary<string, object>? positiveCondition = filter.Key switch
            {
                "state" => StateCondition(MatchingStateIds(positiveOperator, value, stateNames)),
                "id" => IdFilterCondition(positiveOperator, value),
                "requester" => RequesterCondition(TextComparison(positiveOperator, value)),
                "name" => NameCondition(positiveOperator, value),
                "start_time" => DateCondition(kStartFields, positiveOperator, value, filter.ValueTo),
                "end_time" => DateCondition(kEndFields, positiveOperator, value, filter.ValueTo),
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

        /// <summary>
        /// Escapes the LIKE wildcards and the escape character itself, so user input matches literally.
        /// </summary>
        public static string EscapeLikePattern(string value)
        {
            return value.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_");
        }

        /// <summary>
        /// Case-insensitive text comparison for the operators of a text column filter.
        /// </summary>
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
        /// Resolves a state filter on the state names to the matching state ids.
        /// </summary>
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
        /// The id filter only compares for equality, so the id column offers "equals" and "does not equal" only.
        /// Other operators are ignored like by the table itself instead of being reinterpreted; non-numeric input matches no ticket.
        /// </summary>
        private static Dictionary<string, object>? IdFilterCondition(FancyColumnFilterOperator positiveOperator, string value)
        {
            if (positiveOperator != FancyColumnFilterOperator.Equals)
            {
                return null;
            }

            return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out long id) ? IdCondition(id) : kNoMatchCondition;
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

        /// <summary>
        /// The id column shows the id of the ticket, request task, implementation task or approval of a row,
        /// so an id matches on any of these levels.
        /// </summary>
        private static Dictionary<string, object> IdCondition(long id)
        {
            Dictionary<string, object> idEquals = Field(kId, Comparison(kEq, id));
            List<object> alternatives =
            [
                idEquals,
                Nested(kReqTasks, idEquals),
                Nested(kReqTasks, Nested(kImplTasks, idEquals)),
                Nested(kReqTasks, Nested(kApprovals, idEquals))
            ];
            return new Dictionary<string, object> { [kOr] = alternatives };
        }

        /// <summary>
        /// The name column shows the title of the ticket, request task or implementation task and the approver group of an
        /// approval. Only "equals" and "starts with" are supported, other operators are ignored like by the table itself.
        /// </summary>
        private static Dictionary<string, object>? NameCondition(FancyColumnFilterOperator positiveOperator, string value)
        {
            if (positiveOperator != FancyColumnFilterOperator.Equals && positiveOperator != FancyColumnFilterOperator.StartsWith)
            {
                return null;
            }

            return AnyLevelCondition(kNameFields, TextComparison(positiveOperator, value));
        }

        /// <summary>
        /// Compares the day of a date on all levels. The table sends the days as invariant "yyyy-MM-dd" strings; filters
        /// with another format or an unsupported operator are ignored like by the table itself.
        /// </summary>
        private static Dictionary<string, object>? DateCondition(LevelFields fields, FancyColumnFilterOperator positiveOperator, string value, string? valueTo)
        {
            Dictionary<string, object>? comparison = positiveOperator == FancyColumnFilterOperator.IsNotEmpty
                ? new Dictionary<string, object> { ["_is_null"] = false }
                : DayComparison(positiveOperator, value, valueTo);
            return comparison == null ? null : AnyLevelCondition(fields, comparison);
        }

        /// <summary>
        /// Translates a date operator into a range of timestamps: a day reaches from its start up to the start of the next day.
        /// </summary>
        private static Dictionary<string, object>? DayComparison(FancyColumnFilterOperator positiveOperator, string value, string? valueTo)
        {
            if (!TryParseDay(value, out DateOnly day))
            {
                return null;
            }

            return positiveOperator switch
            {
                FancyColumnFilterOperator.Equals => DayRange(day, day),
                FancyColumnFilterOperator.GreaterThan => Comparison(kGte, StartOfNextDay(day)),
                FancyColumnFilterOperator.GreaterThanOrEqual => Comparison(kGte, StartOfDay(day)),
                FancyColumnFilterOperator.LessThan => Comparison(kLt, StartOfDay(day)),
                FancyColumnFilterOperator.LessThanOrEqual => Comparison(kLt, StartOfNextDay(day)),
                FancyColumnFilterOperator.Between when TryParseDay(valueTo, out DateOnly dayTo) => day <= dayTo ? DayRange(day, dayTo) : DayRange(dayTo, day),
                _ => null
            };
        }

        private static Dictionary<string, object> DayRange(DateOnly firstDay, DateOnly lastDay)
        {
            return new Dictionary<string, object> { [kGte] = StartOfDay(firstDay), [kLt] = StartOfNextDay(lastDay) };
        }

        private static bool TryParseDay(string? value, out DateOnly day)
        {
            return DateOnly.TryParseExact(value?.Trim(), kDayFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out day);
        }

        private static string StartOfDay(DateOnly day)
        {
            return day.ToString(kDayFormat, CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// The last representable day has no next day, so PostgreSQL's "infinity" timestamp ends it.
        /// </summary>
        private static string StartOfNextDay(DateOnly day)
        {
            return day == DateOnly.MaxValue ? kAfterLastDay : StartOfDay(day.AddDays(1));
        }

        /// <summary>
        /// Matches if the field of the ticket, of a request task, of an implementation task or of an approval matches.
        /// </summary>
        private static Dictionary<string, object> AnyLevelCondition(LevelFields fields, Dictionary<string, object> comparison)
        {
            List<object> alternatives =
            [
                Field(fields.Ticket, comparison),
                Nested(kReqTasks, Field(fields.ReqTask, comparison)),
                Nested(kReqTasks, Nested(kImplTasks, Field(fields.ImplTask, comparison))),
                Nested(kReqTasks, Nested(kApprovals, Field(fields.Approval, comparison)))
            ];
            return new Dictionary<string, object> { [kOr] = alternatives };
        }

        /// <summary>
        /// The requester exists only on the ticket, so the filter compares the user name of the ticket's requester.
        /// </summary>
        private static Dictionary<string, object> RequesterCondition(Dictionary<string, object> comparison)
        {
            return Nested("uiuser", Field("uiuser_username", comparison));
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

        /// <summary>
        /// Names of the field a column compares on each level of a ticket.
        /// </summary>
        private sealed record LevelFields(string Ticket, string ReqTask, string ImplTask, string Approval);
    }
}
