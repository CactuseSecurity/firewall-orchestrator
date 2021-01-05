using FWO.ApiClient;
using FWO.ApiClient.Queries;
using FWO.Api.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FWO.Report
{
    class ReportScheduler
    {
        APIConnection apiConnection;

        public ReportScheduler(APIConnection apiConnection)
        {
            this.apiConnection = apiConnection;
        }

        public async Task<ScheduledReport[]> GetReports()
        {
            return await apiConnection.SendQueryAsync<ScheduledReport[]>(ReportQueries.getReportSchedules);
        }

        public async Task EditReport(ScheduledReport scheduledReport)
        {
            await apiConnection.SendQueryAsync<int>(ReportQueries.getReportSchedules, scheduledReport);
        }

        public async Task AddReport(ScheduledReport scheduledReport)
        {
            await apiConnection.SendQueryAsync<int>(ReportQueries.addReportSchedule, scheduledReport);
        }

        public async Task DeleteReport(ScheduledReport scheduledReport)
        {
            await apiConnection.SendQueryAsync<int>(ReportQueries.deleteReportSchedule, scheduledReport);
        }
    }
}
