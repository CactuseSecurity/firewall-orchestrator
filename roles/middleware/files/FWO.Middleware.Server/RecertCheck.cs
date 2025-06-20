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
                string decryptedSecret = "";
                try
                {
                    string mainKey = AesEnc.GetMainKey();
                    decryptedSecret = AesEnc.Decrypt(globalConfig.EmailPassword, mainKey);
                }
                catch (Exception exception)
                {
                    Log.WriteError(LogMessageTitle, $"Could not decrypt mailserver password.", exception);				
                }
                EmailConnection emailConnection = new(globalConfig.EmailServerAddress, globalConfig.EmailPort,
                    globalConfig.EmailTls, globalConfig.EmailUser, decryptedSecret, globalConfig.EmailSenderAddress);
                MailKitMailer mailer = new(emailConnection);
                JwtWriter jwtWriter = new(ConfigFile.JwtPrivateKey);
                ApiConnection apiConnectionReporter = new GraphQlApiConnection(ConfigFile.ApiServerUri ?? throw new ArgumentException("Missing api server url on startup."), jwtWriter.CreateJWTReporterViewall());

                foreach(var owner in owners.Where(o => IsCheckTime(o)))
                {
                    // todo: refine handling
                    List<Rule> upcomingRecerts = await GenerateRecertificationReport(apiConnectionReporter, owner, false);
                    List<Rule> overdueRecerts = []; // await GenerateRecertificationReport(apiConnectionReporter, owner, true);

                    if(upcomingRecerts.Count > 0 || overdueRecerts.Count > 0)
                    {
                        await mailer.SendAsync(PrepareEmail(owner, upcomingRecerts, overdueRecerts), emailConnection, new CancellationToken());
                        emailsSent++;
                    }
                    await SetOwnerLastCheck(owner);
                }
            }
            catch(Exception exception)
            {
                Log.WriteError(LogMessageTitle, $"Checking owners for upcoming recertifications leads to exception.", exception);
            }
            return emailsSent;
        }

        private async Task InitEnv()
        {
            globCheckParams = System.Text.Json.JsonSerializer.Deserialize<RecertCheckParams>(globalConfig.RecCheckParams);
            List<Ldap> connectedLdaps = apiConnectionMiddlewareServer.SendQueryAsync<List<Ldap>>(AuthQueries.getLdapConnections).Result;
            foreach (Ldap currentLdap in connectedLdaps)
            {
                if (currentLdap.IsInternal() && currentLdap.HasGroupHandling())
                {
                    groups.AddRange(await currentLdap.GetAllInternalGroups());
                }
            }
            uiUsers = await apiConnectionMiddlewareServer.SendQueryAsync<List<UiUser>>(AuthQueries.getUsers);
            owners = await apiConnectionMiddlewareServer.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwners);
        }

        private bool IsCheckTime(FwoOwner owner)
        {
            RecertCheckParams checkParams = (owner.RecertCheckParamString != null && owner.RecertCheckParamString != "" ? 
                System.Text.Json.JsonSerializer.Deserialize<RecertCheckParams>(owner.RecertCheckParamString) : 
                globCheckParams) ?? throw new ArgumentException("Config Parameters not set.");
            DateTime lastCheck = owner.LastRecertCheck ?? DateTime.MinValue;
            var nextCheck = checkParams.RecertCheckInterval switch
            {
                SchedulerInterval.Days => lastCheck.AddDays(checkParams.RecertCheckOffset),
                SchedulerInterval.Weeks => CalcForWeeks(lastCheck, checkParams),
                SchedulerInterval.Months => CalcForMonths(lastCheck, checkParams),
                _ => throw new NotSupportedException("Time interval is not supported."),
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
                nextCheck = lastCheck.AddDays(checkParams.RecertCheckOffset * 7);
            }
            else
            {
                nextCheck = lastCheck.AddDays((checkParams.RecertCheckOffset - 1) * 7 + 1);
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

        private async Task<List<Rule>> GenerateRecertificationReport(ApiConnection apiConnection, FwoOwner owner, bool overdueOnly)
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

                ReportData reportData = (await ReportGenerator.Generate(new ReportTemplate("", reportParams), apiConnection, userConfig, DefaultInit.DoNothing))?.ReportData ?? new();

                foreach (var management in reportData.ManagementData)
                {
                    foreach (var device in management.Devices.Where(d => d.ContainsRules()))
                    {
                        foreach (var rbLink in device.RulebaseLinks!)
                        {
                            foreach (var rule in management.Rulebases[rbLink.NextRulebaseId].Rules)
                            {
                                rule.Metadata.UpdateRecertPeriods(owner.RecertInterval ?? globalConfig.RecertificationPeriod, 0);
                                rule.DeviceName = device.Name ?? "";
                                rules.Add(rule);
                            }
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

        private MailData PrepareEmail(FwoOwner owner, List<Rule> upcomingRecerts, List<Rule> overdueRecerts)
        {
            string subject = globalConfig.RecCheckEmailSubject + " " + owner.Name;
            string body = "";
            if(upcomingRecerts.Count > 0)
            {
                body += globalConfig.RecCheckEmailUpcomingText + "\r\n\r\n";
                foreach(var rule in upcomingRecerts)
                {
                    body += PrepareLine(rule);
                }
            }
            if(overdueRecerts.Count > 0)
            {
                body += globalConfig.RecCheckEmailOverdueText + "\r\n\r\n";
                foreach(var rule in overdueRecerts)
                {
                    body += PrepareLine(rule);
                }
            }
            return new MailData(CollectEmailAddresses(owner), subject, body);
        }

        private static string PrepareLine(Rule rule)
        {
            Recertification? nextRecert = rule.Metadata.RuleRecertification.FirstOrDefault(x => x.RecertDate == null);
            return (nextRecert != null && nextRecert.NextRecertDate != null ? DateOnly.FromDateTime((DateTime)nextRecert.NextRecertDate) : "") + ": " 
                    + rule.RulebaseName + ": " + rule.Name + ":" + rule.Uid + "\r\n\r\n";  // link ?
        }

        private List<string> CollectEmailAddresses(FwoOwner owner)
        {
            if(globalConfig.UseDummyEmailAddress)
            {
                return [globalConfig.DummyEmailAddress];
            }
            List<string> tos = [];
            List<string> userDns = [];
            if(owner.Dn != "")
            {
                userDns.Add(owner.Dn);
            }
            GroupGetReturnParameters? ownerGroup = groups.FirstOrDefault(x => x.GroupDn == owner.GroupDn);
            if(ownerGroup != null)
            {
                userDns.AddRange(ownerGroup.Members);
            }
            foreach(var userDn in userDns)
            {
                UiUser? uiuser = uiUsers.FirstOrDefault(x => x.Dn == userDn);
                if(uiuser != null && uiuser.Email != null && uiuser.Email != "")
                {
                    tos.Add(uiuser.Email);
                }
            }
            return tos;
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
