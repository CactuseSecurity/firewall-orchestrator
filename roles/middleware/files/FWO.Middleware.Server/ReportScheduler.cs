using FWO.Basics;
using FWO.Api.Data;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Logging;
using FWO.Middleware.Server.Controllers;
using FWO.Report;
using FWO.Report.Filter;
using System.Timers;
using FWO.Config.File;
using FWO.Services;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Report scheduler class
    /// </summary>
    public class ReportScheduler
    {
        private readonly object scheduledReportsLock = new ();
        private List<ReportSchedule> scheduledReports = [];
        private readonly TimeSpan CheckScheduleInterval = TimeSpan.FromMinutes(1);

        private readonly string apiServerUri;
        private readonly ApiConnection apiConnectionScheduler;
        private ApiConnection apiConnectionUserContext;
        private UserConfig userConfig;
        private readonly GraphQlApiSubscription<ReportSchedule[]> scheduledReportsSubscription;
        private readonly JwtWriter jwtWriter;

        private readonly object ldapLock = new ();
        private List<Ldap> connectedLdaps;

		/// <summary>
		/// Constructor needing connection, jwtWriter and subscription to connected ldaps
		/// </summary>
        public ReportScheduler(ApiConnection apiConnection, JwtWriter jwtWriter, GraphQlApiSubscription<List<Ldap>> connectedLdapsSubscription)
        {
            this.jwtWriter = jwtWriter;            
            this.apiConnectionScheduler = apiConnection;
            apiServerUri = ConfigFile.ApiServerUri;

            connectedLdaps = apiConnectionScheduler.SendQueryAsync<List<Ldap>>(AuthQueries.getLdapConnections).Result;
            connectedLdapsSubscription.OnUpdate += OnLdapUpdate;

            //scheduledReports = apiConnectionScheduler.SendQueryAsync<ReportSchedule[]>(ReportQueries.getReportSchedules).Result.ToList();
            scheduledReportsSubscription = apiConnectionScheduler.GetSubscription<ReportSchedule[]>(ApiExceptionHandler, OnScheduleUpdate, ReportQueries.subscribeReportScheduleChanges);

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
                this.scheduledReports = [.. scheduledReports];
            }
        }

        private void ApiExceptionHandler(Exception exception)
        {
            Log.WriteError("Report scheduler", "Api subscription lead to exception. Retry subscription.", exception);
            // Subscription will be restored if no exception is thrown here
        }

        private async void CheckSchedule(object? _, ElapsedEventArgs __)
        {
            List<Task> reportGeneratorTasks = [];

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
            CancellationToken token = new ();
            return Task.Run(async () =>
            {
                try
                {
                    Log.WriteInfo("Report Scheduling", $"Generating scheduled report \"{reportSchedule.Name}\" with id \"{reportSchedule.Id}\" for user \"{reportSchedule.ScheduleOwningUser.Name}\" with id \"{reportSchedule.ScheduleOwningUser.DbId}\" ...");

                    if(!await InitUserEnvironment(reportSchedule))
                    {
                        return;
                    }

                    ReportFile reportFile = new ()
                    { 
                        Name = $"{reportSchedule.Name}_{dateTimeNowRounded.ToShortDateString()}",
                        GenerationDateStart = DateTime.Now,
                        TemplateId = reportSchedule.Template.Id,
                        OwnerId = reportSchedule.ScheduleOwningUser.DbId,
                        Type = reportSchedule.Template.ReportParams.ReportType
                    };

                    await apiConnectionUserContext.SendQueryAsync<object>(ReportQueries.countReportSchedule, new { report_schedule_id = reportSchedule.Id });
                    await AdaptDeviceFilter(reportSchedule.Template.ReportParams, apiConnectionUserContext);

                    ReportBase report = ReportBase.ConstructReport(reportSchedule.Template, userConfig);
                    if(report.ReportType.IsDeviceRelatedReport())
                    {
                        await report.Generate(int.MaxValue, apiConnectionUserContext, 
                            rep =>
                            {
                                report.ReportData.ManagementData = rep.ManagementData;
                                SetRelevantManagements(ref report.ReportData.ManagementData, reportSchedule.Template.ReportParams.DeviceFilter);
                                return Task.CompletedTask;
                            }, token);
                    }
                    else
                    {
                        await GenerateConnectionsReport(reportSchedule, report, apiConnectionUserContext, token);
                    }
                    await report.GetObjectsInReport(int.MaxValue, apiConnectionUserContext, _ => Task.CompletedTask);
                    WriteReportFile(report, reportSchedule.OutputFormat, reportFile);
                    await SaveReport(reportFile, report.SetDescription(), apiConnectionUserContext);
                    Log.WriteInfo("Report Scheduling", $"Scheduled report \"{reportSchedule.Name}\" with id \"{reportSchedule.Id}\" for user \"{reportSchedule.ScheduleOwningUser.Name}\" with id \"{reportSchedule.ScheduleOwningUser.DbId}\" successfully generated.");
                }
                catch (Exception exception)
                {
                    Log.WriteError("Report Scheduling", $"Generating scheduled report \"{reportSchedule.Name}\" with id \"{reportSchedule.Id}\" lead to exception.", exception);
                }
            }, token);
        }

        private async Task<bool> InitUserEnvironment(ReportSchedule reportSchedule)
        {
            AuthManager authManager = new (jwtWriter, connectedLdaps, apiConnectionScheduler);
            string jwt = await authManager.AuthorizeUserAsync(reportSchedule.ScheduleOwningUser, validatePassword: false, lifetime: TimeSpan.FromDays(365));
            apiConnectionUserContext = new GraphQlApiConnection(apiServerUri, jwt);
            GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(jwt);
            userConfig = await UserConfig.ConstructAsync(globalConfig, apiConnectionUserContext, reportSchedule.ScheduleOwningUser.DbId);

            if(((ReportType)reportSchedule.Template.ReportParams.ReportType).IsModellingReport())
            {
                userConfig.User.Groups = reportSchedule.ScheduleOwningUser.Groups;
                await UiUserHandler.GetOwnerships(apiConnectionUserContext, userConfig.User);
                if(!userConfig.User.Ownerships.Contains(reportSchedule.Template.ReportParams.ModellingFilter.SelectedOwner.Id))
                {
                    Log.WriteDebug("Report Scheduling", "Report not generated as owner is not valid anymore.");
                    return false;
                }
            }
            return true;
        }

        private async Task GenerateConnectionsReport(ReportSchedule reportSchedule, ReportBase report, ApiConnection apiConnectionUser, CancellationToken token)
        {
            ModellingAppRole dummyAppRole = new();
            List<ModellingAppRole> dummyAppRoles = await apiConnectionUser.SendQueryAsync<List<ModellingAppRole>>(ModellingQueries.getDummyAppRole);
            if(dummyAppRoles.Count > 0)
            {
                dummyAppRole = dummyAppRoles.First();
            }
            foreach(var selectedOwner in reportSchedule.Template.ReportParams.ModellingFilter.SelectedOwners)
            {
                OwnerReport actOwnerData = new(dummyAppRole.Id){ Name = selectedOwner.Name };
                report.ReportData.OwnerData.Add(actOwnerData);
                await report.Generate(int.MaxValue, apiConnectionUser,
                rep =>
                {
                    actOwnerData.Connections = rep.OwnerData.First().Connections;
                    return Task.CompletedTask;
                }, token);
            }
            await PrepareConnReportData(reportSchedule, report, apiConnectionUser);
            List<ModellingConnection> comSvcs = await apiConnectionUser.SendQueryAsync<List<ModellingConnection>>(ModellingQueries.getCommonServices);
            if(comSvcs.Count > 0)
            {
                report.ReportData.GlobalComSvc = [new(){GlobalComSvcs = comSvcs, Name = userConfig.GetText("global_common_services")}];
            }
        }

        private async Task PrepareConnReportData(ReportSchedule reportSchedule, ReportBase report, ApiConnection apiConnectionUser)
        {
            ModellingHandlerBase handlerBase = new(apiConnectionUser, userConfig, new(), false, DefaultInit.DoNothing);
            foreach(var ownerReport in report.ReportData.OwnerData)
            {
                foreach(var conn in ownerReport.Connections)
                {
                    await handlerBase.ExtractUsedInterface(conn);
                }
                ownerReport.Name = reportSchedule.Template.ReportParams.ModellingFilter.SelectedOwner.Name;
                ownerReport.RegularConnections = ownerReport.Connections.Where(x => !x.IsInterface && !x.IsCommonService).ToList();
                ownerReport.Interfaces = ownerReport.Connections.Where(x => x.IsInterface).ToList();
                ownerReport.CommonServices = ownerReport.Connections.Where(x => !x.IsInterface && x.IsCommonService).ToList();
            }
        }

        private static async Task AdaptDeviceFilter(ReportParams reportParams, ApiConnection apiConnectionUser)
        {
            try
            {
                if(!reportParams.DeviceFilter.isAnyDeviceFilterSet())
                {
                    // for scheduling no device selection means "all"
                    reportParams.DeviceFilter.Managements = await apiConnectionUser.SendQueryAsync<List<ManagementSelect>>(DeviceQueries.getDevicesByManagement);
                    reportParams.DeviceFilter.applyFullDeviceSelection(true);
                }
                if(reportParams.ReportType == (int)ReportType.UnusedRules)
                {
                    reportParams.DeviceFilter = (await ReportDevicesBase.GetUsageDataUnsupportedDevices(apiConnectionUser, reportParams.DeviceFilter)).reducedDeviceFilter;
                }
            }
            catch (Exception)
            {
                Log.WriteError("Set Device Filter", $"Could not adapt device filter.");
                throw;
            }
        }

        private static async Task WriteReportFile(ReportBase report, List<FileFormat> fileFormats, ReportFile reportFile)
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
                        reportFile.Pdf = report.ToPdf(PeachPDF.PdfSharpCore.PageSize.A4);
                        break;

                    case GlobalConst.kJson:
                        break;

                    default:
                        throw new NotSupportedException("Output format is not supported.");
                }
            }
            reportFile.GenerationDateEnd = DateTime.Now;
        }

        private static async Task SaveReport(ReportFile reportFile, string desc, ApiConnection apiConnectionUser)
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
                await apiConnectionUser.SendQueryAsync<object>(ReportQueries.addGeneratedReport, queryVariables);
            }
            catch (Exception)
            {
                Log.WriteError("Save Report", $"Could not save report \"{reportFile.Name}\".");
                throw;
            }
        }

        private static void SetRelevantManagements(ref List<ManagementReport> managementsReport, DeviceFilter deviceFilter)
        {
            if (deviceFilter.isAnyDeviceFilterSet())
            {
                List<int> relevantManagements = deviceFilter.getSelectedManagements();
                foreach (var mgm in managementsReport)
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
