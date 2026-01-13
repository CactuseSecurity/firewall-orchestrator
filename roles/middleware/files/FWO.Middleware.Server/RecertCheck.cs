using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Config.File;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.Report;
using FWO.Encryption;
using FWO.Logging;
using FWO.Mail;
using FWO.Report;
using FWO.Services;
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
        private readonly List<GroupGetReturnParameters> groups = [];
        private List<UiUser> uiUsers = [];
        private RecertCheckParams? globCheckParams;
        private List<FwoOwner> owners = [];
        private const string LogMessageTitle = "Recertification Check";

        /// <summary>
        /// Constructor for Recertification check class
        /// </summary>
        public RecertCheck(ApiConnection apiConnection, GlobalConfig globalConfig)
        {
            this.apiConnectionMiddlewareServer = apiConnection;
            this.globalConfig = globalConfig;
        }

        /// <summary>
        /// Recertification check
        /// </summary>
        public async Task<int> CheckRecertifications()
        {
            int emailsSent = 0;
            try
            {
                await InitEnv();
                if (globalConfig.RecertificationMode == RecertificationMode.RuleByRule)
                {
                    string decryptedSecret = AesEnc.TryDecrypt(globalConfig.EmailPassword, false, LogMessageTitle, "Could not decrypt mailserver password.");
                    EmailConnection emailConnection = new(globalConfig.EmailServerAddress, globalConfig.EmailPort,
                        globalConfig.EmailTls, globalConfig.EmailUser, decryptedSecret, globalConfig.EmailSenderAddress);
                    JwtWriter jwtWriter = new(ConfigFile.JwtPrivateKey);
                    ApiConnection apiConnectionReporter = new GraphQlApiConnection(ConfigFile.ApiServerUri ?? throw new ArgumentException("Missing api server url on startup."), jwtWriter.CreateJWTReporterViewall());
                    foreach (var owner in owners)
                    {
                        emailsSent += await CheckRuleByRule(owner, apiConnectionReporter, emailConnection);
                        await SetOwnerLastCheck(owner);
                    }
                }
                else
                {
                    List<UserGroup> OwnerGroups = await MiddlewareServerServices.GetInternalGroups(apiConnectionMiddlewareServer);
                    NotificationService notificationService = await NotificationService.CreateAsync(NotificationClient.Recertification, globalConfig, apiConnectionMiddlewareServer, OwnerGroups);
                    foreach (var owner in owners.Where(o => IsRecertCheckTime(o)))
                    {
                        emailsSent += await notificationService.SendNotifications(owner, null, PrepareOwnerBody(owner), await PrepareOwnerReport(owner));
                        await SetOwnerLastCheck(owner);
                    }
                    await notificationService.UpdateNotificationsLastSent();
                }
            }
            catch (Exception exception)
            {
                Log.WriteError(LogMessageTitle, $"Checking owners for upcoming recertifications leads to exception.", exception);
            }
            return emailsSent;
        }

        private async Task InitEnv()
        {
            globCheckParams = System.Text.Json.JsonSerializer.Deserialize<RecertCheckParams>(globalConfig.RecCheckParams);
            List<Ldap> connectedLdaps = apiConnectionMiddlewareServer.SendQueryAsync<List<Ldap>>(AuthQueries.getLdapConnections).Result;
            foreach (Ldap currentLdap in connectedLdaps.Where(l => l.IsInternal() && l.HasGroupHandling()))
            {
                groups.AddRange(await currentLdap.GetAllInternalGroups());
            }
            uiUsers = await apiConnectionMiddlewareServer.SendQueryAsync<List<UiUser>>(AuthQueries.getUsers);
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
            var nextCheck = checkParams.RecertCheckInterval switch
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

        private async Task<int> CheckRuleByRule(FwoOwner owner, ApiConnection apiConnection, EmailConnection emailConnection)
        {
            List<Rule> openRecerts = await GenerateRulesRecertificationReport(apiConnection, owner);
            List<Rule> upcomingRecerts = [];
            List<Rule> overdueRecerts = [];
            foreach (var rule in openRecerts)
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
            if (upcomingRecerts.Count > 0 || overdueRecerts.Count > 0)
            {
                await MailKitMailer.SendAsync(PrepareRulesEmail(owner, upcomingRecerts, overdueRecerts), emailConnection, false, new());
                return 1;
            }
            return 0;
        }

        private async Task<List<Rule>> GenerateRulesRecertificationReport(ApiConnection apiConnection, FwoOwner owner)
        {
            List<Rule> rules = [];
            try
            {
                UserConfig userConfig = new(globalConfig);

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

                foreach (var management in reportData.ManagementData)
                {
                    foreach (var rulebase in management.Rulebases)
                    {
                        foreach (var rule in rulebase.Rules)
                        {
                            rule.Metadata.UpdateRecertPeriods(owner.RecertInterval ?? globalConfig.RecertificationPeriod, 0);
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

        private MailData PrepareRulesEmail(FwoOwner owner, List<Rule> upcomingRecerts, List<Rule> overdueRecerts)
        {
            string subject = globalConfig.RecCheckEmailSubject + " " + owner.Name;
            return new MailData(CollectEmailAddresses(owner), subject) { Body = PrepareRulesBody(upcomingRecerts, overdueRecerts, owner.Name) };
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
                foreach (var rule in overdueRecerts)
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

        private List<string> CollectEmailAddresses(FwoOwner owner)
        {
            if (globalConfig.UseDummyEmailAddress)
            {
                return [globalConfig.DummyEmailAddress];
            }
            List<string> tos = [];
            List<string> userDns = [];
            if (owner.Dn != "")
            {
                userDns.Add(owner.Dn);
            }
            GroupGetReturnParameters? ownerGroup = groups.FirstOrDefault(x => x.GroupDn == owner.GroupDn);
            if (ownerGroup != null)
            {
                userDns.AddRange(ownerGroup.Members);
            }
            foreach (var userDn in userDns)
            {
                UiUser? uiuser = uiUsers.FirstOrDefault(x => x.Dn == userDn);
                if (uiuser != null && uiuser.Email != null && uiuser.Email != "")
                {
                    tos.Add(uiuser.Email);
                }
            }
            return tos;
        }

        private string PrepareOwnerBody(FwoOwner owner)
        {
            string msgText = owner.NextRecertDate >= DateTime.Today ? globalConfig.RecCheckEmailUpcomingText : globalConfig.RecCheckEmailOverdueText;
            return msgText.Replace(Placeholder.APPNAME, owner.Name);
        }

        private async Task<ReportBase?> PrepareOwnerReport(FwoOwner owner)
        {
            ReportParams reportParams = new((int)ReportType.OwnerRecertification, new())
            {
                ModellingFilter = new()
                {
                    SelectedOwner = owner
                }
            };
            return await ReportGenerator.GenerateFromTemplate(new ReportTemplate("", reportParams), apiConnectionMiddlewareServer, new UserConfig(globalConfig), DefaultInit.DoNothing);
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
