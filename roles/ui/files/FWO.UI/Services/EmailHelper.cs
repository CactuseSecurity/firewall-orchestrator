using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Api.Data;
using FWO.Config.Api;
using FWO.Middleware.Client;
using FWO.Mail;
using System.Text.Json.Serialization;
using Newtonsoft.Json;


namespace FWO.Ui.Services
{
    public class EmailActionParams
    {
        [JsonProperty("to"), JsonPropertyName("to")]
        public EmailRecipientOption RecipientTo { get; set; } = EmailRecipientOption.Requester;

        [JsonProperty("cc"), JsonPropertyName("cc")]
        public EmailRecipientOption? RecipientCC { get; set; }

        [JsonProperty("subject"), JsonPropertyName("subject")]
        public string Subject { get; set; } = "";

        [JsonProperty("body"), JsonPropertyName("body")]
        public string Body { get; set; } = "";
    }

    public class EmailHelper
    {
        private readonly ApiConnection apiConnection;
        private readonly MiddlewareClient middlewareClient;
        private readonly UserConfig userConfig;
        private readonly Action<Exception?, string, string, bool> displayMessageInUi;
        private List<UserGroup> ownerGroups = [];
        private List<UiUser> uiUsers = [];
        private string? ScopedUserTo;
        private string? ScopedUserCc;



        public EmailHelper(ApiConnection apiConnection, MiddlewareClient middlewareClient, UserConfig userConfig, Action<Exception?, string, string, bool> displayMessageInUi)
        {
            this.apiConnection = apiConnection;
            this.middlewareClient = middlewareClient;
            this.userConfig = userConfig;
            this.displayMessageInUi = displayMessageInUi;
        }

        public async Task Init(string? scopedUserTo = null, string? scopedUserCc = null)
        {
            ownerGroups = await GroupAccess.GetGroupsFromInternalLdap(middlewareClient, userConfig, displayMessageInUi, true);
            uiUsers = await apiConnection.SendQueryAsync<List<UiUser>>(AuthQueries.getUserEmails);
            ScopedUserTo = scopedUserTo;
            ScopedUserCc = scopedUserCc;
        }

        public List<UserGroup> GetOwnerGroups()
        {
            return ownerGroups;
        }

        public async Task<bool> SendEmailToOwnerResponsibles(FwoOwner owner, string subject, string body)
        {
            List<string>? requester = userConfig.ModReqEmailRequesterInCc ? new() { GetEmailAddress(userConfig.User.Dn) } : null;
            return await SendEmail(GetRecipients(userConfig.ModReqEmailReceiver, null, owner, null), subject, body, requester);
        }

        public async Task<bool> SendOwnerEmailFromAction(EmailActionParams emailActionParams, WfStatefulObject statefulObject, FwoOwner? owner)
        {
            List<string> tos = GetRecipients(emailActionParams.RecipientTo, statefulObject, owner, ScopedUserTo);
            List<string>? ccs = emailActionParams.RecipientCC != null ? GetRecipients((EmailRecipientOption)emailActionParams.RecipientCC, statefulObject, owner, ScopedUserCc) : null;
            return await SendEmail(tos, emailActionParams.Subject, emailActionParams.Body, ccs);
        }

        public async Task<bool> SendUserEmailFromAction(EmailActionParams emailActionParams, WfStatefulObject statefulObject, string userGrpDn)
        {
            return await SendEmail(CollectEmailAddressesFromUserOrGroup(userGrpDn), emailActionParams.Subject, emailActionParams.Body);
        }

        private async Task<bool> SendEmail(List<string> tos, string subject, string body, List<string>? ccs = null)
        {
            EmailConnection emailConnection = new(userConfig.EmailServerAddress, userConfig.EmailPort,
                userConfig.EmailTls, userConfig.EmailUser, userConfig.EmailPassword, userConfig.EmailSenderAddress);
            MailKitMailer mailer = new(emailConnection);
            tos = tos.Where(t => t != "").ToList();
            ccs = ccs?.Where(c => c != "").ToList();
            return await mailer.SendAsync(new MailData(tos, subject, body, null, null, null, null, null, ccs), emailConnection, new CancellationToken(), true);
        }

