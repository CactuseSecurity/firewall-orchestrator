using System.Collections.Immutable;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.File;
using FWO.Data;
using FWO.Data.Report;
using FWO.Logging;
using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.Services;
using FWO.Report;
using FWO.Services;
using Quartz;

namespace FWO.Middleware.Server.Jobs
{
    /// <summary>
    /// Quartz job that checks report schedules and generates reports when due.
    /// </summary>
    [DisallowConcurrentExecution]
    public class ReportJob : IJob
    {
        private const string LogMessageTitle = "Report Scheduling";
        private static readonly TimeSpan CheckScheduleInterval = TimeSpan.FromMinutes(1);

        private readonly ApiConnection apiConnectionScheduler;
        private readonly JwtWriter jwtWriter;
        private readonly ReportSchedulerState state;
        private readonly string apiServerUri;

        /// <summary>
        /// Creates a new report scheduling job.
        /// </summary>
        /// <param name="apiConnectionScheduler">API connection used by the scheduler.</param>
        /// <param name="jwtWriter">JWT writer to authorize users.</param>
        /// <param name="state">Shared scheduler state.</param>
        public ReportJob(ApiConnection apiConnectionScheduler, JwtWriter jwtWriter, ReportSchedulerState state)
        {
            this.apiConnectionScheduler = apiConnectionScheduler;
            this.jwtWriter = jwtWriter;
            this.state = state;
            apiServerUri = ConfigFile.ApiServerUri ?? throw new ArgumentException("Missing api server url on startup.");
        }

        /// <inheritdoc />
        public async Task Execute(IJobExecutionContext context)
        {
            Log.WriteDebug(LogMessageTitle, "Process started");
            DateTime dateTimeNowRounded = RoundDown(DateTime.Now, CheckScheduleInterval);
            ImmutableArray<ReportSchedule> scheduledReports = state.ScheduledReports;

            if (scheduledReports.IsDefaultOrEmpty)
            {
                return;
            }

            await Parallel.ForEachAsync(scheduledReports, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                async (reportSchedule, ct) =>
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
                                    SchedulerInterval.Days => reportSchedule.StartTime.AddDays(reportSchedule.RepeatOffset),
                                    SchedulerInterval.Weeks => reportSchedule.StartTime.AddDays(reportSchedule.RepeatOffset * GlobalConst.kDaysPerWeek),
                                    SchedulerInterval.Months => reportSchedule.StartTime.AddMonths(reportSchedule.RepeatOffset),
                                    SchedulerInterval.Years => reportSchedule.StartTime.AddYears(reportSchedule.RepeatOffset),
                                    SchedulerInterval.Never => reportSchedule.StartTime.AddYears(42_42),
                                    _ => throw new NotSupportedException("Time interval is not supported."),
                                };
                            }

