using FWO.Report.Filter;
using FWO.Config.Api;
using FWO.Basics;
using FWO.Data.Report;
using FWO.Ui.Display;
using System.Text.Json;
using System.Text;

namespace FWO.Report
{
    public abstract class ReportOwnersBase : ReportBase
    {
        private static readonly JsonSerializerOptions kIndentedJsonSerializerOptions = new()
        {
            WriteIndented = true
        };

        protected ReportOwnersBase(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType)
        { }

        public override string ExportToJson()
        {
            return JsonSerializer.Serialize(GetDisplayedOwnerData(), kIndentedJsonSerializerOptions);
        }

        public override string ExportToCsv()
        {
            throw new NotImplementedException();
        }

        public override string SetDescription()
        {
            return $"{GetDisplayedOwnerData().Count} {userConfig.GetText("owners")}";
        }

        protected string GenerateHtmlFrame(string title, string filter, DateTime date, StringBuilder htmlReport)
        {
            string? ownerFilter = ReportType.IsOwnerReport()
                ? null
                : string.Join("; ", GetDisplayedOwnerData().ConvertAll(o => GetOwnerDisplayName(o)));
            return GenerateHtmlFrameBase(title, filter, date, htmlReport, new HtmlFrameOptions
            {
                OwnerFilter = ownerFilter
            });
        }

        protected List<OwnerConnectionReport> GetDisplayedOwnerData()
        {
            AddInfoFilter effectiveOwnerAddInfoFilter = GetEffectiveOwnerAddInfoFilter();
            return [.. ReportData.OwnerData.Where(owner => OwnerRecertDisplay.MatchesAdditionalInfoFilter(owner.Owner, effectiveOwnerAddInfoFilter))];
        }

        protected AddInfoFilter GetEffectiveOwnerAddInfoFilter()
        {
            if (!string.IsNullOrWhiteSpace(ReportData.OwnerAddInfoFilter.Name))
            {
                return new AddInfoFilter(ReportData.OwnerAddInfoFilter);
            }

            if (!string.IsNullOrWhiteSpace(ReportData.OwnerAdditionalInfoKey))
            {
                return new AddInfoFilter
                {
                    Name = ReportData.OwnerAdditionalInfoKey,
                    Mode = AddInfoFilterMode.display_only
                };
            }

            return new AddInfoFilter();
        }

        private static string GetOwnerDisplayName(OwnerConnectionReport ownerReport)
        {
            if (!string.IsNullOrWhiteSpace(ownerReport.Name))
            {
                return ownerReport.Name;
            }

            return ownerReport.Owner.Name;
        }
    }
}
