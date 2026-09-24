using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Config.File;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.Report;
using FWO.Logging;
using FWO.Middleware.Server.Services;
using FWO.Report;
using FWO.Services;
using System;
using System.Linq;
using System.Text;

namespace FWO.Middleware.Server
{
    /// <summary>
    /// Recertification check class
    /// </summary>
    public class RecertCheck
    {
        private readonly ApiConnection apiConnectionMiddlewareServer;
        private readonly GlobalConfig globalConfig;
        private readonly TokenLifetimeProvider tokenLifetimeProvider;
        private RecertCheckParams? globCheckParams;
        private List<FwoOwner> owners = [];
        private const string LogMessageTitle = "Recertification Check";

        /// <summary>
        /// Constructor for Recertification check class
        /// </summary>
        public RecertCheck(ApiConnection apiConnection, GlobalConfig globalConfig, TokenLifetimeProvider tokenLifetimeProvider)
        {
            this.apiConnectionMiddlewareServer = apiConnection;
            this.globalConfig = globalConfig;
            this.tokenLifetimeProvider = tokenLifetimeProvider;
        }

        /// <summary>
        /// Recertification check
        /// </summary>
        /// <param name="cancellationToken">Stops before the next owner; the last sent state of notifications is still updated.</param>
        public async Task<int> CheckRecertifications(CancellationToken cancellationToken = default)
        {
            int emailsSent = 0;
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                await InitEnv();
                if (globalConfig.RecertificationMode == RecertificationMode.RuleByRule)
                {
                    NotificationService notificationService = await NotificationService.CreateAsync(
                        NotificationClient.RuleRecertification,
                        globalConfig,
                        apiConnectionMiddlewareServer);
                    JwtWriter jwtWriter = new(ConfigFile.JwtPrivateKey);
                    ApiConnection apiConnectionReporter = new GraphQlApiConnection(ConfigFile.ApiServerUri ?? throw new ArgumentException("Missing api server url on startup."), jwtWriter.CreateJWTReporterViewall(tokenLifetimeProvider.GetInternalServiceTokenLifetime()));
                    await ForEachOwnerUpdatingLastSent(owners, notificationService, cancellationToken, async owner =>
                    {
                        int ownerEmailsSent = await CheckRuleByRule(owner, apiConnectionReporter, notificationService);
                        emailsSent += ownerEmailsSent;
                        if (ownerEmailsSent > 0)
                        {
                            await SetOwnerLastCheck(owner);
                        }
                    });
                }
                else
                {
                    NotificationService notificationService = await NotificationService.CreateAsync(
                        NotificationClient.Recertification,
                        globalConfig,
                        apiConnectionMiddlewareServer);
                    using UserConfig reportUserConfig = UserConfig.ForGlobalSettings(
                        globalConfig,
                        apiConnectionMiddlewareServer,
                        globalConfig.DefaultLanguage);
                    await ForEachOwnerUpdatingLastSent(owners.Where(o => IsRecertCheckTime(o)), notificationService, cancellationToken, async owner =>
                    {
                        int ownerEmailsSent = await notificationService.SendNotificationsIfDue(
                            owner,
                            owner.NextRecertDate,
                            PrepareOwnerBody(owner),
                            await PrepareOwnerReport(owner, reportUserConfig));
                        emailsSent += ownerEmailsSent;
                        if (ownerEmailsSent > 0)
                        {
                            await SetOwnerLastCheck(owner);
                        }
                    });
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                Log.WriteError(LogMessageTitle, $"Checking owners for upcoming recertifications leads to exception.", exception);
            }
            return emailsSent;
        }

        /// <summary>
        /// Checks every owner and updates the last sent state of the notifications afterwards,
        /// also when stopped between two owners, so already sent notifications are not repeated.
        /// </summary>
        private static async Task ForEachOwnerUpdatingLastSent(IEnumerable<FwoOwner> ownersToCheck, NotificationService notificationService,
            CancellationToken cancellationToken, Func<FwoOwner, Task> checkOwner)
        {
            try
            {
                foreach (FwoOwner owner in ownersToCheck)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    await checkOwner(owner);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                await notificationService.UpdateNotificationsLastSent();
                throw;
            }
            await notificationService.UpdateNotificationsLastSent();
        }

