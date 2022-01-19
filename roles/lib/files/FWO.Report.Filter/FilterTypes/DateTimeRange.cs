using FWO.Report.Filter.Ast;
using FWO.Report.Filter.Exceptions;
using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FWO.Report.Filter
{
    class DateTimeRange
    {
        public readonly DateTime? Start;
        public readonly DateTime? End;

        public DateTimeRange(AstNodeFilterDateTimeRange filter)
        {
            bool isSingleDate = DateTime.TryParse(filter.Value.Text, out DateTime time);
            int currentYear = DateTime.Now.Year;
            int currentMonth = DateTime.Now.Month;

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
                            Start = new DateTime(currentYear, 01, 01, 00, 00, 00);
                            End = new DateTime(currentYear + 1, 01, 01, 00, 00, 00);
                            break;
                        case "last year":
                            Start = new DateTime(currentYear - 1, 01, 01, 00, 00, 00);
                            End = new DateTime(currentYear, 01, 01, 00, 00, 00);
                            break;
                        case "this month":
                            Start = new DateTime(currentYear, currentMonth, 01, 00, 00, 00);
                            End = new DateTime(currentYear, currentMonth + 1, 01, 00, 00, 00);
                            break;
                        case "last month":
                            Start = new DateTime(currentYear, currentMonth - 1, 01, 00, 00, 00);
                            End = new DateTime(currentYear, currentMonth, 01, 00, 00, 00);
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
