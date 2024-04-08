﻿using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.GlobalConstants;
using FWO.Api.Data;
using FWO.Config.File;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Logging;
using FWO.Mail;
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
        private GlobalConfig globalConfig;
        private List<GroupGetReturnParameters> groups = new List<GroupGetReturnParameters>();
        private List<UiUser> uiUsers = new List<UiUser>();
        private RecertCheckParams? globCheckParams;
        private List<FwoOwner> owners = new List<FwoOwner>();

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
                EmailConnection emailConnection = new EmailConnection(globalConfig.EmailServerAddress, globalConfig.EmailPort,
                    globalConfig.EmailTls, globalConfig.EmailUser, globalConfig.EmailPassword, globalConfig.EmailSenderAddress);
                MailKitMailer mailer = new MailKitMailer(emailConnection);
                JwtWriter jwtWriter = new JwtWriter(ConfigFile.JwtPrivateKey);
                ApiConnection apiConnectionReporter = new GraphQlApiConnection(ConfigFile.ApiServerUri ?? throw new Exception("Missing api server url on startup."), jwtWriter.CreateJWTReporterViewall());

                foreach(var owner in owners)
                {
                    if(isCheckTime(owner))
                    {
                        // todo: refine handling
                        List<Rule> upcomingRecerts = await generateRecertificationReport(apiConnectionReporter, owner, false);
                        List<Rule> overdueRecerts = new List<Rule>(); // await generateRecertificationReport(apiConnectionReporter, owner, true);

                        if(upcomingRecerts.Count > 0 || overdueRecerts.Count > 0)
                        {
                            await mailer.SendAsync(prepareEmail(owner, upcomingRecerts, overdueRecerts), emailConnection, new CancellationToken());
                            emailsSent++;
                        }
                        await setOwnerLastCheck(owner);
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
            uiUsers = await apiConnectionMiddlewareServer.SendQueryAsync<List<UiUser>>(FWO.Api.Client.Queries.AuthQueries.getUsers);
            owners = await apiConnectionMiddlewareServer.SendQueryAsync<List<FwoOwner>>(FWO.Api.Client.Queries.OwnerQueries.getOwners);
        }

        private bool isCheckTime(FwoOwner owner)
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

        private async Task<List<Rule>> generateRecertificationReport(ApiConnection apiConnection, FwoOwner owner, bool overdueOnly)
        {
            List<Rule> rules = new List<Rule>();
            try
            {
                CancellationToken token = new CancellationToken();
                UserConfig userConfig = new UserConfig(globalConfig);

                DeviceFilter deviceFilter = new DeviceFilter();
                deviceFilter.Managements = await apiConnection.SendQueryAsync<List<ManagementSelect>>(DeviceQueries.getDevicesByManagement);
                deviceFilter.applyFullDeviceSelection(true);

                ReportParams reportParams = new ReportParams((int) ReportType.Recertification, deviceFilter);
                reportParams.RecertFilter = new RecertFilter()
                {
                    RecertOwnerList = new List<int>() { owner.Id },
                    RecertificationDisplayPeriod = globalConfig.RecertificationNoticePeriod
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

        private MailData prepareEmail(FwoOwner owner, List<Rule> upcomingRecerts, List<Rule> overdueRecerts)
        {
            string subject = globalConfig.RecCheckEmailSubject + " " + owner.Name;
            string body = "";
            if(upcomingRecerts.Count > 0)
            {
                body += globalConfig.RecCheckEmailUpcomingText + "\r\n\r\n";
                foreach(var rule in upcomingRecerts)
                {
                    body += prepareLine(rule);
                }
            }
            if(overdueRecerts.Count > 0)
            {
                body += globalConfig.RecCheckEmailOverdueText + "\r\n\r\n";
                foreach(var rule in overdueRecerts)
                {
                    body += prepareLine(rule);
                }
            }
            return new MailData(collectEmailAddresses(owner), subject, body);
        }

        private string prepareLine(Rule rule)
        {
            Recertification? nextRecert = rule.Metadata.RuleRecertification.FirstOrDefault(x => x.RecertDate == null);
            return (nextRecert != null && nextRecert.NextRecertDate != null ? DateOnly.FromDateTime((DateTime)nextRecert.NextRecertDate) : "") + ": " 
                    + rule.DeviceName + ": " + rule.Name + ":" + rule.Uid + "\r\n\r\n";  // link ?
        }

        private List<string> collectEmailAddresses(FwoOwner owner)
        {
            List<string> tos = new List<string>();
            List<string> userDns = new List<string>();
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

        private async Task setOwnerLastCheck(FwoOwner owner)
        {
            var Variables = new
            {
                id = owner.Id,
                lastRecertCheck = DateTime.Now
            };
            await apiConnectionMiddlewareServer.SendQueryAsync<object>(FWO.Api.Client.Queries.OwnerQueries.setOwnerLastCheck, Variables);
        }
    }
}
