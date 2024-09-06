using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Api.Data;
using FWO.Config.File;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Logging;
using FWO.Mail;
using FWO.Encryption;
using FWO.Middleware.RequestParameters;
using FWO.Report;
using FWO.Report.Filter;


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
                    Log.WriteError("CheckRecertifications", $"Could not decrypt mailserver password.", exception);				
                }
                EmailConnection emailConnection = new(globalConfig.EmailServerAddress, globalConfig.EmailPort,
                    globalConfig.EmailTls, globalConfig.EmailUser, decryptedSecret, globalConfig.EmailSenderAddress);
                MailKitMailer mailer = new(emailConnection);
                JwtWriter jwtWriter = new(ConfigFile.JwtPrivateKey);
                ApiConnection apiConnectionReporter = new GraphQlApiConnection(ConfigFile.ApiServerUri ?? throw new Exception("Missing api server url on startup."), jwtWriter.CreateJWTReporterViewall());

                foreach(var owner in owners)
                {
                    if(IsCheckTime(owner))
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
            }
            catch(Exception exception)
            {
                Log.WriteError("Recertification Check", $"Checking owners for upcoming recertifications leads to exception.", exception);
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
                    groups.AddRange(currentLdap.GetAllInternalGroups());
                }
            }
            uiUsers = await apiConnectionMiddlewareServer.SendQueryAsync<List<UiUser>>(AuthQueries.getUsers);
            owners = await apiConnectionMiddlewareServer.SendQueryAsync<List<FwoOwner>>(OwnerQueries.getOwners);
        }

        private bool IsCheckTime(FwoOwner owner)
        {
            RecertCheckParams checkParams = (owner.RecertCheckParamString != null && owner.RecertCheckParamString != "" ? 
                System.Text.Json.JsonSerializer.Deserialize<RecertCheckParams>(owner.RecertCheckParamString) : 
                globCheckParams) ?? throw new Exception("Config Parameters not set.");
            DateTime lastCheck = owner.LastRecertCheck ?? DateTime.MinValue;
            DateTime nextCheck;

            switch (checkParams.RecertCheckInterval)
            {
                case Interval.Days:
                    nextCheck = lastCheck.AddDays(checkParams.RecertCheckOffset);
                break;
                case Interval.Weeks:
                    if(checkParams.RecertCheckWeekday == null)
                    {
                        nextCheck = lastCheck.AddDays(checkParams.RecertCheckOffset * 7);
                    }
                    else
                    {
                        nextCheck = lastCheck.AddDays((checkParams.RecertCheckOffset - 1) * 7 + 1);
                        int count = 0;
                        while(nextCheck.DayOfWeek != (DayOfWeek)checkParams.RecertCheckWeekday && count < 6)
                        {
                            nextCheck = nextCheck.AddDays(1);
                            count++;
                        }
                    }
                break;
                case Interval.Months:
                    if(checkParams.RecertCheckDayOfMonth == null)
                    {
                        nextCheck = lastCheck.AddMonths(checkParams.RecertCheckOffset);
                    }
                    else
                    {
                        nextCheck = lastCheck.AddMonths(checkParams.RecertCheckOffset - 1);
                        nextCheck = nextCheck.AddDays(1);
                        int count = 0;
                        while(nextCheck.Day != (int)checkParams.RecertCheckDayOfMonth && count < 30)
                        {
                            nextCheck = nextCheck.AddDays(1);
                            count++;
                        }
                        if(nextCheck.Day != (int)checkParams.RecertCheckDayOfMonth)
                        {
                            // missed the day because or month change: set to first of following month
                            nextCheck = nextCheck.AddDays(1 - nextCheck.Day);
                        }
                    }
                break;
                default:
                    throw new NotSupportedException("Time interval is not supported.");
            }

            if(nextCheck <= DateTime.Today)
            {
                return true;
            }
            return false;
        }

        private async Task<List<Rule>> GenerateRecertificationReport(ApiConnection apiConnection, FwoOwner owner, bool overdueOnly)
        {
            List<Rule> rules = [];
            try
            {
                CancellationToken token = new ();
                UserConfig userConfig = new (globalConfig);

                DeviceFilter deviceFilter = new()
                {
                    Managements = await apiConnection.SendQueryAsync<List<ManagementSelect>>(DeviceQueries.getDevicesByManagement)
                };
                deviceFilter.applyFullDeviceSelection(true);

                ReportParams reportParams = new((int)ReportType.Recertification, deviceFilter)
                {
                    RecertFilter = new()
                    {
                        RecertOwnerList = [owner.Id],
                        RecertificationDisplayPeriod = globalConfig.RecertificationNoticePeriod
                    }
                };
                ReportBase? currentReport = ReportBase.ConstructReport(new ReportTemplate("", reportParams), userConfig);

                ReportData reportData = new ();

                await currentReport.Generate(int.MaxValue, apiConnection,
                rep =>
                {
                    reportData.ManagementData = rep.ManagementData;
                    return Task.CompletedTask;
                }, token);

                foreach (var management in reportData.ManagementData)
                {
                    foreach (var device in management.Devices)
                    {
                        if (device.ContainsRules())
                        {
                            foreach (var rule in device.Rules!)
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
                Log.WriteError("Recertification Check", $"Report for owner {owner.Name} leads to exception.", exception);
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
                    + rule.DeviceName + ": " + rule.Name + ":" + rule.Uid + "\r\n\r\n";  // link ?
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
