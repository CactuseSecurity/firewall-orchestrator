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
    [TypeConverter(typeof(DateTimeRangeTypeConverter))]
    class DateTimeRange
    {
        public readonly DateTime? Start;
        public readonly DateTime? End;

        public DateTimeRange(AstNodeFilter<DateTimeRange> filter)
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
                                throw new SyntaxException($"Error: wrong time range format.", filter.Value.Position); // Unexpected token
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
                    throw new SemanticException("", filter.Operator.Position);
            }
        }
    }

    public class DateTimeRangeTypeConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            string? casted = value as string;
            return casted != null
                ? new DateTimeRange(casted)
                : base.ConvertFrom(context, culture, value);
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            var casted = value as CrazyClass;
            return destinationType == typeof(string) && casted != null
                ? String.Join("", casted.Charray)
                : base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