        private async Task InitEnv()
        {
            globCheckParams = System.Text.Json.JsonSerializer.Deserialize<RecertCheckParams>(globalConfig.RecCheckParams);
            owners = await apiConnectionMiddlewareServer.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwners);
        }

        private bool IsRecertCheckTime(FwoOwner owner)
        {
            if (!owner.RecertActive)
            {
                return false;
            }
            RecertCheckParams checkParams = (owner.RecertCheckParamString != null && owner.RecertCheckParamString != "" ?
                System.Text.Json.JsonSerializer.Deserialize<RecertCheckParams>(owner.RecertCheckParamString) :
                globCheckParams) ?? throw new ArgumentException("Config Parameters not set.");
            DateTime lastCheck = owner.LastRecertCheck ?? DateTime.MinValue;
            DateTime nextCheck = checkParams.RecertCheckInterval switch
            {
                SchedulerInterval.Days => lastCheck.AddDays(checkParams.RecertCheckOffset),
                SchedulerInterval.Weeks => CalcForWeeks(lastCheck, checkParams),
                SchedulerInterval.Months => CalcForMonths(lastCheck, checkParams),
                _ => throw new NotSupportedException("Time interval is not supported.")
            };
            if (nextCheck <= DateTime.Today)
            {
                return true;
            }
            return false;
        }

        private static DateTime CalcForWeeks(DateTime lastCheck, RecertCheckParams checkParams)
        {
            DateTime nextCheck;
            if (checkParams.RecertCheckWeekday == null)
            {
                nextCheck = lastCheck.AddDays(checkParams.RecertCheckOffset * GlobalConst.kDaysPerWeek);
            }
            else
            {
                nextCheck = lastCheck.AddDays((checkParams.RecertCheckOffset - 1) * GlobalConst.kDaysPerWeek + 1);
                int count = 0;
                while (nextCheck.DayOfWeek != (DayOfWeek)checkParams.RecertCheckWeekday && count < 6)
                {
                    nextCheck = nextCheck.AddDays(1);
                    count++;
                }
            }
            return nextCheck;
        }

        private static DateTime CalcForMonths(DateTime lastCheck, RecertCheckParams checkParams)
        {
            DateTime nextCheck;
            if (checkParams.RecertCheckDayOfMonth == null)
            {
                nextCheck = lastCheck.AddMonths(checkParams.RecertCheckOffset);
            }
            else
            {
                nextCheck = lastCheck.AddMonths(checkParams.RecertCheckOffset - 1);
                nextCheck = nextCheck.AddDays(1);
                int count = 0;
                while (nextCheck.Day != (int)checkParams.RecertCheckDayOfMonth && count < 30)
                {
                    nextCheck = nextCheck.AddDays(1);
                    count++;
                }
                if (nextCheck.Day != (int)checkParams.RecertCheckDayOfMonth)
                {
                    // missed the day because or month change: set to first of following month
                    nextCheck = nextCheck.AddDays(1 - nextCheck.Day);
                }
            }
            return nextCheck;
        }

        private async Task<int> CheckRuleByRule(FwoOwner owner, ApiConnection apiConnection, NotificationService notificationService)
        {
            List<Rule> openRecerts = await GenerateRulesRecertificationReport(apiConnection, owner);
            List<Rule> upcomingRecerts = [];
            List<Rule> overdueRecerts = [];
            foreach (Rule rule in openRecerts)
            {
                if (rule.Metadata.RuleRecertification.Count > 0 && rule.Metadata.RuleRecertification[0].NextRecertDate >= DateTime.Now)
                {
                    upcomingRecerts.Add(rule);
                }
                else
                {
                    overdueRecerts.Add(rule);
                }
            }
            if (upcomingRecerts.Count == 0 && overdueRecerts.Count == 0)
            {
                return 0;
            }

            string body = PrepareRulesBody(upcomingRecerts, overdueRecerts, owner.Name);
            int emailsSent = 0;
            foreach (FwoNotification notification in notificationService.Notifications.Where(n => n.OwnerId == null || n.OwnerId == owner.Id))
            {
                emailsSent += await notificationService.SendNotification(notification, owner, body);
            }
            return emailsSent;
        }