                            if (RoundDown(reportSchedule.StartTime, CheckScheduleInterval) == dateTimeNowRounded)
                            {
                                await GenerateReport(reportSchedule, dateTimeNowRounded, ct);
                            }
                        }
                    }
                    catch (Exception exception)
                    {
                        Log.WriteError(LogMessageTitle, "Checking scheduled reports lead to exception.", exception);
                    }
                });
        }

        private async Task GenerateReport(ReportSchedule reportSchedule, DateTime dateTimeNowRounded, CancellationToken token)
        {
            ApiConnection? apiConnectionUserContext = null;
            UserConfig? userConfig = null;

            try
            {
                Log.WriteInfo(LogMessageTitle, $"Generating scheduled report \"{reportSchedule.Name}\" with id \"{reportSchedule.Id}\" for user \"{reportSchedule.ScheduleOwningUser.Name}\" with id \"{reportSchedule.ScheduleOwningUser.DbId}\" ...");

                (apiConnectionUserContext, userConfig) = await InitUserEnvironment(reportSchedule);
                if (apiConnectionUserContext == null || userConfig == null)
                {
                    return;
                }

                ReportFile reportFile = new()
                {
                    Name = $"{reportSchedule.Name}_{dateTimeNowRounded.ToShortDateString()}",
                    GenerationDateStart = DateTime.Now,
                    TemplateId = reportSchedule.Template.Id,
                    OwningUserId = reportSchedule.ScheduleOwningUser.DbId,
                    Type = reportSchedule.Template.ReportParams.ReportType,
                };

                await apiConnectionUserContext.SendQueryAsync<object>(ReportQueries.countReportSchedule, new { report_schedule_id = reportSchedule.Id });
                await AdaptDeviceFilter(reportSchedule.Template.ReportParams, apiConnectionUserContext);

                ReportBase? report = await ReportGenerator.GenerateFromTemplate(reportSchedule.Template, apiConnectionUserContext, userConfig, DefaultInit.DoNothing, token);
                if (report != null)
                {
                    await report.GetObjectsInReport(int.MaxValue, apiConnectionUserContext, _ => Task.CompletedTask);
                    await WriteReportFile(report, reportSchedule.OutputFormat, reportFile);
                    await SaveReport(reportFile, report.SetDescription(), apiConnectionUserContext);
                    Log.WriteInfo(LogMessageTitle, $"Scheduled report \"{reportSchedule.Name}\" with id \"{reportSchedule.Id}\" for user \"{reportSchedule.ScheduleOwningUser.Name}\" with id \"{reportSchedule.ScheduleOwningUser.DbId}\" successfully generated.");
                }
                else
                {
                    Log.WriteInfo(LogMessageTitle, $"Scheduled report \"{reportSchedule.Name}\" with id \"{reportSchedule.Id}\" for user \"{reportSchedule.ScheduleOwningUser.Name}\" with id \"{reportSchedule.ScheduleOwningUser.DbId}\" was empty.");
                }
            }
            catch (TaskCanceledException)
            {
                Log.WriteWarning(LogMessageTitle, $"Generating scheduled report \"{reportSchedule.Name}\" was cancelled");
            }
            catch (Exception exception)
            {
                Log.WriteError(LogMessageTitle, $"Generating scheduled report \"{reportSchedule.Name}\" with id \"{reportSchedule.Id}\" lead to exception.", exception);
            }
            finally
            {
                userConfig?.Dispose();
                apiConnectionUserContext?.Dispose();
            }
        }

        private async Task<(ApiConnection?, UserConfig?)> InitUserEnvironment(ReportSchedule reportSchedule)
        {
            List<Ldap> connectedLdaps = state.ConnectedLdaps.ToList();
            AuthManager authManager = new(jwtWriter, connectedLdaps, apiConnectionScheduler);
            string jwt = await authManager.AuthorizeUserAsync(reportSchedule.ScheduleOwningUser, validatePassword: false, lifetime: TimeSpan.FromDays(365));
            ApiConnection apiConnectionUserContext = new GraphQlApiConnection(apiServerUri, jwt);
            GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(jwt);
            UserConfig userConfig = await UserConfig.ConstructAsync(globalConfig, apiConnectionUserContext, reportSchedule.ScheduleOwningUser.DbId);

            if (((ReportType)reportSchedule.Template.ReportParams.ReportType).IsModellingReport())
            {
                userConfig.User.Groups = reportSchedule.ScheduleOwningUser.Groups;
                await UiUserHandler.GetOwnershipsFromOwnerLdap(apiConnectionScheduler, userConfig.User);
                if (!userConfig.User.Ownerships.Contains(reportSchedule.Template.ReportParams.ModellingFilter.SelectedOwner.Id)
                    && !userConfig.User.Ownerships.Contains(0))
                {
                    Log.WriteInfo(LogMessageTitle, "Report not generated as owner is not valid anymore.");
                    return (null, null);
                }
            }

            return (apiConnectionUserContext, userConfig);
        }

        private static async Task AdaptDeviceFilter(ReportParams reportParams, ApiConnection apiConnectionUser)
        {
            try
            {
                if (!reportParams.DeviceFilter.IsAnyDeviceFilterSet())
                {
                    // For scheduling no device selection means "all".
                    reportParams.DeviceFilter.Managements = await apiConnectionUser.SendQueryAsync<List<ManagementSelect>>(DeviceQueries.getDevicesByManagement);
                    reportParams.DeviceFilter.ApplyFullDeviceSelection(true);
                }

                if (reportParams.ReportType == (int)ReportType.UnusedRules)
                {
                    reportParams.DeviceFilter = (await ReportDevicesBase.GetUsageDataUnsupportedDevices(apiConnectionUser, reportParams.DeviceFilter)).reducedDeviceFilter;
                }
            }
            catch (Exception)
            {
                Log.WriteError(LogMessageTitle, "Could not adapt device filter.");
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
                        string html = report.ExportToHtml();
                        reportFile.Pdf = await report.ToPdf(html);
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
                    report_owner_id = reportFile.OwningUserId,
                    report_template_id = reportFile.TemplateId,
                    report_pdf = reportFile.Pdf,
                    report_csv = reportFile.Csv,
                    report_html = reportFile.Html,
                    report_json = reportFile.Json,
                    report_type = reportFile.Type,
                    description = desc,
                    read_only = false,
                };

                await apiConnectionUser.SendQueryAsync<object>(ReportQueries.addGeneratedReport, queryVariables);
            }
            catch (Exception)
            {
                Log.WriteError(LogMessageTitle, $"Could not save report \"{reportFile.Name}\".");
                throw;
            }
        }

        private static DateTime RoundDown(DateTime dateTime, TimeSpan roundInterval)
        {
            long delta = dateTime.Ticks % roundInterval.Ticks;
            return new DateTime(dateTime.Ticks - delta);
        }
    }
}
