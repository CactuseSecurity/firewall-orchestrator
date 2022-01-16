using FWO.Report.Filter.Ast;
using FWO.Report.Filter.Exceptions;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FWO.Report.Filter
{
    [TypeConverter(typeof(ReportTypeTypeConverter))]
    public enum ReportType
    {
        None,
        Rules,
        Changes,
        Statistics,
        NatRules
    }

    public class ReportTypeTypeConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext? context, Type sourceType)
        {
            return sourceType == typeof(AstNodeFilter<string>) || base.CanConvertFrom(context, sourceType);
        }

        public override object? ConvertFrom(ITypeDescriptorContext? context, CultureInfo? culture, object value)
        {
            AstNodeFilter<string>? reportTypeAstNode = value as AstNodeFilter<string>;

            if (reportTypeAstNode != null)
            {
                return reportTypeAstNode.Value.Text switch
                {
                    "rules" or "rule" => ReportType.Rules,
                    "statistics" or "statistic" => ReportType.Statistics,
                    "changes" or "change" => ReportType.Changes,
                    "natrules" or "nat_rules" => ReportType.NatRules,
                    _ => throw new SemanticException($"Unexpected report type found", reportTypeAstNode.Value.Position)
                };
            }
            else 
                return base.ConvertFrom(context, culture, value);
        }
    }
}
