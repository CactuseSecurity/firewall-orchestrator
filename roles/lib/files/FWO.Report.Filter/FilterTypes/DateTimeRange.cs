using FWO.Report.Filter.Ast;
using FWO.Report.Filter.Exceptions;

namespace FWO.Report.Filter.FilterTypes
{
    class DateTimeRange
    {
        public readonly DateTime? Start;
        public readonly DateTime? End;

        /// <summary>
        /// Resolves the date filter relative to the current local time.
        /// </summary>
        /// <param name="filter">The date filter node holding operator and value.</param>
        public DateTimeRange(AstNodeFilterDateTimeRange filter) : this(filter, DateTime.Now)
        { }

        /// <summary>
        /// Resolves the date filter relative to the given reference time: "now", "this month", "last year" etc.
        /// refer to the time, month and year of <paramref name="referenceTime"/>.
        /// </summary>
        /// <param name="filter">The date filter node holding operator and value.</param>
        /// <param name="referenceTime">The point in time the relative values are resolved against.</param>
        public DateTimeRange(AstNodeFilterDateTimeRange filter, DateTime referenceTime)
        {
            bool isSingleDate = DateTime.TryParse(filter.Value.Text, out DateTime time);
            DateTime startOfCurrentYear = new(referenceTime.Year, 1, 1, 0, 0, 0, referenceTime.Kind);
            DateTime startOfCurrentMonth = new(referenceTime.Year, referenceTime.Month, 1, 0, 0, 0, referenceTime.Kind);

            switch (filter.Operator.Kind)
            {
                case TokenKind.EEQ:
                case TokenKind.EQ:
                    switch (filter.Value.Text)
                    {
                        // todo: add today, yesterday, this week, last week
                        case "now":
                            Start = referenceTime;
                            End = referenceTime;
                            break;
                        case "this year":
                            Start = startOfCurrentYear;
                            End = startOfCurrentYear.AddYears(1);
                            break;
                        case "last year":
                            Start = startOfCurrentYear.AddYears(-1);
                            End = startOfCurrentYear;
                            break;
                        case "this month":
                            Start = startOfCurrentMonth;
                            End = startOfCurrentMonth.AddMonths(1);
                            break;
                        case "last month":
                            Start = startOfCurrentMonth.AddMonths(-1);
                            End = startOfCurrentMonth;
                            break;
                        default:
                            if (isSingleDate)
                            {
                                Start = time;
                                End = time;
                            }
                            else
                            {
                                throw new SyntaxException($"Wrong time range format.", filter.Value.Position); // Unexpected token
                            }
                            break;
                    }
                    break;
                case TokenKind.LSS:
                    End = time;
                    break;
                case TokenKind.GRT:
                    Start = time;
                    break;
                default:
                    throw new SemanticException($"Operator is not appliable for filter {filter.Name.Kind} of type {typeof(DateTimeRange)}", filter.Operator.Position);
            }
        }
    }
}
