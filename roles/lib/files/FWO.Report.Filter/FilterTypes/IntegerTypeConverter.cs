using FWO.Report.Filter.Ast;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FWO.Report.Filter.FilterTypes
{
    public class IntegerTypeConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        {
            return sourceType == typeof(AstNodeFilter<DateTimeRange>) || base.CanConvertFrom(context, sourceType);
        }

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            AstNodeFilter<DateTimeRange>? dateTimeRangeAstNode = value as AstNodeFilter<DateTimeRange>;
            return dateTimeRangeAstNode != null
                ? new DateTimeRange(dateTimeRangeAstNode)
                : base.ConvertFrom(context, culture, value);
        }
    }
}
