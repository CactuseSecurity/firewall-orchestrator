using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.Workflow;
using FWO.Mail;
using FWO.Middleware.Client;
using System;
using Microsoft.AspNetCore.Http;
using System.Text.RegularExpressions;
using System.Linq;
using FWO.Basics;
using FWO.Logging;

namespace FWO.Services
{
    public class EmailHelper
    {
        private readonly ApiConnection apiConnection;
        private readonly MiddlewareClient? middlewareClient;
        private readonly UserConfig userConfig;
        private readonly Action<Exception?, string, string, bool> displayMessageInUi;
        private readonly bool useInMwServer = false;
        private readonly IWorkflowRecipientResolver? recipientResolver;
        private List<UserGroup> ownerGroups = [];
        private List<OwnerResponsibleType> ownerResponsibleTypes = [];
        private List<UiUser> uiUsers = [];
        private string? ScopedUserTo;
        private string? ScopedUserCc;
        private string? ScopedUserBcc;
        private string? ScopedUserEmailTo;
        private string? ScopedUserEmailCc;
        private string? ScopedUserEmailBcc;


        public EmailHelper(ApiConnection apiConnection, MiddlewareClient? middlewareClient, UserConfig userConfig, Action<Exception?, string, string, bool> displayMessageInUi,
            List<UserGroup>? ownerGroups = null, bool useInMwServer = false, IWorkflowRecipientResolver? recipientResolver = null)
        {
            this.apiConnection = apiConnection;
            this.middlewareClient = middlewareClient;
            this.userConfig = userConfig;
            this.displayMessageInUi = displayMessageInUi;
            this.useInMwServer = useInMwServer;
            this.ownerGroups = ownerGroups ?? [];
            this.recipientResolver = recipientResolver;
        }

        public virtual async Task Init(string? scopedUserTo = null, string? scopedUserCc = null, string? scopedUserBcc = null,
            string? scopedUserEmailTo = null, string? scopedUserEmailCc = null, string? scopedUserEmailBcc = null)
        {
            if (!useInMwServer && middlewareClient != null)
            {
                ownerGroups = await GroupAccess.GetGroupsFromInternalLdap(middlewareClient, userConfig, displayMessageInUi, true);
            }
            try
            {
                ownerResponsibleTypes = await apiConnection.SendQueryAsync<List<OwnerResponsibleType>>(OwnerQueries.getOwnerResponsibleTypes);
            }
            catch
            {
                ownerResponsibleTypes = [];
            }
            uiUsers = await apiConnection.SendQueryAsync<List<UiUser>>(AuthQueries.getUserEmails);
            ScopedUserTo = scopedUserTo;
            ScopedUserCc = scopedUserCc;
            ScopedUserBcc = scopedUserBcc;
            ScopedUserEmailTo = scopedUserEmailTo;
            ScopedUserEmailCc = scopedUserEmailCc;
            ScopedUserEmailBcc = scopedUserEmailBcc;
        }

        public virtual async Task<bool> SendEmailToOwnerResponsibles(FwoOwner owner, string subject, string body, EmailRecipientOption recOpt, bool reqInCc = false)
        {
            List<string>? requester = reqInCc ? new() { GetEmailAddress(userConfig.User.Dn) } : null;
            return await SendEmail(await GetRecipients(recOpt, null, owner, null, null), subject, body, requester);
        }

        public virtual async Task<bool> SendEmailToOwnerResponsibles(FwoOwner owner, string subject, string body, string recipientConfig, bool reqInCc = false, List<string>? otherAddresses = null)
        {
            List<string>? requester = reqInCc ? new() { GetEmailAddress(userConfig.User.Dn) } : null;
            List<string> recipients = await GetRecipients(recipientConfig, owner, otherAddresses);
            return await SendEmail(recipients, subject, body, requester);
        }

