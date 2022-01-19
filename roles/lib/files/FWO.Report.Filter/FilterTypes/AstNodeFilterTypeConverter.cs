using FWO.Report.Filter.Ast;
using FWO.Report.Filter.Exceptions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FWO.Report.Filter.FilterTypes
{
    //internal class AstNodeFilterTypeConverter : TypeConverter
    //{
    //    public override bool CanConvertTo(ITypeDescriptorContext? context, Type? destinationType)
    //    {
    //        return base.CanConvertTo(context, destinationType) || destinationType == typeof(string) || destinationType == typeof(int) ||
    //            destinationType == typeof(bool) || destinationType == typeof(DateTimeRange) || destinationType == typeof(ReportType);
    //    }

    //    public override object? ConvertTo(ITypeDescriptorContext? context, CultureInfo? culture, object? value, Type destinationType)
    //    {
    //        if (value == null)
    //        {
    //            throw new ArgumentNullException(nameof(value));
    //        }

    //        // value.GetType() == typeof(AstNodeFilter<destinationType>)
    //        if (value.GetType().GetGenericArguments()[0] != destinationType)
    //        {
    //            throw new ArgumentException($"The conversion constraint value.GetType() == typeof(AstNodeFilter<destinationType>) is violated. \n value.GetType() == {value.GetType()}");
    //        }

    //        return value switch
    //        {
    //            AstNodeFilter<string> stringFilter => ConvertToString(stringFilter),
    //            AstNodeFilter<int> intFilter => ConvertToInt(intFilter),
    //            AstNodeFilter<bool> boolFilter => ConvertToBool(boolFilter),
    //            AstNodeFilter<DateTimeRange> dateTimeRangeFilter => ConvertToDateTimeRange(dateTimeRangeFilter),
    //            AstNodeFilter<ReportType> reportTypeFilter => ConvertToReportType(reportTypeFilter),
    //            _ => base.ConvertTo(context, culture, value, destinationType),
    //        };
    //    }

    //    private string? ConvertToString(AstNodeFilter<string> filter)
    //    {

    //    }

    //    private int? ConvertToInt(AstNodeFilter<int> filter)
    //    {

    //    }

    //    private bool? ConvertToBool(AstNodeFilter<bool> filter)
    //    {

    //    }

    //    private DateTimeRange? ConvertToDateTimeRange(AstNodeFilter<DateTimeRange> filter)
    //    {

    //    }

    //    private ReportType? ConvertToReportType(AstNodeFilter<ReportType> filter)
    //    {
    //    }

    //    private void CheckOperator(Token isOperator, bool equalsIsExactEquals, params TokenKind[] expectedOperators)
    //    {
    //        if (equalsIsExactEquals)
    //        {
    //            isOperator.EqualsIsExactEquals();
    //        }
    //        if (!expectedOperators.Contains(isOperator.Kind))
    //        {
    //            throw new SemanticException($"Expected one of the following tokens: {string.Join(", ", expectedOperators)} Got: {isOperator.Kind}", isOperator.Position);
    //        }
    //    }
    //}
}
