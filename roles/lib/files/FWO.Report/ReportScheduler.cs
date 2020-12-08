using FWO.ApiClient;
using FWO.ApiClient.Queries;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FWO.Report
{
    class ReportScheduler
    {

        public ReportScheduler(APIConnection apiConnection)
        {
            //apiConnection.SendQueryAsync<>(ReportQueries.getReportSchedules);
        }

        public void GetReports()
        {

        }

        public void EditReport()
        {

        }

        public void AddReport(string filter)
        {
            //ReportQueries.addReportSchedule
        }

        public void DeleteReport()
        {
            //ReportQueries.dele
        }
    }
}
