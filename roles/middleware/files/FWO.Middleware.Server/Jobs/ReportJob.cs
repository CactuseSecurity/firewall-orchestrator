using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.File;
using FWO.Data;
using FWO.Data.Report;
using FWO.Logging;
using FWO.Middleware.Server.Controllers;
using FWO.Report;
using FWO.Services;
using Quartz;
using FWO.Report.Data;
using FWO.Mail;
using FWO.Encryption;
using System.Text.Json;

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
        private readonly string apiServerUri;

        /// <summary>
        /// Creates a new report scheduling job.
        /// </summary>
        /// <param name="apiConnectionScheduler">API connection used by the scheduler.</param>
        /// <param name="jwtWriter">JWT writer to authorize users.</param>
        public ReportJob(ApiConnection apiConnectionScheduler, JwtWriter jwtWriter)
        {
            this.apiConnectionScheduler = apiConnectionScheduler;
            this.jwtWriter = jwtWriter;
            apiServerUri = ConfigFile.ApiServerUri ?? throw new ArgumentException("Missing api server url on startup.");
        }

        /// <inheritdoc />
        public async Task Execute(IJobExecutionContext context)
        {
            Log.WriteDebug(LogMessageTitle, "Process started");
            DateTime dateTimeNowRounded = RoundDown(DateTime.Now, CheckScheduleInterval);
            List<ReportSchedule> scheduledReports = await apiConnectionScheduler.SendQueryAsync<List<ReportSchedule>>(ReportQueries.getReportSchedules);

            if (scheduledReports is null || scheduledReports.Count == 0)
            {
                return;
            }

            await Parallel.ForEachAsync(scheduledReports, new ParallelOptions { MaxDegreeOfParallelism = Environment.ProcessorCount },
                async (reportSchedule, ct) => await ProcessScheduledReport(reportSchedule, dateTimeNowRounded, ct));
        }

        private async Task ProcessScheduledReport(ReportSchedule reportSchedule, DateTime dateTimeNowRounded, CancellationToken ct)
        {
            try
            {
                if (reportSchedule.Active)
                {
                    // Add schedule interval as long as schedule time is smaller than current time
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

                    Log.WriteInfo(LogMessageTitle, $"Scheduled report \"{reportSchedule.Name}\" with id \"{reportSchedule.Id}\" for user \"{reportSchedule.ScheduleOwningUser.Name}\" with id \"{reportSchedule.ScheduleOwningUser.DbId}\" successfully generated.");

                    ReportSchedulerConfig reportSchedulerConfig = GetReportSchedulerConfig(reportSchedule.Id, userConfig);

                    if (reportSchedulerConfig.ToArchive)
                    {
                        await SaveReportToArchive(reportFile, report.SetDescription(), apiConnectionUserContext);
                    }

                    if (reportSchedulerConfig.ToEmail)
                    {
                        await TrySendReportViaEmail(reportSchedule, reportFile, reportSchedulerConfig, userConfig);
                    }

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
                userConfig = null; // Clear reference to prevent memory leak through exception stack traces
                apiConnectionUserContext?.Dispose();
                apiConnectionUserContext = null; // Clear reference to prevent memory leak through exception stack traces
            }
        }

        private ReportSchedulerConfig GetReportSchedulerConfig(int reportScheduleID, UserConfig? userConfig = null)
        {
            if (userConfig != null)
            {
                List<ReportSchedulerConfig> reportSchedulerConfig = JsonSerializer.Deserialize<List<ReportSchedulerConfig>>(userConfig.GlobalConfig!.ReportSchedulerConfig) ?? new();
                return reportSchedulerConfig.FirstOrDefault(config => config.ReportScheduleID == reportScheduleID) ?? new();
            }
            else
            {
                return new();
            }

        }

        private async Task<(ApiConnection?, UserConfig?)> InitUserEnvironment(ReportSchedule reportSchedule)
        {
            List<Ldap> connectedLdaps = await apiConnectionScheduler.SendQueryAsync<List<Ldap>>(AuthQueries.getLdapConnections);
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

        private static async Task SaveReportToArchive(ReportFile reportFile, string desc, ApiConnection apiConnectionUser)
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

        /// <summary>
        /// Send Email with compliance report to all recipients defined in compliance settings
        /// </summary>
        /// <returns></returns>
        public async Task TrySendReportViaEmail(ReportSchedule reportSchedule, ReportFile reportFile, ReportSchedulerConfig reportSchedulerConfig, UserConfig? userConfig = null)
        {

            if (reportSchedulerConfig.ToEmail && userConfig?.GlobalConfig is GlobalConfig globalConfig)
            {
                string decryptedSecret = AesEnc.TryDecrypt(globalConfig.EmailPassword, false, "Report Scheduler", "Could not decrypt mailserver password.");

                EmailConnection emailConnection = new(
                    globalConfig.EmailServerAddress,
                    globalConfig.EmailPort,
                    globalConfig.EmailTls,
                    globalConfig.EmailUser,
                    decryptedSecret,
                    globalConfig.EmailSenderAddress
                );

                MailData? mail = PrepareEmail(reportSchedule, reportFile, reportSchedulerConfig, userConfig);

                if (mail != null)
                {
                    bool emailSend = await MailKitMailer.SendAsync(mail, emailConnection, false, new CancellationToken());
                    if (emailSend)
                    {
                        Log.WriteInfo(LogMessageTitle, "Report email sent successfully.");
                    }
                    else
                    {
                        Log.WriteError(LogMessageTitle, "Report email could not be sent.");
                    }
                }
            }
        }

        private MailData? PrepareEmail(ReportSchedule reportSchedule, ReportFile reportFile, ReportSchedulerConfig reportSchedulerConfig, UserConfig? userConfig = null)
        {
            if (userConfig != null)
            {
                string subject = reportSchedulerConfig.Subject;
                string body = reportSchedulerConfig.Body;
                MailData mailData = new(EmailHelper.CollectRecipientsFromConfig(userConfig, reportSchedulerConfig.Recipients), subject) { Body = body };
                FormFile? attachment;
                mailData.Attachments = new FormFileCollection();

                foreach (FileFormat format in reportSchedule.OutputFormat)
                {
                    switch (format.Name)
                    {
                        case GlobalConst.kCsv:
                            attachment = EmailHelper.CreateAttachment(reportFile.Csv, GlobalConst.kCsv, subject);
                            break;

                        case GlobalConst.kHtml:
                            attachment = EmailHelper.CreateAttachment(reportFile.Html, GlobalConst.kHtml, subject);
                            break;

                        case GlobalConst.kPdf:
                            attachment = EmailHelper.CreateAttachment(reportFile.Pdf, GlobalConst.kPdf, subject);
                            break;

                        case GlobalConst.kJson:
                            attachment = EmailHelper.CreateAttachment(reportFile.Json, GlobalConst.kJson, subject);
                            break;

                        default:
                            throw new NotSupportedException("Output format is not supported.");
                    }

                    if (attachment != null)
                    {
                        ((FormFileCollection)mailData.Attachments).Add(attachment);
                    }
                }

                return mailData;
            }
            else
            {
                return null;
            }
        }


        private static DateTime RoundDown(DateTime dateTime, TimeSpan roundInterval)
        {
            long delta = dateTime.Ticks % roundInterval.Ticks;
            return new DateTime(dateTime.Ticks - delta, dateTime.Kind);
        }
    }
}