        /// <summary>
        /// Sends an immediate workflow action email using notification recipient fields.
        /// </summary>
        public async Task<bool> SendWorkflowActionEmail(FwoNotification notification, WfStatefulObject statefulObject, FwoOwner? owner, string? userGrpDn = null,
            WorkflowEmailContent? workflowContent = null, WfStatefulObject? placeholderObject = null)
        {
            List<string> tos = await GetWorkflowActionRecipients(notification.RecipientTo, notification.EmailAddressTo, statefulObject, owner, ScopedUserTo, ScopedUserEmailTo, userGrpDn);
            List<string>? ccs = notification.RecipientCc == EmailRecipientOption.None
                ? null
                : await GetWorkflowActionRecipients(notification.RecipientCc, notification.EmailAddressCc, statefulObject, owner, ScopedUserCc, ScopedUserEmailCc, userGrpDn);
            List<string>? bccs = notification.RecipientBcc == EmailRecipientOption.None
                ? null
                : await GetWorkflowActionRecipients(notification.RecipientBcc, notification.EmailAddressBcc, statefulObject, owner, ScopedUserBcc, ScopedUserEmailBcc, userGrpDn);
            WfStatefulObject placeholderContext = placeholderObject ?? statefulObject;
            string subject = NotificationPlaceholderResolver.ReplaceWorkflowPlaceholders(notification.EmailSubject, placeholderContext, owner);
            string body = NotificationPlaceholderResolver.ReplaceWorkflowPlaceholders(NotificationEmailLayoutHelper.BuildBody(notification, workflowContent), placeholderContext, owner);
            FormFile? attachment = await NotificationEmailLayoutHelper.BuildAttachment(notification.Layout, workflowContent, subject);
            return await SendEmail(tos, subject, body, ccs, bccs,
                notification.Layout == NotificationLayout.HtmlInBody, attachment);
        }

        private async Task<List<string>> GetWorkflowActionRecipients(
            EmailRecipientOption recipientOption,
            string addressList,
            WfStatefulObject statefulObject,
            FwoOwner? owner,
            string? scopedUser,
            string? scopedUserEmail,
            string? assignedGroupDn = null)
        {
            WfStatefulObject recipientContext = AssignedGroupRecipientContext(recipientOption, statefulObject, assignedGroupDn);
            List<string> recipients = await GetRecipients(recipientOption, recipientContext, owner, scopedUser, SplitAddresses(addressList), scopedUserEmail);
            if (recipients.Count == 0 && recipientOption != EmailRecipientOption.None)
            {
                Log.WriteWarning("Workflow Email", $"No recipients resolved for option '{recipientOption}'. Scoped DN: '{scopedUser ?? ""}', stateful object: '{statefulObject.GetType().Name}'.");
            }
            return recipients;
        }

        private static WfStatefulObject AssignedGroupRecipientContext(EmailRecipientOption recipientOption, WfStatefulObject statefulObject, string? assignedGroupDn)
        {
            if (recipientOption != EmailRecipientOption.AssignedGroup || string.IsNullOrWhiteSpace(assignedGroupDn))
            {
                return statefulObject;
            }
            return new WfStatefulObject(statefulObject) { AssignedGroup = assignedGroupDn };
        }

        private async Task<bool> SendEmail(List<string> tos, string subject, string body, List<string>? ccs = null, List<string>? bccs = null,
            bool mailFormatHtml = true, FormFile? attachment = null)
        {
            EmailConnection emailConnection = new(userConfig.EmailServerAddress, userConfig.EmailPort,
                userConfig.EmailTls, userConfig.EmailUser, userConfig.EmailPassword, userConfig.EmailSenderAddress);
            ApplyDummyRecipientOverride(ref tos, ref ccs, ref bccs);
            tos = [.. tos.Where(t => t != "")];
            if (tos.Count == 0)
            {
                Log.WriteWarning("SendEmail", $"No email sent because no To recipients could be resolved. Subject: '{subject}'.");
                return false;
            }
            ccs = ccs?.Where(c => c != "").ToList();
            bccs = bccs?.Where(bcc => bcc != "").ToList();
            MailData mailData = new(tos, subject) { Body = body, Cc = ccs ?? [], Bcc = bccs ?? [] };
            if (attachment != null)
            {
                mailData.Attachments = new FormFileCollection() { attachment };
            }
            Log.WriteInfo("SendEmail", $"Sending workflow email to {tos.Count} recipient(s), cc {mailData.Cc.Count}, bcc {mailData.Bcc.Count}. Subject: '{subject}'.");
            bool sent = await MailKitMailer.SendAsync(mailData, emailConnection, mailFormatHtml, new CancellationToken());
            if (!sent)
            {
                Log.WriteWarning("SendEmail", $"MailKit returned false while sending workflow email. To recipients: {tos.Count}, subject: '{subject}'.");
            }
            return sent;
        }

        public async Task<List<string>> GetRecipients(EmailRecipientOption recipientOption, WfStatefulObject? statefulObject, FwoOwner? owner, string? scopedUser,
            List<string>? otherAddresses, string? scopedUserEmail = null)
        {
            if (userConfig.UseDummyEmailAddress && recipientOption != EmailRecipientOption.None)
            {
                return DummyRecipients();
            }
            Dictionary<EmailRecipientOption, Func<Task<List<string>>>> handlers = BuildRecipientHandlers(statefulObject, owner, scopedUser, otherAddresses, scopedUserEmail);
            if (handlers.TryGetValue(recipientOption, out Func<Task<List<string>>>? handler))
            {
                return await handler();
            }
            return [];
        }

