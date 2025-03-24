using FWO.Basics;
using FWO.Data;
using FWO.Data.Modelling;
using FWO.Api.Client;
using FWO.Report.Filter;
using FWO.Config.Api;
using System.Text;
using FWO.Services;

namespace FWO.Report
{
    public class ReportVariances : ReportConnections
    {
        public ReportVariances(DynGraphqlQuery query, UserConfig userConfig, ReportType reportType) : base(query, userConfig, reportType) { }



    }
}
