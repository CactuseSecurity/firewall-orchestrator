using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Api.Data;
using FWO.Config.Api;
using FWO.Config.Api.Data;
using FWO.Logging;
using FWO.Mail;
using FWO.Middleware.RequestParameters;


namespace FWO.Middleware.Server
{
    /// <summary>
    /// Recertification check class
    /// </summary>
    public class RecertCheck
    {
        private readonly ApiConnection apiConnection;
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
            this.apiConnection = apiConnection;
            this.globalConfig = globalConfig;
        }

        /// <summary>
        /// Recertification check
        /// </summary>
        public async Task CheckRecertifications()
        {
            try
            {
                if(globalConfig.RecCheckActive)
                {
                    await InitEnv();
                    EmailConnection emailConnection = new EmailConnection(globalConfig.EmailServerAddress, globalConfig.EmailPort,
                        globalConfig.EmailTls, globalConfig.EmailUser, globalConfig.EmailPassword, globalConfig.EmailSenderAddress);
                    MailKitMailer mailer = new MailKitMailer(emailConnection);

                    foreach(var owner in owners)
                    {
                        if(isCheckTime(owner))
                        {
                            // new task?
                            List<Rule> upcomingRecerts = new List<Rule>();  // todo: get with view_recert_upcoming_rules?
                            List<Rule> overdueRecerts = new List<Rule>(); // todo: get with view_recert_overdue_rules

                            if(upcomingRecerts.Count > 0 || overdueRecerts.Count > 0)
                            {
                                await mailer.SendAsync(prepareEmail(owner, upcomingRecerts, overdueRecerts), emailConnection, new CancellationToken());
                            }
                            await setOwnerLastCheck(owner);
                        }
                    }
                }
            }
            catch(Exception exception)
            {
                Log.WriteError("Recertification Check", $"Checking owners for upcoming recertifications leads to exception.", exception);
            }
        }

        private async Task InitEnv()
        {
            globCheckParams = System.Text.Json.JsonSerializer.Deserialize<RecertCheckParams>(globalConfig.RecCheckParams);
            List<Ldap> connectedLdaps = apiConnection.SendQueryAsync<List<Ldap>>(AuthQueries.getLdapConnections).Result;
            foreach (Ldap currentLdap in connectedLdaps)
            {
                if (currentLdap.IsInternal() && currentLdap.HasGroupHandling())
                {
                    groups.AddRange(currentLdap.GetAllInternalGroups());
                }
            }
            uiUsers = await apiConnection.SendQueryAsync<List<UiUser>>(FWO.Api.Client.Queries.AuthQueries.getUsers);
            owners = await apiConnection.SendQueryAsync<List<FwoOwner>>(FWO.Api.Client.Queries.OwnerQueries.getOwners);
        }

        private bool isCheckTime(FwoOwner owner)
        {
            RecertCheckParams checkParams = (owner.RecertCheckParams != null && owner.RecertCheckParams != "" ? 
                System.Text.Json.JsonSerializer.Deserialize<RecertCheckParams>(owner.RecertCheckParams) : 
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
                        nextCheck = lastCheck.AddDays((checkParams.RecertCheckOffset-1) * 7 + 1);
                        int count = 0;
                        while(nextCheck.DayOfWeek != (DayOfWeek)checkParams.RecertCheckWeekday && count < 6)
                        {
                            nextCheck.AddDays(1);
                            count++;
                        }
                    }
                break;
                case Interval.Months:
                    nextCheck = lastCheck.AddMonths(checkParams.RecertCheckOffset);
                    if(checkParams.RecertCheckDayOfMonth != null)
                    {
                        nextCheck.AddDays((int)checkParams.RecertCheckDayOfMonth - nextCheck.Day);
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

        private MailData prepareEmail(FwoOwner owner, List<Rule> upcomingRecerts, List<Rule> overdueRecerts)
        {
            string subject = globalConfig.RecCheckEmailSubject + " " + owner.Name;
            string body = "";
            if(upcomingRecerts.Count > 0)
            {
                body += globalConfig.RecCheckEmailUpcomingText + "/n";
                foreach(var rule in upcomingRecerts)
                {
                    body += rule.Name + ": " + rule.DeviceName + ":" + rule.Uid + "/n";  // next recert date // link ?
                }
            }
            if(overdueRecerts.Count > 0)
            {
                body += globalConfig.RecCheckEmailOverdueText + "/n";
                foreach(var rule in overdueRecerts)
                {
                    body += rule.Name + ": " + rule.DeviceName + ":" + rule.Uid + "/n";  // next recert date // link ?
                }
            }
            return new MailData(collectEmailAddresses(owner), subject, body);
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
            await apiConnection.SendQueryAsync<object>(FWO.Api.Client.Queries.OwnerQueries.setOwnerLastCheck, Variables);
        }
    }
}