        private Dictionary<EmailRecipientOption, Func<Task<List<string>>>> BuildRecipientHandlers(
            WfStatefulObject? statefulObject,
            FwoOwner? owner,
            string? scopedUser,
            List<string>? otherAddresses,
            string? scopedUserEmail)
        {
            Func<Task<List<string>>> scopedUserHandler = () => CollectEmailAddressesFromScopedUser(scopedUser, scopedUserEmail);
            return new Dictionary<EmailRecipientOption, Func<Task<List<string>>>>
            {
                { EmailRecipientOption.CurrentHandler, () => CollectEmailAddressesFromUser(statefulObject?.CurrentHandler) },
                { EmailRecipientOption.RecentHandler, () => CollectEmailAddressesFromUser(statefulObject?.RecentHandler) },
                { EmailRecipientOption.AssignedGroup, () => CollectEmailAddressesFromUserOrGroup(statefulObject?.AssignedGroup) },
                { EmailRecipientOption.OwnerMainResponsible, () => CollectOwnerAddressesByType(owner, GlobalConst.kOwnerResponsibleTypeMain) },
                { EmailRecipientOption.AllOwnerResponsibles, () => CollectEmailAddressesFromDns(owner?.GetAllOwnerResponsibles()) },
                { EmailRecipientOption.OwnerGroupOnly, () => CollectOwnerAddressesByType(owner, GlobalConst.kOwnerResponsibleTypeSupporting) },
                { EmailRecipientOption.ConfiguredResponsibles, () => Task.FromResult(new List<string>()) },
                { EmailRecipientOption.Requester, scopedUserHandler },
                { EmailRecipientOption.Approver, scopedUserHandler },
                { EmailRecipientOption.LastCommenter, scopedUserHandler },
                { EmailRecipientOption.FallbackToMainResponsibleIfOwnerGroupEmpty, () => GetOwnerGroupOrMainResponsibleRecipients(owner) },
                { EmailRecipientOption.OtherAddresses, () => Task.FromResult(GetOtherAddresses(otherAddresses)) }
            };
        }

        private async Task<List<string>> CollectOwnerAddressesByType(FwoOwner? owner, int responsibleType)
        {
            return await CollectEmailAddressesFromDns(owner?.GetOwnerResponsiblesByType(responsibleType));
        }

        private static List<string> GetOtherAddresses(List<string>? otherAddresses)
        {
            return otherAddresses != null ? [.. otherAddresses] : [];
        }

        private async Task<List<string>> GetOwnerGroupOrMainResponsibleRecipients(FwoOwner? owner)
        {
            if (owner is null)
            {
                return [];
            }

            List<string> ownerGroupAddresses = await CollectOwnerAddressesByType(owner, GlobalConst.kOwnerResponsibleTypeSupporting);
            List<string> mainResponsibleAddresses = await CollectOwnerAddressesByType(owner, GlobalConst.kOwnerResponsibleTypeMain);
            ownerGroupAddresses.AddRange(mainResponsibleAddresses);
            if (ownerGroupAddresses.Count > 0)
            {
                return ownerGroupAddresses;
            }

            return mainResponsibleAddresses;
        }

        public async Task<List<string>> GetRecipients(EmailRecipientSelection selection, FwoOwner? owner, List<string>? otherAddresses)
        {
            if (!selection.HasAnyRecipientOption())
            {
                return [];
            }
            if (userConfig.UseDummyEmailAddress)
            {
                return DummyRecipients();
            }

            HashSet<string> recipients = new(StringComparer.OrdinalIgnoreCase);
            AddOtherAddresses(selection, otherAddresses, recipients);
            if (owner != null)
            {
                await AddOwnerTypeRecipients(owner, selection.OwnerResponsibleTypeIds.Distinct(), recipients);
                await AddFallbackRecipients(selection, owner, recipients);
            }

            return recipients.ToList();
        }

        public async Task<List<string>> GetRecipients(string recipientConfig, FwoOwner? owner, List<string>? otherAddresses)
        {
            EmailRecipientSelection selection = EmailRecipientSelection.Parse(recipientConfig, GetActiveOwnerResponsibleTypeIds());
            return await GetRecipients(selection, owner, otherAddresses);
        }

