using FWO.Api.Data;
using FWO.ApiClient;
using FWO.ApiClient.Queries;
using FWO.Logging;
using FWO.Middleware.Server.Requests;
using FWO.Report;
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;

namespace FWO.Middleware.Server
{
    public class ReportScheduler
    {
        private readonly object scheduledReportsLock = new object();
        private List<ScheduledReport> scheduledReports = new List<ScheduledReport>();
        private readonly TimeSpan CheckScheduleInterval = TimeSpan.FromMinutes(1);

        private readonly string apiServerUri;
        private readonly APIConnection apiConnection;
        private readonly ApiSubscription<ScheduledReport[]> scheduledReportsSubscription;
        private readonly JwtWriter jwtWriter;

        private readonly object ldapLock = new object();
        private List<Ldap> connectedLdaps;

        public ReportScheduler(APIConnection apiConnection, JwtWriter jwtWriter, ApiSubscription<List<Ldap>> connectedLdapsSubscription)
        {
            this.jwtWriter = jwtWriter;            
            this.apiConnection = apiConnection;
            apiServerUri = apiConnection.APIServerURI;

            connectedLdaps = apiConnection.SendQueryAsync<List<Ldap>>(AuthQueries.getLdapConnections).Result;
            connectedLdapsSubscription.OnUpdate += OnLdapUpdate;

            //scheduledReports = apiConnection.SendQueryAsync<ScheduledReport[]>(ReportQueries.getReportSchedules).Result.ToList();
            scheduledReportsSubscription = apiConnection.GetSubscription<ScheduledReport[]>(ApiExceptionHandler, ReportQueries.subscribeReportScheduleChanges);
            scheduledReportsSubscription.OnUpdate += OnScheduleUpdate;

            Timer checkScheduleTimer = new Timer();
            checkScheduleTimer.Elapsed += CheckSchedule;
            checkScheduleTimer.Interval = CheckScheduleInterval.TotalMilliseconds;
            checkScheduleTimer.AutoReset = true;
            checkScheduleTimer.Start();
        }

        private void OnLdapUpdate(List<Ldap> connectedLdaps)
        {
            lock(ldapLock)
            {
                this.connectedLdaps = connectedLdaps;
            }
        }

        private void OnScheduleUpdate(ScheduledReport[] scheduledReports)
        {
            lock (scheduledReportsLock)
            {
                this.scheduledReports = scheduledReports.ToList();
            }
        }

        private void ApiExceptionHandler(Exception exception)
        {
            Log.WriteError("Report scheduler", "Api subscription lead to exception. Retry subscription.", exception);
            // Subscription will be restored if no exception is thrown here
        }

        private async void CheckSchedule(object _, ElapsedEventArgs __)
        {
            List<Task> reportGeneratorTasks = new List<Task>();

            DateTime dateTimeNowRounded = RoundUp(DateTime.Now, CheckScheduleInterval);

            lock (scheduledReports)
            {
                foreach (ScheduledReport scheduledReport in scheduledReports)
                {
                    try
                    {
                        if (scheduledReport.Active)
                        {
                            // Add schedule interval as long as schedule time is smaller then current time 
                            while (RoundUp(scheduledReport.StartTime, CheckScheduleInterval) < dateTimeNowRounded)
                            {
                                scheduledReport.StartTime = scheduledReport.RepeatInterval switch
                                {
                                    Interval.Days => scheduledReport.StartTime.AddDays(scheduledReport.RepeatOffset),
                                    Interval.Weeks => scheduledReport.StartTime.AddDays(scheduledReport.RepeatOffset * 7),
                                    Interval.Months => scheduledReport.StartTime.AddMonths(scheduledReport.RepeatOffset),
                                    Interval.Years => scheduledReport.StartTime.AddYears(scheduledReport.RepeatOffset),
                                    _ => throw new NotSupportedException("Time interval is not supported.")
                                };
                            }

                            if (RoundUp(scheduledReport.StartTime, CheckScheduleInterval) == dateTimeNowRounded)
                            {
                                reportGeneratorTasks.Add(GenerateReport(scheduledReport, dateTimeNowRounded));
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        Log.WriteError("Report Scheduling", "Checking scheduled reports lead to exception.", exception);
                    }
                }
            }

            await Task.WhenAll(reportGeneratorTasks);
        }

        private Task GenerateReport(ScheduledReport report, DateTime dateTimeNowRounded)
        {
            return Task.Run(async () =>
            {
                try
                {
                    DateTime reportGenerationStartDate = DateTime.Now;

                    // get uiuser roles + tenant
                    AuthenticationRequestHandler authHandler = new AuthenticationRequestHandler(connectedLdaps, jwtWriter, apiConnection);
                    report.Owner.Roles = await authHandler.GetRoles(report.Owner);
                    report.Owner.Tenant = await authHandler.GetTenantAsync(report.Owner);
                    APIConnection apiConnectionUserContext = new APIConnection(apiServerUri, await jwtWriter.CreateJWT(report.Owner));

                    ReportBase reportRules = ReportBase.ConstructReport(report.Template.Filter);
                    await reportRules.Generate
                    (
                        int.MaxValue,
                        report.Template.Filter,
                        apiConnectionUserContext, 
                        _ => Task.CompletedTask
                    );

                    ////$report_name: String!
                    ////$report_start_time: timestamp!
                    ////$report_generation_time: timestamp!
                    ////$report_owner_id: Int!
                    ////$report_template_id: Int!
                    //$report_pdf: bytea
                    //$report_csv: String
                    //$report_html: String
                    //$report_json: json

                    string reportCsv = null;
                    string reportHtml = null;
                    string reportJson = null;
                    byte[] reportPdf = null;

                    foreach (FileFormat format in report.OutputFormat)
                    {
                        switch (format.Name)
                        {
                            case "csv":
                                reportCsv = reportRules.ToCsv();
                                break;

                            case "html":
                                reportHtml = reportRules.ToHtml();
                                break;

                            case "pdf":
                                reportPdf = reportRules.ToPdf();
                                break;

                            case "json":
                                reportJson = reportRules.ToJson();
                                break;

                            default:
                                throw new NotSupportedException("Output format is not supported.");
                        }
                    }

                    var queryVariables = new
                    {
                        report_name = $"{report.Name}_{dateTimeNowRounded.ToShortDateString()}",
                        report_start_time = reportGenerationStartDate,
                        report_end_time = DateTime.Now,
                        report_owner_id = report.Owner.DbId,
                        report_template_id = report.Template.Id,
                        report_pdf = reportPdf,
                        report_csv = reportCsv,
                        report_html = reportHtml,
                        report_json = reportJson,
                    };


                    await apiConnectionUserContext.SendQueryAsync<object>(ReportQueries.addGeneratedReport, queryVariables);
                }
                catch (Exception exception)
                {
                    Log.WriteError("Report Scheduling", $"Generating scheduled report \"{report.Name}\" lead to exception.", exception);
                }
            });
        }

        private static DateTime RoundUp(DateTime dateTime, TimeSpan roundInterval)
        {
            return new DateTime((dateTime.Ticks + roundInterval.Ticks - 1) / roundInterval.Ticks * roundInterval.Ticks, dateTime.Kind);
        }

        class ReportData
        {
            public string Csv { get; set; }

            public string Html { get; set; }

            public byte[] Pdf { get; set; }

            public string Json { get; set; }
        }
    }
}
