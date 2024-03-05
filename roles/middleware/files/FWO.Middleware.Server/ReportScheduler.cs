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
    /// <summary>
    /// Report scheduler class
    /// </summary>
    public class ReportScheduler
    {
        private readonly object scheduledReportsLock = new object();
        private List<ReportSchedule> scheduledReports = new List<ReportSchedule>();
        private readonly TimeSpan CheckScheduleInterval = TimeSpan.FromMinutes(1);

        private readonly string apiServerUri;
        private readonly ApiConnection apiConnection;
        private readonly GraphQlApiSubscription<ReportSchedule[]> scheduledReportsSubscription;
        private readonly JwtWriter jwtWriter;

        private readonly object ldapLock = new object();
        private List<Ldap> connectedLdaps;

		/// <summary>
		/// Constructor needing connection, jwtWriter and subscription to connected ldaps
		/// </summary>
        public ReportScheduler(ApiConnection apiConnection, JwtWriter jwtWriter, GraphQlApiSubscription<List<Ldap>> connectedLdapsSubscription)
        {
            this.jwtWriter = jwtWriter;            
            this.apiConnection = apiConnection;
            apiServerUri = ConfigFile.ApiServerUri;

            connectedLdaps = apiConnection.SendQueryAsync<List<Ldap>>(AuthQueries.getLdapConnections).Result;
            connectedLdapsSubscription.OnUpdate += OnLdapUpdate;

            //scheduledReports = apiConnection.SendQueryAsync<ReportSchedule[]>(ReportQueries.getReportSchedules).Result.ToList();
            scheduledReportsSubscription = apiConnection.GetSubscription<ReportSchedule[]>(ApiExceptionHandler, OnScheduleUpdate, ReportQueries.subscribeReportScheduleChanges);

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

        private void OnScheduleUpdate(ReportSchedule[] scheduledReports)
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
                foreach (ReportSchedule reportSchedule in scheduledReports)
                {
                    try
                    {
                        if (reportSchedule.Active)
                        {
                            // Add schedule interval as long as schedule time is smaller then current time 
                            while (RoundDown(reportSchedule.StartTime, CheckScheduleInterval) < dateTimeNowRounded)
                            {
                                reportSchedule.StartTime = reportSchedule.RepeatInterval switch
                                {
                                    Interval.Days => reportSchedule.StartTime.AddDays(reportSchedule.RepeatOffset),
                                    Interval.Weeks => reportSchedule.StartTime.AddDays(reportSchedule.RepeatOffset * 7),
                                    Interval.Months => reportSchedule.StartTime.AddMonths(reportSchedule.RepeatOffset),
                                    Interval.Years => reportSchedule.StartTime.AddYears(reportSchedule.RepeatOffset),
                                    Interval.Never => reportSchedule.StartTime.AddYears(42_42),
                                    _ => throw new NotSupportedException("Time interval is not supported.")
                                };
                            }

                            if (RoundDown(reportSchedule.StartTime, CheckScheduleInterval) == dateTimeNowRounded)
                            {
                                reportGeneratorTasks.Add(GenerateReport(reportSchedule, dateTimeNowRounded));
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

        private Task GenerateReport(ReportSchedule reportSchedule, DateTime dateTimeNowRounded)
        {
            CancellationToken token = new CancellationToken();
            return Task.Run(async () =>
            {
                try
                {
                    Log.WriteInfo("Report Scheduling", $"Generating scheduled report \"{reportSchedule.Name}\" with id \"{reportSchedule.Id}\" for user \"{reportSchedule.Owner.Name}\" with id \"{reportSchedule.Owner.DbId}\" ...");

                    ReportFile reportFile = new ReportFile
                    { 
                        Name = $"{reportSchedule.Name}_{dateTimeNowRounded.ToShortDateString()}",
                        GenerationDateStart = DateTime.Now,
                        TemplateId = reportSchedule.Template.Id,
                        OwnerId = reportSchedule.Owner.DbId,
                        Type = reportSchedule.Template.ReportParams.ReportType
                    };

                    // get uiuser roles + tenant
                    AuthManager authManager = new AuthManager(jwtWriter, connectedLdaps, apiConnection);
                    string jwt = await authManager.AuthorizeUserAsync(reportSchedule.Owner, validatePassword: false, lifetime: TimeSpan.FromDays(365));
                    ApiConnection apiConnectionUserContext = new GraphQlApiConnection(apiServerUri, jwt);
                    GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(jwt);
                    UserConfig userConfig = await UserConfig.ConstructAsync(globalConfig, apiConnection, reportSchedule.Owner.DbId);

                    await apiConnectionUserContext.SendQueryAsync<object>(ReportQueries.countReportSchedule, new { report_schedule_id = reportSchedule.Id });
                    await AdaptDeviceFilter(reportSchedule.Template.ReportParams, apiConnectionUserContext);

                    ReportBase report = ReportBase.ConstructReport(reportSchedule.Template, userConfig);
                    if(report.ReportType.IsDeviceRelatedReport())
                    {
                        Management[] managementsReport = Array.Empty<Management>();
                        await report.GenerateMgt(int.MaxValue, apiConnectionUserContext, 
                            managementsReportIntermediate =>
                            {
                                managementsReport = managementsReportIntermediate;
                                SetRelevantManagements(ref managementsReport, reportSchedule.Template.ReportParams.DeviceFilter);
                                return Task.CompletedTask;
                            }, token);
                        await report.GetMgtObjectsInReport(int.MaxValue, apiConnectionUserContext, _ => Task.CompletedTask);
                    }
                    else
                    {
                        List<ModellingConnection> connectionReport = new();
                        await report.GenerateCon(int.MaxValue, apiConnectionUserContext,
                            connectionReportIntermediate =>
                            {
                                connectionReport = connectionReportIntermediate;
                                return Task.CompletedTask;
                            }, token);
                        //await report.GetConObjectsInReport(int.MaxValue, apiConnectionUserContext, _ => Task.CompletedTask);
                    }
                    WriteReportFile(report, reportSchedule.OutputFormat, reportFile);
                    await SaveReport(reportFile, report.SetDescription(), apiConnectionUserContext);
                    Log.WriteInfo("Report Scheduling", $"Scheduled report \"{reportSchedule.Name}\" with id \"{reportSchedule.Id}\" for user \"{reportSchedule.Owner.Name}\" with id \"{reportSchedule.Owner.DbId}\" successfully generated.");
                }
                catch (Exception exception)
                {
                    Log.WriteError("Report Scheduling", $"Generating scheduled report \"{reportSchedule.Name}\" with id \"{reportSchedule.Id}\" lead to exception.", exception);
                }
            }, token);
        }

        private static async Task AdaptDeviceFilter(ReportParams reportParams, ApiConnection apiConnection)
        {
            try
            {
                if(!reportParams.DeviceFilter.isAnyDeviceFilterSet())
                {
                    // for scheduling no device selection means "all"
                    reportParams.DeviceFilter.Managements = await apiConnection.SendQueryAsync<List<ManagementSelect>>(DeviceQueries.getDevicesByManagement);
                    reportParams.DeviceFilter.applyFullDeviceSelection(true);
                }
                if(reportParams.ReportType == (int)ReportType.UnusedRules)
                {
                    reportParams.DeviceFilter = (await ReportDevicesBase.GetUsageDataUnsupportedDevices(apiConnection, reportParams.DeviceFilter)).reducedDeviceFilter;
                }
            }
            catch (Exception)
            {
                Log.WriteError("Set Device Filter", $"Could not adapt device filter.");
                throw;
            }
        }

        private static void WriteReportFile(ReportBase report, List<FileFormat> fileFormats, ReportFile reportFile)
        {
            reportFile.Json = report.ExportToJson();
            foreach (FileFormat format in fileFormats)
            {
                switch (format.Name)
                {
                    case GlobalConst.kCsv:
                        reportFile.Csv = report.ExportToCsv();
                        break;

                    case GlobalConst.kHtml:
                        reportFile.Html = report.ExportToHtml();
                        break;

                    case GlobalConst.kPdf:
                        reportFile.Pdf = Convert.ToBase64String(report.ToPdf(PaperKind.A4));
                        break;

                    case GlobalConst.kJson:
                        break;

                    default:
                        throw new NotSupportedException("Output format is not supported.");
                }
            }
            reportFile.GenerationDateEnd = DateTime.Now;
        }

        private static async Task SaveReport(ReportFile reportFile, string desc, ApiConnection apiConnection)
        {
            try
            {
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
                    description = desc
                };
                await apiConnection.SendQueryAsync<object>(ReportQueries.addGeneratedReport, queryVariables);
            }
            catch (Exception)
            {
                Log.WriteError("Save Report", $"Could not save report \"{reportFile.Name}\".");
                throw;
            }
        }

        private static void SetRelevantManagements(ref Management[] managementsReport, DeviceFilter deviceFilter)
        {
            if (deviceFilter.isAnyDeviceFilterSet())
            {
                List<int> relevantManagements = deviceFilter.getSelectedManagements();
                foreach (Management mgm in managementsReport)
                {
                    mgm.Ignore = !relevantManagements.Contains(mgm.Id);
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