        private List<string> GetRecipients(EmailRecipientOption recipientOption, WfStatefulObject? statefulObject, FwoOwner? owner, string? scopedUser)
        {
            List<string> recipients = [];
            switch (recipientOption)
            {
                case EmailRecipientOption.CurrentHandler:
                    recipients.Add(GetEmailAddress(statefulObject?.CurrentHandler?.Dn));
                    break;
                case EmailRecipientOption.RecentHandler:
                    recipients.Add(GetEmailAddress(statefulObject?.RecentHandler?.Dn));
                    break;
                case EmailRecipientOption.AssignedGroup:
                    recipients.AddRange(CollectEmailAddressesFromUserOrGroup(statefulObject?.AssignedGroup));
                    break;
                case EmailRecipientOption.OwnerMainResponsible:
                    recipients.Add(GetEmailAddress(owner?.Dn));
                    break;
                case EmailRecipientOption.AllOwnerResponsibles:
                    recipients.AddRange(CollectEmailAddressesFromOwner(owner));
                    break;
                case EmailRecipientOption.OwnerGroupOnly:
                    recipients.AddRange(GetAddressesFromGroup(owner?.GroupDn));
                    break;
                case EmailRecipientOption.Requester:
                case EmailRecipientOption.Approver:
                case EmailRecipientOption.LastCommenter:
                    recipients.Add(GetEmailAddress(scopedUser));
                    break;
                case EmailRecipientOption.MainResponsibleOwnerEmpty:
                    List<string> owners = CollectEmailAddressesFromOwner(owner);

                    if (owner is null || owners.Count == 0 || owners.All(_ => string.IsNullOrEmpty(_)))
                    {
                        recipients.Add(GetEmailAddress(owner?.Dn));
                    }
                    else
                    {
                        recipients.AddRange(owners.Where(_ => !string.IsNullOrEmpty(_)));
                    }

                    break;
                default:
                    break;
            }
            return recipients;
        }

        public List<string> GetOwnerMainResponsibleRecipients(List<UserGroup> owners)
        {
            List<string> recipients = [];

            foreach (UserGroup owner in owners)
            {
                if (owner is null || string.IsNullOrWhiteSpace(owner.Dn))
                {
                    continue;
                }
                recipients.Add(GetEmailAddress(owner.Dn));
            }

            return recipients;
        }

        private List<string> CollectEmailAddressesFromOwner(FwoOwner? owner)
        {
            List<string> tos = new() { GetEmailAddress(owner?.Dn) };
            tos.AddRange(GetAddressesFromGroup(owner?.GroupDn));
            return tos;
        }

        private List<string> CollectEmailAddressesFromUserOrGroup(string? dn)
        {
            List<string> tos = new() { GetEmailAddress(dn) };
            tos.AddRange(GetAddressesFromGroup(dn));
            return tos;
        }

        private List<string> GetAddressesFromGroup(string? groupDn)
        {
            List<string> tos = [];
            UserGroup? ownerGroup = ownerGroups.FirstOrDefault(x => x.Dn == groupDn);
            if (ownerGroup != null)
            {
                foreach (var user in ownerGroup.Users)
                {
                    tos.Add(GetEmailAddress(user.Dn));
                }
            }
            return tos;
        }

        private string GetEmailAddress(string? dn)
        {
            if (userConfig.UseDummyEmailAddress)
            {
                return userConfig.DummyEmailAddress;
            }
            UiUser? uiuser = uiUsers.FirstOrDefault(x => x.Dn == dn);
            if (uiuser != null && uiuser.Email != null && uiuser.Email != "")
            {
                return uiuser.Email;
            }
            return "";
        }
    }
}