        private static void AddOtherAddresses(EmailRecipientSelection selection, List<string>? otherAddresses, HashSet<string> recipients)
        {
            if (selection.OtherAddresses)
            {
                AddAddresses(recipients, selection.OtherAddressList);
                AddAddresses(recipients, otherAddresses);
            }
        }

        private async Task AddOwnerTypeRecipients(FwoOwner owner, IEnumerable<int> responsibleTypeIds, HashSet<string> recipients)
        {
            foreach (int responsibleTypeId in responsibleTypeIds)
            {
                List<string> ownerTypeRecipients = await CollectEmailAddressesFromDns(owner.GetOwnerResponsiblesByType(responsibleTypeId));
                AddAddresses(recipients, ownerTypeRecipients);
            }
        }

        private async Task AddFallbackRecipients(EmailRecipientSelection selection, FwoOwner owner, HashSet<string> recipients)
        {
            if (!selection.EnsureAtLeastOneNotification || recipients.Count > 0)
            {
                return;
            }

            HashSet<int> selectedTypeIds = selection.OwnerResponsibleTypeIds.ToHashSet();
            List<int> fallbackTypeIds = ownerResponsibleTypes
                .Where(type => type.Active && !selectedTypeIds.Contains(type.Id))
                .OrderByDescending(type => type.SortOrder)
                .ThenByDescending(type => type.Id)
                .Select(type => type.Id)
                .ToList();

            foreach (int responsibleTypeId in fallbackTypeIds)
            {
                List<string> ownerTypeRecipients = await CollectEmailAddressesFromDns(owner.GetOwnerResponsiblesByType(responsibleTypeId));
                if (ownerTypeRecipients.Count > 0)
                {
                    AddAddresses(recipients, ownerTypeRecipients);
                    break;
                }
            }
        }

        private static void AddAddresses(HashSet<string> recipients, IEnumerable<string>? addresses)
        {
            if (addresses == null)
            {
                return;
            }

            foreach (string address in addresses)
            {
                if (!string.IsNullOrWhiteSpace(address))
                {
                    recipients.Add(address);
                }
            }
        }

        private List<int> GetActiveOwnerResponsibleTypeIds()
        {
            return ownerResponsibleTypes
                .Where(type => type.Active)
                .Select(type => type.Id)
                .ToList();
        }

        private void ApplyDummyRecipientOverride(ref List<string> tos, ref List<string>? ccs, ref List<string>? bccs)
        {
            if (!userConfig.UseDummyEmailAddress)
            {
                return;
            }

            tos = DummyRecipients();
            ccs = [];
            bccs = [];
        }

