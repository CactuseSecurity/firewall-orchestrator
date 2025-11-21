using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Mail;
using FWO.Middleware.Client;
using Newtonsoft.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Http;
using System.Text.RegularExpressions;
using FWO.Basics;
using FWO.Logging;

namespace FWO.Services
{
    public class EmailActionParams
    {
        [JsonProperty("to"), JsonPropertyName("to")]
        public EmailRecipientOption RecipientTo { get; set; } = EmailRecipientOption.None;

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
        private readonly MiddlewareClient? middlewareClient;
        private readonly UserConfig userConfig;
        private readonly Action<Exception?, string, string, bool> displayMessageInUi;
        private readonly bool useInMwServer = false;
        private List<UserGroup> ownerGroups = [];
        private List<UiUser> uiUsers = [];
        private string? ScopedUserTo;
        private string? ScopedUserCc;


        public EmailHelper(ApiConnection apiConnection, MiddlewareClient? middlewareClient, UserConfig userConfig, Action<Exception?, string, string, bool> displayMessageInUi, List<UserGroup>? ownerGroups = null, bool useInMwServer = false)
        {
            this.apiConnection = apiConnection;
            this.middlewareClient = middlewareClient;
            this.userConfig = userConfig;
            this.displayMessageInUi = displayMessageInUi;
            this.useInMwServer = useInMwServer;
            this.ownerGroups = ownerGroups ?? [];
        }

        public async Task Init(string? scopedUserTo = null, string? scopedUserCc = null)
        {
            if (!useInMwServer && middlewareClient != null)
            {
                ownerGroups = await GroupAccess.GetGroupsFromInternalLdap(middlewareClient, userConfig, displayMessageInUi, true);
            }
            uiUsers = await apiConnection.SendQueryAsync<List<UiUser>>(AuthQueries.getUserEmails);
            ScopedUserTo = scopedUserTo;
            ScopedUserCc = scopedUserCc;
        }

        public async Task<bool> SendEmailToOwnerResponsibles(FwoOwner owner, string subject, string body, EmailRecipientOption recOpt, bool reqInCc = false)
        {
            List<string>? requester = reqInCc ? new() { GetEmailAddress(userConfig.User.Dn) } : null;
            return await SendEmail(GetRecipients(recOpt, null, owner, null, null), subject, body, requester);
        }

        public async Task<bool> SendOwnerEmailFromAction(EmailActionParams emailActionParams, WfStatefulObject statefulObject, FwoOwner? owner)
        {
            List<string> tos = GetRecipients(emailActionParams.RecipientTo, statefulObject, owner, ScopedUserTo, null);
            List<string>? ccs = emailActionParams.RecipientCC != null ? GetRecipients((EmailRecipientOption)emailActionParams.RecipientCC, statefulObject, owner, ScopedUserCc, null) : null;
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
            tos = [.. tos.Where(t => t != "")];
            ccs = ccs?.Where(c => c != "").ToList();
            return await MailKitMailer.SendAsync(new MailData(tos, subject) { Body = body, Cc = ccs ?? [] }, emailConnection, true, new CancellationToken());
        }

        public List<string> GetRecipients(EmailRecipientOption recipientOption, WfStatefulObject? statefulObject, FwoOwner? owner, string? scopedUser, List<string>? otherAddresses)
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
                case EmailRecipientOption.FallbackToMainResponsibleIfOwnerGroupEmpty:
                    if (owner is null)
                        break;

                    List<string> ownerGroupAdresses = GetAddressesFromGroup(owner?.GroupDn);

                    if (ownerGroupAdresses.Count == 0)
                    {
                        recipients.Add(GetEmailAddress(owner?.Dn));
                    }
                    else
                    {
                        recipients.AddRange(ownerGroupAdresses);
                    }
                    break;
                case EmailRecipientOption.OtherAddresses:
                    if (otherAddresses != null)
                    {
                        recipients.AddRange(otherAddresses);
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

        /// <summary>
        /// Split email addresses from string to list
        /// </summary>
        /// <param name="addresslist"></param>
        /// <returns></returns>
        public static List<string> SplitAddresses(string addresslist)
        {
            string[] separatingStrings = [",", ";", "|"];
            return [.. addresslist.Split(separatingStrings, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];
        }

        private List<string> CollectEmailAddressesFromOwner(FwoOwner? owner)
        {
            List<string> tos = [GetEmailAddress(owner?.Dn), .. GetAddressesFromGroup(owner?.GroupDn)];
            return tos;
        }

        private List<string> CollectEmailAddressesFromUserOrGroup(string? dn)
        {
            List<string> tos = [GetEmailAddress(dn), .. GetAddressesFromGroup(dn)];
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

        public static List<string> CollectRecipientsFromConfig(UserConfig userConfig, string configValue)
        {
            if (userConfig.UseDummyEmailAddress)
            {
                return [userConfig.DummyEmailAddress];
            }
            string[] separatingStrings = [",", ";", "|"];
            return [.. configValue.Split(separatingStrings, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];
        }

        public static FormFile? CreateAttachment(string? content, string fileFormat, string subject)
        {
            if (content != null)
            {
                string fileName = ConstructFileName(subject, fileFormat);

                MemoryStream memoryStream;
                string contentType;

                if (fileFormat == GlobalConst.kPdf)
                {
                    memoryStream = new(Convert.FromBase64String(content));
                    contentType = "application/octet-stream";
                }
                else
                {
                    memoryStream = new(System.Text.Encoding.UTF8.GetBytes(content));
                    contentType = $"application/{fileFormat}";
                }

                return new(memoryStream, 0, memoryStream.Length, "FWO-Report-Attachment", fileName)
                {
                    Headers = new HeaderDictionary(),
                    ContentType = contentType
                };
            }
            return null;
        }

        private static string ConstructFileName(string input, string fileFormat)
        {
            try
            {
                Regex regex = new(@"\s", RegexOptions.None, TimeSpan.FromMilliseconds(500));
                return $"{regex.Replace(input, "")}_{DateTime.Now.ToUniversalTime().ToString("yyyy-MM-ddTHH-mm-ssK")}.{fileFormat}";
            }
            catch (RegexMatchTimeoutException)
            {
                Log.WriteWarning("Construct File Name", "Timeout when constructing file name. Taking input.");
                return input;
            }
        }
    }
}
