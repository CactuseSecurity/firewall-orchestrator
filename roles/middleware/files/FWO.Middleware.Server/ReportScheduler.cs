using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Logging;
using FWO.Middleware.Controllers;
using FWO.Report;
using FWO.Report.Filter;
using System.Timers;
using WkHtmlToPdfDotNet;
using FWO.Config.File;

namespace FWO.Middleware.Server
{
    public class ReportScheduler
    {
        private readonly object scheduledReportsLock = new object();
        private List<ScheduledReport> scheduledReports = new List<ScheduledReport>();
        private readonly TimeSpan CheckScheduleInterval = TimeSpan.FromMinutes(1);

        private readonly string apiServerUri;
        private readonly ApiConnection apiConnection;
        private readonly ApiSubscription<ScheduledReport[]> scheduledReportsSubscription;
        private readonly JwtWriter jwtWriter;

        private readonly object ldapLock = new object();
        private List<Ldap> connectedLdaps;

        public ReportScheduler(ApiConnection apiConnection, JwtWriter jwtWriter, ApiSubscription<List<Ldap>> connectedLdapsSubscription)
        {
            this.jwtWriter = jwtWriter;            
            this.apiConnection = apiConnection;
            apiServerUri = ConfigFile.ApiServerUri;

            connectedLdaps = apiConnection.SendQueryAsync<List<Ldap>>(AuthQueries.getLdapConnections).Result;
            connectedLdapsSubscription.OnUpdate += OnLdapUpdate;

            //scheduledReports = apiConnection.SendQueryAsync<ScheduledReport[]>(ReportQueries.getReportSchedules).Result.ToList();
            scheduledReportsSubscription = apiConnection.GetSubscription<ScheduledReport[]>(ApiExceptionHandler, OnScheduleUpdate, ReportQueries.subscribeReportScheduleChanges);

            System.Timers.Timer checkScheduleTimer = new();
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

        private async void CheckSchedule(object? _, ElapsedEventArgs __)
        {
            List<Task> reportGeneratorTasks = new List<Task>();

            DateTime dateTimeNowRounded = RoundDown(DateTime.Now, CheckScheduleInterval);

            lock (scheduledReports)
            {
                foreach (ScheduledReport scheduledReport in scheduledReports)
                {
                    try
                    {
                        if (scheduledReport.Active)
                        {
                            // Add schedule interval as long as schedule time is smaller then current time 
                            while (RoundDown(scheduledReport.StartTime, CheckScheduleInterval) < dateTimeNowRounded)
                            {
                                scheduledReport.StartTime = scheduledReport.RepeatInterval switch
                                {
                                    Interval.Days => scheduledReport.StartTime.AddDays(scheduledReport.RepeatOffset),
                                    Interval.Weeks => scheduledReport.StartTime.AddDays(scheduledReport.RepeatOffset * 7),
                                    Interval.Months => scheduledReport.StartTime.AddMonths(scheduledReport.RepeatOffset),
                                    Interval.Years => scheduledReport.StartTime.AddYears(scheduledReport.RepeatOffset),
                                    Interval.Never => scheduledReport.StartTime.AddYears(42_42),
                                    _ => throw new NotSupportedException("Time interval is not supported.")
                                };
                            }

                            if (RoundDown(scheduledReport.StartTime, CheckScheduleInterval) == dateTimeNowRounded)
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
            CancellationToken token = new CancellationToken();
            return Task.Run(async () =>
            {
                try
                {
                    ReportFile reportFile = new ReportFile
                    { 
                        Name = $"{report.Name}_{dateTimeNowRounded.ToShortDateString()}",
                        GenerationDateStart = DateTime.Now,
                        TemplateId = report.Template.Id,
                        OwnerId = report.Owner.DbId,
                        Type = report.Template.ReportParams.ReportType
                    };

                    DateTime reportGenerationStartDate = DateTime.Now;

                    // get uiuser roles + tenant
                    AuthManager authManager = new AuthManager(jwtWriter, connectedLdaps, apiConnection);
                    //AuthenticationRequestHandler authHandler = new AuthenticationRequestHandler(connectedLdaps, jwtWriter, apiConnection);
                    string jwt = await authManager.AuthorizeUserAsync(report.Owner, validatePassword: false);
                    ApiConnection apiConnectionUserContext = new GraphQlApiConnection(apiServerUri, jwt);
                    GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(jwt);
                    UserConfig userConfig = await UserConfig.ConstructAsync(globalConfig, apiConnection, report.Owner.DbId);

                    await apiConnectionUserContext.SendQueryAsync<object>(ReportQueries.countReportSchedule, new { report_schedule_id = report.Id });

                    if(!report.Template.ReportParams.DeviceFilter.isAnyDeviceFilterSet())
                    {
                        // for scheduling no device selection means "all"
                        report.Template.ReportParams.DeviceFilter.Managements = await apiConnectionUserContext.SendQueryAsync<List<ManagementSelect>>(DeviceQueries.getDevicesByManagements);
                        report.Template.ReportParams.DeviceFilter.applyFullDeviceSelection(true);
                    }

                    ReportBase reportRules = ReportBase.ConstructReport(report.Template.Filter, 
                        report.Template.ReportParams.DeviceFilter,
                        report.Template.ReportParams.TimeFilter, 
                        (report.Template.ReportParams.ReportType != null ? (ReportType)report.Template.ReportParams.ReportType : ReportType.None),
                        userConfig);
                    Management[] managementsReport = Array.Empty<Management>();
                    await reportRules.Generate(int.MaxValue, apiConnectionUserContext, 
                        managementsReportIntermediate =>
                        {
                            managementsReport = managementsReportIntermediate;
                            setRelevantManagements(ref managementsReport, report.Template.ReportParams.DeviceFilter);
                            return Task.CompletedTask;
                        }, token);
                    await reportRules.GetObjectsInReport(int.MaxValue, apiConnectionUserContext, _ => Task.CompletedTask);

                    reportFile.Json = reportRules.ExportToJson();

                    foreach (FileFormat format in report.OutputFormat)
                    {
                        switch (format.Name)
                        {
                            case "csv":
                                reportFile.Csv = reportRules.ExportToCsv();
                                break;

                            case "html":
                                reportFile.Html = reportRules.ExportToHtml();
                                break;

                            case "pdf":
                                reportFile.Pdf = Convert.ToBase64String(reportRules.ToPdf(PaperKind.A4));
                                break;

                            case "json":
                                break;

                            default:
                                throw new NotSupportedException("Output format is not supported.");
                        }
                    }

                    reportFile.GenerationDateEnd = DateTime.Now;

                    var queryVariables = new
                    {
                        report_name = reportFile.Name,
                        report_start_time = reportFile.GenerationDateStart,
                        report_end_time = reportFile.GenerationDateEnd,
                        report_owner_id = reportFile.OwnerId,
                        report_template_id = reportFile.TemplateId,
                        report_pdf = reportFile.Pdf,
                        report_csv = reportFile.Csv,
                        report_html = reportFile.Html,
                        report_json = reportFile.Json,
                        report_type = reportFile.Type,
                        description = reportRules.SetDescription()
                    };

                    await apiConnectionUserContext.SendQueryAsync<object>(ReportQueries.addGeneratedReport, queryVariables);

                    Log.WriteInfo("Report Scheduling", $"Scheduled report \"{report.Name}\" for user \"{report.Owner.Name}\" with id \"{report.Owner.DbId}\" successfully generated.");
                }
                catch (Exception exception)
                {
                    Log.WriteError("Report Scheduling", $"Generating scheduled report \"{report.Name}\" lead to exception.", exception);
                }
            }, token);
        }

        private void setRelevantManagements(ref Management[] managementsReport, DeviceFilter deviceFilter)
        {
            if (deviceFilter.isAnyDeviceFilterSet())
            {
                List<int> relevantManagements = deviceFilter.getSelectedManagements();
                foreach (Management mgm in managementsReport)
                {
                    mgm.Ignore = (relevantManagements.Contains(mgm.Id) ? false : true);
                }
            }
        }

        private static DateTime RoundDown(DateTime dateTime, TimeSpan roundInterval)
        {
            long delta = dateTime.Ticks % roundInterval.Ticks;
            return new DateTime(dateTime.Ticks - delta);
        }
    }
}