        private List<string> DummyRecipients()
        {
            return string.IsNullOrWhiteSpace(userConfig.DummyEmailAddress) ? [] : [userConfig.DummyEmailAddress];
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
        public static List<string> SplitAddresses(string? addresslist)
        {
            if (string.IsNullOrWhiteSpace(addresslist))
            {
                return [];
            }
            string[] separatingStrings = [",", ";", "|"];
            return [.. addresslist.Split(separatingStrings, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];
        }

        private async Task<List<string>> CollectEmailAddressesFromUserOrGroup(string? dn)
        {
            return await CollectEmailAddressesFromDns(dn == null ? null : [dn]);
        }

        private async Task<List<string>> CollectEmailAddressesFromScopedUser(string? dn, string? email)
        {
            if (userConfig.UseDummyEmailAddress)
            {
                return [userConfig.DummyEmailAddress];
            }
            if (!string.IsNullOrWhiteSpace(email))
            {
                return [email];
            }
            return await CollectEmailAddressesFromUserOrGroup(dn);
        }

        private async Task<List<string>> CollectEmailAddressesFromUser(UiUser? user)
        {
            if (user == null || string.IsNullOrWhiteSpace(user.Dn))
            {
                return [];
            }
            if (userConfig.UseDummyEmailAddress)
            {
                return [userConfig.DummyEmailAddress];
            }
            if (!string.IsNullOrWhiteSpace(user.Email))
            {
                return [user.Email];
            }
            return await CollectEmailAddressesFromUserOrGroup(user.Dn);
        }

        private async Task<List<string>> CollectEmailAddressesFromDns(IEnumerable<string>? dns)
        {
            List<string> dnsList = dns?.Where(dn => !string.IsNullOrWhiteSpace(dn)).Distinct(DistName.DnComparer).ToList() ?? [];
            if (dnsList.Count == 0)
            {
                Log.WriteWarning("Workflow Email", "No DNs supplied for recipient resolution.");
                return [];
            }

            List<string> resolverRecipients = await CollectEmailAddressesFromResolver(dnsList);
            if (resolverRecipients.Count > 0)
            {
                return resolverRecipients;
            }

            List<string> tos = [];
            List<string> resolvedDns = await ResolveUserDns(dnsList);
            foreach (string dn in resolvedDns)
            {
                tos.Add(GetEmailAddress(dn));
            }
            if (tos.All(string.IsNullOrWhiteSpace))
            {
                Log.WriteWarning("Workflow Email", $"Resolved DNs but found no email addresses: {string.Join(", ", resolvedDns)}.");
            }
            return tos;
        }

        private async Task<List<string>> CollectEmailAddressesFromResolver(IEnumerable<string>? dns)
        {
            List<string> dnsList = dns?.Where(dn => !string.IsNullOrWhiteSpace(dn)).Distinct(DistName.DnComparer).ToList() ?? [];
            if (recipientResolver == null || dnsList.Count == 0)
            {
                return [];
            }

            List<UiUser> resolvedUsers = await recipientResolver.ResolveUsers(dnsList);
            foreach (UiUser resolvedUser in resolvedUsers)
            {
                UpsertResolvedUiUser(resolvedUser);
            }

            return [.. resolvedUsers.Select(user => GetEmailAddress(user.Dn)).Where(email => email != "")];
        }

        private void UpsertResolvedUiUser(UiUser resolvedUser)
        {
            if (string.IsNullOrWhiteSpace(resolvedUser.Dn))
            {
                return;
            }

            UiUser? existingUser = uiUsers.FirstOrDefault(user => DistName.DnEquals(user.Dn, resolvedUser.Dn));
            if (existingUser == null)
            {
                uiUsers.Add(resolvedUser);
                return;
            }

            if (!string.IsNullOrWhiteSpace(resolvedUser.Email))
            {
                existingUser.Email = resolvedUser.Email;
            }
        }

        private async Task<List<string>> ResolveUserDns(IEnumerable<string>? dns)
        {
            List<string> dnsList = dns?.Where(dn => !string.IsNullOrWhiteSpace(dn)).Distinct(DistName.DnComparer).ToList() ?? [];
            if (dnsList.Count == 0)
            {
                return [];
            }

            List<string> resolvedDns = await ResolveUserDnsFromWorkflowResolver(dnsList);
            if (resolvedDns.Count > 0)
            {
                return resolvedDns;
            }

            resolvedDns = await ResolveUserDnsFromMiddleware(dnsList);
            return resolvedDns.Count > 0 ? resolvedDns : ResolveUserDnsFromOwnerGroups(dnsList);
        }

        private async Task<List<string>> ResolveUserDnsFromWorkflowResolver(List<string> dnsList)
        {
            return recipientResolver == null ? [] : await recipientResolver.ResolveUserDns(dnsList);
        }

        private async Task<List<string>> ResolveUserDnsFromMiddleware(List<string> dnsList)
        {
            if (middlewareClient == null)
            {
                return [];
            }

            try
            {
                var response = await middlewareClient.ResolveGroupMembers(new GroupResolveParameters { Dns = dnsList });
                if (response.IsSuccessful && response.Data != null)
                {
                    return [.. response.Data.Where(dn => !string.IsNullOrWhiteSpace(dn)).Distinct(DistName.DnComparer)];
                }

                DisplayResolveGroupMembersError(null);
            }
            catch (Exception exception)
            {
                DisplayResolveGroupMembersError(exception);
            }
            return [];
        }

        private List<string> ResolveUserDnsFromOwnerGroups(List<string> dnsList)
        {
            HashSet<string> resolved = new(DistName.DnComparer);
            foreach (string dn in dnsList)
            {
                UserGroup? ownerGroup = ownerGroups.FirstOrDefault(x => DistName.DnEquals(x.Dn, dn));
                if (ownerGroup != null)
                {
                    foreach (var user in ownerGroup.Users)
                    {
                        resolved.Add(user.Dn);
                    }
                }
                else
                {
                    resolved.Add(dn);
                }
            }
            return resolved.ToList();
        }

        private void DisplayResolveGroupMembersError(Exception? exception)
        {
            displayMessageInUi(exception, userConfig.GetText("fetch_groups"), userConfig.GetText("E5231"), true);
        }

        private string GetEmailAddress(string? dn)
        {
            if (userConfig.UseDummyEmailAddress)
            {
                return userConfig.DummyEmailAddress;
            }
            UiUser? uiuser = uiUsers.FirstOrDefault(x => DistName.DnEquals(x.Dn, dn));
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