        private async Task<List<Rule>> GenerateRulesRecertificationReport(ApiConnection apiConnection, FwoOwner owner)
        {
            List<Rule> rules = [];
            try
            {
                using UserConfig userConfig = UserConfig.ForGlobalSettings(globalConfig, apiConnection, globalConfig.DefaultLanguage);

                DeviceFilter deviceFilter = new()
                {
                    Managements = await apiConnection.SendQueryAsync<List<ManagementSelect>>(DeviceQueries.getDevicesByManagement)
                };
                deviceFilter.ApplyFullDeviceSelection(true);

                ReportParams reportParams = new((int)ReportType.Recertification, deviceFilter)
                {
                    RecertFilter = new()
                    {
                        RecertOwnerList = [owner.Id],
                        RecertificationDisplayPeriod = globalConfig.RecertificationNoticePeriod
                    }
                };

                ReportData reportData = (await ReportGenerator.GenerateFromTemplate(new ReportTemplate("", reportParams), apiConnection, userConfig, DefaultInit.DoNothing))?.ReportData ?? new();

                foreach (ManagementReport management in reportData.ManagementData)
                {
                    foreach (var rulebase in management.Rulebases)
                    {
                        foreach (var rule in rulebase.Rules)
                        {
                            rules.Add(rule);
                        }
                    }
                }
            }
            catch (Exception exception)
            {
                Log.WriteError(LogMessageTitle, $"Report for owner {owner.Name} leads to exception.", exception);
            }
            return rules;
        }

        private string PrepareRulesBody(List<Rule> upcomingRecerts, List<Rule> overdueRecerts, string ownerName)
        {
            StringBuilder body = new();
            if (upcomingRecerts.Count > 0)
            {
                body.AppendLine(globalConfig.RecCheckEmailUpcomingText.Replace(Placeholder.APPNAME, ownerName) + "\r\n\r\n");
                foreach (var rule in upcomingRecerts)
                {
                    body.AppendLine(PrepareLine(rule));
                }
            }
            body.AppendLine("\r\n\r\n");
            if (overdueRecerts.Count > 0)
            {
                body.AppendLine(globalConfig.RecCheckEmailOverdueText.Replace(Placeholder.APPNAME, ownerName) + "\r\n\r\n");
                foreach (Rule rule in overdueRecerts)
                {
                    body.AppendLine(PrepareLine(rule));
                }
            }
            return body.ToString();
        }

        private static string PrepareLine(Rule rule)
        {
            Recertification? nextRecert = rule.Metadata.RuleRecertification.FirstOrDefault(x => x.RecertDate == null);
            return (nextRecert != null && nextRecert.NextRecertDate != null ? DateOnly.FromDateTime((DateTime)nextRecert.NextRecertDate) : "") + ": "
                    + rule.DeviceName + ": " + rule.Name + ":" + rule.Uid + "\r\n\r\n";  // link ?
        }

        private string PrepareOwnerBody(FwoOwner owner)
        {
            string msgText = owner.NextRecertDate >= DateTime.Today ? globalConfig.RecCheckEmailUpcomingText : globalConfig.RecCheckEmailOverdueText;
            return msgText.Replace(Placeholder.APPNAME, owner.Name);
        }

        private async Task<ReportBase?> PrepareOwnerReport(FwoOwner owner, UserConfig reportUserConfig)
        {
            ReportParams reportParams = new((int)ReportType.OwnerRecertification, new())
            {
                ModellingFilter = new()
                {
                    SelectedOwner = owner,
                    // The scheduled check uses LastRecertCheck for due evaluation. Do not
                    // apply the interactive report's next_recert_date filter as well.
                    ShowAllOwners = true
                }
            };
            return await ReportGenerator.GenerateFromTemplate(new ReportTemplate("", reportParams), apiConnectionMiddlewareServer, reportUserConfig, DefaultInit.DoNothing);
        }

        private async Task SetOwnerLastCheck(FwoOwner owner)
        {
            var Variables = new
            {
                id = owner.Id,
                lastRecertCheck = DateTime.Now
            };
            await apiConnectionMiddlewareServer.SendQueryAsync<object>(OwnerQueries.setOwnerLastCheck, Variables);
        }
    }
}
