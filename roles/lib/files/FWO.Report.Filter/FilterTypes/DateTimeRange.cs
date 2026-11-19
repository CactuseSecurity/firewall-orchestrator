using FWO.Report.Filter.Ast;
using FWO.Report.Filter.Exceptions;

namespace FWO.Report.Filter.FilterTypes
{
    class DateTimeRange
    {
        public readonly DateTime? Start;
        public readonly DateTime? End;

        public DateTimeRange(AstNodeFilterDateTimeRange filter)
        {
            bool isSingleDate = DateTime.TryParse(filter.Value.Text, out DateTime time);
            DateTime startOfCurrentYear = new(DateTime.Now.Year, 1, 1, 0, 0, 0, DateTimeKind.Local);
            DateTime startOfCurrentMonth = new(DateTime.Now.Year, DateTime.Now.Month, 1, 0, 0, 0, DateTimeKind.Local);

            switch (filter.Operator.Kind)
            {
                case TokenKind.EEQ:
                case TokenKind.EQ:
                    switch (filter.Value.Text)
                    {
                        // todo: add today, yesterday, this week, last week
                        case "now":
                            DateTime now = DateTime.Now;
                            Start = now;
                            End = now;
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
