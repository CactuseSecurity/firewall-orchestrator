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
    public enum EmailRecipientOption
    {
        CurrentHandler = 1,
        RecentHandler = 2,
        AssignedGroup = 3,
        OwnerMainResponsible = 10, 
        AllOwnerResponsibles = 11,
        Requester = 20,
        Approver = 21,
        LastCommenter = 30
        // AllCommenters = 31
    }

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
        private List<UserGroup> ownerGroups = new ();
        private List<UiUser> uiUsers = new ();
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

        public async Task<bool> SendEmailToOwnerResponsibles(FwoOwner owner, string subject, string body, bool requesterInCc)
        {
            List<string>? requester = requesterInCc ? new() { GetEmailAddress(userConfig.User.Dn) } : null;
            return await SendEmail(CollectEmailAddressesFromOwner(owner), subject, body, requester);
        }

        public async Task<bool> SendEmailFromAction(EmailActionParams emailActionParams, RequestStatefulObject statefulObject, FwoOwner? owner)
        {
            List<string> tos = GetRecipients(emailActionParams.RecipientTo, statefulObject, owner, ScopedUserTo);
            List<string>? ccs = emailActionParams.RecipientCC != null ? GetRecipients((EmailRecipientOption)emailActionParams.RecipientCC, statefulObject, owner, ScopedUserCc) : null;
            return await SendEmail(tos, emailActionParams.Subject, emailActionParams.Body, ccs);
        }

        private async Task<bool> SendEmail(List<string> tos, string subject, string body, List<string>? ccs = null)
        {
            EmailConnection emailConnection = new (userConfig.EmailServerAddress, userConfig.EmailPort,
                userConfig.EmailTls, userConfig.EmailUser, userConfig.EmailPassword, userConfig.EmailSenderAddress);
            MailKitMailer mailer = new (emailConnection);
            tos = tos.Where(t => t != "").ToList();
            ccs = ccs?.Where(c => c != "").ToList();
            return await mailer.SendAsync(new MailData(tos, subject, body, null, null, null, null, null, ccs), emailConnection, new CancellationToken(), true);
        }

        private List<string> GetRecipients(EmailRecipientOption recipientOption, RequestStatefulObject statefulObject, FwoOwner? owner, string? scopedUser)
        {
            List<string> recipients = new();
            switch(recipientOption)
            {
                case EmailRecipientOption.CurrentHandler:
                    recipients.Add(GetEmailAddress(statefulObject.CurrentHandler?.Dn));
                    break;
                case EmailRecipientOption.RecentHandler:
                    recipients.Add(GetEmailAddress(statefulObject.RecentHandler?.Dn));
                    break;
                case EmailRecipientOption.AssignedGroup:
                    recipients.AddRange(GetAddressesFromGroup(statefulObject.AssignedGroup));
                    break;
                case EmailRecipientOption.OwnerMainResponsible:
                    recipients.Add(GetEmailAddress(owner?.Dn));
                    break;
                case EmailRecipientOption.AllOwnerResponsibles:
                    recipients.AddRange(CollectEmailAddressesFromOwner(owner));
                    break;
                case EmailRecipientOption.Requester:
                case EmailRecipientOption.Approver:
                case EmailRecipientOption.LastCommenter:
                    recipients.Add(GetEmailAddress(scopedUser));
                    break;
                default:
                    break;
            }
            return recipients;
        }

        private List<string> CollectEmailAddressesFromOwner(FwoOwner? owner)
        {
            List<string> tos = new() { GetEmailAddress(owner?.Dn) };
            tos.AddRange(GetAddressesFromGroup(owner?.GroupDn));
            return tos;
        }

        private List<string> GetAddressesFromGroup(string? groupDn)
        {
            List<string> tos = new();
            UserGroup? ownerGroup = ownerGroups.FirstOrDefault(x => x.Dn == groupDn);
            if(ownerGroup != null)
            {
                foreach(var user in ownerGroup.Users)
                {
                    tos.Add(GetEmailAddress(user.Dn));
                }
            }
            return tos;
        }

        private string GetEmailAddress(string? dn)
        {
            UiUser? uiuser = uiUsers.FirstOrDefault(x => x.Dn == dn);
            if(uiuser != null && uiuser.Email != null && uiuser.Email != "")
            {
                return uiuser.Email;
            }
            return "";
        }
    }
}
