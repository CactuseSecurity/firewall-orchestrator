using FWO.Basics;
using FWO.Data;
using FWO.Data.Report;
using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Logging;
using FWO.Middleware.Server.Controllers;
using FWO.Report;
using FWO.Services;
using System.Timers;
using FWO.Config.File;
using System.Collections.Concurrent;
using FWO.Encryption;
using FWO.Mail;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Report scheduler class
    /// </summary>
    public class ReportScheduler : SchedulerBase
    {
        private ConcurrentBag<ReportSchedule> scheduledReports = [];
        private readonly TimeSpan CheckScheduleInterval = TimeSpan.FromMinutes(1);

        private readonly string apiServerUri;
        private readonly ApiConnection apiConnectionScheduler;
        private ApiConnection? apiConnectionUserContext;
        private UserConfig? _userConfig;
        private readonly JwtWriter jwtWriter;
        private const string LogMessageTitle = "Report Scheduling";

        private List<Ldap> connectedLdaps;

        private ReportBase? _report = null;
        private ReportFile? _reportFile = null;
        private List<FileFormat>? _fileFormats = null;


        /// <summary>
        /// Async Constructor needing the connection, jwtWriter and subscription
        /// </summary>
        public static async Task<ReportScheduler> CreateAsync(ApiConnection apiConnection, JwtWriter jwtWriter, GraphQlApiSubscription<List<Ldap>> connectedLdapsSubscription)
        {
            GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(apiConnection, true);
            return new ReportScheduler(apiConnection, globalConfig, jwtWriter, connectedLdapsSubscription);
        }

        private ReportScheduler(ApiConnection apiConnection, GlobalConfig globalConfig, JwtWriter jwtWriter, GraphQlApiSubscription<List<Ldap>> connectedLdapsSubscription)
            : base(apiConnection, globalConfig, ReportQueries.subscribeReportScheduleChanges, SchedulerInterval.Minutes, "Report")
        {
            this.jwtWriter = jwtWriter;
            apiConnectionScheduler = apiConnection;
            apiServerUri = ConfigFile.ApiServerUri;

            connectedLdaps = apiConnectionScheduler.SendQueryAsync<List<Ldap>>(AuthQueries.getLdapConnections).Result;
            connectedLdapsSubscription.OnUpdate += OnLdapUpdate;

            apiConnectionScheduler.GetSubscription<ReportSchedule[]>(ApiExceptionHandler, OnScheduleUpdate, ReportQueries.subscribeReportScheduleChanges);

            StartScheduleTimer(1, DateTime.Now);
        }

        private void OnLdapUpdate(List<Ldap> connectedLdaps)
        {
            this.connectedLdaps = connectedLdaps;
        }

        private void OnScheduleUpdate(ReportSchedule[] scheduledReports)
        {
            this.scheduledReports = [.. scheduledReports];
        }

        /// <summary>
        /// set scheduling timer from config values (not applicable here)
        /// </summary>
        protected override void OnGlobalConfigChange(List<ConfigItem> config)
        { }

        /// <summary>
        /// define the processing to be done
        /// </summary>
        protected override async void Process(object? _, ElapsedEventArgs __)
        {
            DateTime dateTimeNowRounded = RoundDown(DateTime.Now, CheckScheduleInterval);

            await Parallel.ForEachAsync(scheduledReports, new ParallelOptions() { MaxDegreeOfParallelism = Environment.ProcessorCount },
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
                                    SchedulerInterval.Weeks => reportSchedule.StartTime.AddDays(reportSchedule.RepeatOffset * 7),
                                    SchedulerInterval.Months => reportSchedule.StartTime.AddMonths(reportSchedule.RepeatOffset),
                                    SchedulerInterval.Years => reportSchedule.StartTime.AddYears(reportSchedule.RepeatOffset),
                                    SchedulerInterval.Never => reportSchedule.StartTime.AddYears(42_42),
                                    _ => throw new NotSupportedException("Time interval is not supported.")
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
            _fileFormats = reportSchedule.OutputFormat;

            await Task.Run(async () =>
            {
                try
                {
                    Log.WriteInfo(LogMessageTitle, $"Generating scheduled report \"{reportSchedule.Name}\" with id \"{reportSchedule.Id}\" for user \"{reportSchedule.ScheduleOwningUser.Name}\" with id \"{reportSchedule.ScheduleOwningUser.DbId}\" ...");

                    if (!await InitUserEnvironment(reportSchedule) || apiConnectionUserContext == null || _userConfig == null)
                    {
                        return;
                    }

                    _reportFile = new()
                    {
                        Name = $"{reportSchedule.Name}_{dateTimeNowRounded.ToShortDateString()}",
                        GenerationDateStart = DateTime.Now,
                        TemplateId = reportSchedule.Template.Id,
                        OwnerId = reportSchedule.ScheduleOwningUser.DbId,
                        Type = reportSchedule.Template.ReportParams.ReportType
                    };

                    await apiConnectionUserContext.SendQueryAsync<object>(ReportQueries.countReportSchedule, new { report_schedule_id = reportSchedule.Id });
                    await AdaptDeviceFilter(reportSchedule.Template.ReportParams, apiConnectionUserContext);

                    _report = await ReportGenerator.Generate(reportSchedule.Template, apiConnectionUserContext, _userConfig, DefaultInit.DoNothing, token);
                    if (_report != null)
                    {
                        await _report.GetObjectsInReport(int.MaxValue, apiConnectionUserContext, _ => Task.CompletedTask);
                        await WriteReportFile(_report, reportSchedule.OutputFormat, _reportFile);
                        await SaveReport(_reportFile, _report.SetDescription(), apiConnectionUserContext);
                        Log.WriteInfo(LogMessageTitle, $"Scheduled report \"{reportSchedule.Name}\" with id \"{reportSchedule.Id}\" for user \"{reportSchedule.ScheduleOwningUser.Name}\" with id \"{reportSchedule.ScheduleOwningUser.DbId}\" successfully generated.");
                        if (!string.IsNullOrWhiteSpace(_userConfig?.GlobalConfig?.ComplianceCheckMailRecipients))
                        {
                            await SendComplianceCheckEmail();
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
            }, token);
        }

        private async Task<bool> InitUserEnvironment(ReportSchedule reportSchedule)
        {
            AuthManager authManager = new(jwtWriter, connectedLdaps, apiConnectionScheduler);
            string jwt = await authManager.AuthorizeUserAsync(reportSchedule.ScheduleOwningUser, validatePassword: false, lifetime: TimeSpan.FromDays(365));
            apiConnectionUserContext = new GraphQlApiConnection(apiServerUri, jwt);
            GlobalConfig globalConfig = await GlobalConfig.ConstructAsync(jwt);
            _userConfig = await UserConfig.ConstructAsync(globalConfig, apiConnectionUserContext, reportSchedule.ScheduleOwningUser.DbId);

            if (((ReportType)reportSchedule.Template.ReportParams.ReportType).IsModellingReport())
            {
                _userConfig.User.Groups = reportSchedule.ScheduleOwningUser.Groups;
                await UiUserHandler.GetOwnershipsFromOwnerLdap(apiConnectionUserContext, _userConfig.User);
                if (!_userConfig.User.Ownerships.Contains(reportSchedule.Template.ReportParams.ModellingFilter.SelectedOwner.Id))
                {
                    Log.WriteInfo(LogMessageTitle, "Report not generated as owner is not valid anymore.");
                    return false;
                }
            }
            return true;
        }

        private static async Task AdaptDeviceFilter(ReportParams reportParams, ApiConnection apiConnectionUser)
        {
            try
            {
                if (!reportParams.DeviceFilter.IsAnyDeviceFilterSet())
                {
                    // for scheduling no device selection means "all"
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
                Log.WriteError(LogMessageTitle, $"Could not adapt device filter.");
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
                Log.WriteError(LogMessageTitle, $"Could not save report \"{reportFile.Name}\".");
                throw;
            }
        }

        private static DateTime RoundDown(DateTime dateTime, TimeSpan roundInterval)
        {
            long delta = dateTime.Ticks % roundInterval.Ticks;
            return new DateTime(dateTime.Ticks - delta);
        }
        
        /// <summary>
        /// Send Email with compliance report to all recipients defined in compliance settings
        /// </summary>
        /// <returns></returns>
        public async Task SendComplianceCheckEmail()
        {
            if (_userConfig?.GlobalConfig is GlobalConfig globalConfig)
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

                MailData? mail = PrepareEmail();

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

        private MailData? PrepareEmail()
        {
            if (_userConfig?.GlobalConfig is GlobalConfig globalConfig && _reportFile is ReportFile reportFile && _fileFormats is List<FileFormat> fileFormats)
            {
                string subject = globalConfig.ComplianceCheckMailSubject;
                string body = globalConfig.ComplianceCheckMailBody;
                MailData mailData = new(EmailHelper.CollectRecipientsFromConfig(_userConfig, globalConfig.ComplianceCheckMailRecipients), subject) { Body = body };
                FormFile? attachment;
                mailData.Attachments = new FormFileCollection();

                foreach (FileFormat format in fileFormats)
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
                            attachment = null;
                            break;

                        default:
                            throw new NotSupportedException("Output format is not supported.");
                    }

                    if (attachment != null)
                    {
                        ((FormFileCollection) mailData.Attachments).Add(attachment);
                    }
                }

                return mailData;
            }
            else
            {
                return null;
            }
        }
    }
}
