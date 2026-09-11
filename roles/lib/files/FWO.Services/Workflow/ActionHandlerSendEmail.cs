using FWO.Api.Client.Queries;
using FWO.Data;
using FWO.Data.Workflow;
using FWO.Logging;
using System.Text.Json;


namespace FWO.Services.Workflow
{
    public partial class ActionHandler
    {
        public async Task SendEmail(WfStateAction action, WfStatefulObject statefulObject, WfObjectScopes scope, FwoOwner? owner, string? userGrpDn = null)
        {
            await TrySendEmail(action, statefulObject, scope, owner, userGrpDn);
        }

        /// <summary>
        /// Performs a send email state action and reports whether it delivered. Callers that have to know
        /// about a failed delivery - the bundle flush above all, whose captured emails have no other
        /// delivery attempt - use this instead of <see cref="SendEmail"/>, which discards the outcome.
        /// </summary>
        /// <param name="action">State action to execute</param>
        /// <param name="statefulObject">Object the action was triggered for</param>
        /// <param name="scope">Scope the action was triggered in</param>
        /// <param name="owner">Owner the recipients are resolved for, if any</param>
        /// <param name="userGrpDn">User group DN the action was triggered for, if any</param>
        /// <returns>false if a send was attempted and failed, or the action threw; true otherwise</returns>
        public virtual async Task<bool> TrySendEmail(WfStateAction action, WfStatefulObject statefulObject, WfObjectScopes scope, FwoOwner? owner, string? userGrpDn = null)
        {
            Log.WriteDebug("SendEmail", "Perform Action");
            EmailActionParams? emailActionParams = null;
            try
            {
                emailActionParams = JsonSerializer.Deserialize<EmailActionParams>(action.ExternalParams) ?? throw new JsonException("Extparams could not be parsed.");
                if (CaptureBundledEmail(action, emailActionParams, statefulObject, scope, owner, userGrpDn))
                {
                    Log.WriteDebug("SendEmail", "Captured workflow action email for bundled delivery.");
                    return true;
                }

                return await SendActionNotifications(emailActionParams, statefulObject, scope, owner, userGrpDn);
            }
            catch (Exception exc)
            {
                Log.WriteError("Send Email", $"Could not send email: ", exc);
                if (emailActionParams?.ConfirmSentMail ?? false)
                {
                    wfHandler.DisplayMessage(exc, wfHandler.userConfig.GetText("send_email"), "", true);
                }
                return false;
            }
        }

        /// <summary>
        /// Sends the email of every notification the action resolves to.
        /// </summary>
        /// <returns>false if at least one send was attempted and failed</returns>
        private async Task<bool> SendActionNotifications(EmailActionParams emailActionParams, WfStatefulObject statefulObject,
            WfObjectScopes scope, FwoOwner? owner, string? userGrpDn)
        {
            List<FwoNotification> actionNotifications = await ResolveActionNotifications(emailActionParams);
            int sentEmailCount = 0;
            int failedEmailCount = 0;
            List<int> sentNotificationIds = [];
            foreach (FwoNotification actionNotification in actionNotifications)
            {
                await SetScope(statefulObject, scope, actionNotification);
                WorkflowEmailContent? workflowContent = await CreateWorkflowEmailContent(emailActionParams, statefulObject, scope);
                EmailHelper emailHelper = new(apiConnection, wfHandler.MiddlewareClient, wfHandler.userConfig, wfHandler.DisplayMessage, UserGroups, useInMwServer, workflowRecipientResolver);
                await emailHelper.Init(ScopedUserTo, ScopedUserCc, ScopedUserBcc, ScopedUserEmailTo, ScopedUserEmailCc, ScopedUserEmailBcc);
                WfStatefulObject placeholderObject = WorkflowPlaceholderObject(statefulObject);
                WorkflowEmailDeliveryResult deliveryResult = await emailHelper.SendWorkflowActionEmail(actionNotification, statefulObject, owner, userGrpDn, workflowContent, placeholderObject);
                if (deliveryResult == WorkflowEmailDeliveryResult.Failed)
                {
                    ++failedEmailCount;
                }
                else if (deliveryResult == WorkflowEmailDeliveryResult.Delivered)
                {
                    ++sentEmailCount;
                    AddSentNotificationId(sentNotificationIds, actionNotification);
                }
            }
            await UpdateSentNotificationTimestamps(sentNotificationIds);
            Log.WriteInfo("SendEmail", $"Sent {sentEmailCount} workflow action email(s).");
            DisplaySentEmailConfirmation(emailActionParams, sentEmailCount);
            if (failedEmailCount > 0)
            {
                Log.WriteWarning("SendEmail", $"{failedEmailCount} of {actionNotifications.Count} workflow action email(s) could not be delivered.");
            }
            return failedEmailCount == 0;
        }

        private static void AddSentNotificationId(List<int> sentNotificationIds, FwoNotification actionNotification)
        {
            if (actionNotification.Id > 0)
            {
                sentNotificationIds.Add(actionNotification.Id);
            }
        }

        private bool CaptureBundledEmail(WfStateAction action, EmailActionParams emailActionParams, WfStatefulObject statefulObject,
            WfObjectScopes scope, FwoOwner? owner, string? userGrpDn)
        {
            if (EmailBundleCollector == null || EmailBundleCollector.IsFlushing
                || emailActionParams.AttachedContent != EmailAttachedContent.RequestedConnections
                || emailActionParams.RequestTaskBundleMode != EmailRequestTaskBundleMode.SameTaskType
                || scope != WfObjectScopes.RequestTask || statefulObject is not WfReqTask reqTask
                || reqTask.TicketId <= 0)
            {
                return false;
            }

            if (!EmailBundleCollector.TryAdd(action, reqTask, owner, userGrpDn))
            {
                Log.WriteWarning("SendEmail", $"Workflow email bundle for ticket {reqTask.TicketId} is full. " +
                    $"Sending the email of request task {reqTask.TaskNumber} immediately instead of bundling it.");
                return false;
            }
            return true;
        }

        private void DisplaySentEmailConfirmation(EmailActionParams emailActionParams, int sentEmailCount)
        {
            if (emailActionParams.ConfirmSentMail && sentEmailCount > 0)
            {
                wfHandler.DisplayMessage(null, wfHandler.userConfig.GetText("send_email"), $"{sentEmailCount}{wfHandler.userConfig.GetText("emails_sent")}", false);
            }
        }

        private async Task UpdateSentNotificationTimestamps(List<int> notificationIds)
        {
            List<int> distinctNotificationIds = [.. notificationIds.Where(id => id > 0).Distinct()];
            if (distinctNotificationIds.Count == 0)
            {
                return;
            }

            try
            {
                int affectedRows = (await apiConnection.SendQueryAsync<ReturnId>(NotificationQueries.updateNotificationsLastSent,
                    new { ids = distinctNotificationIds, lastSent = DateTime.Now })).AffectedRows;
                if (affectedRows != distinctNotificationIds.Count)
                {
                    Log.WriteWarning("SendEmail", $"Updated last_sent for {affectedRows} of {distinctNotificationIds.Count} workflow action notification(s).");
                }
            }
            catch (Exception exc)
            {
                Log.WriteWarning("SendEmail", $"Could not update last_sent for workflow action notification(s): {exc.Message}");
            }
        }

        private async Task<List<FwoNotification>> ResolveActionNotifications(EmailActionParams emailActionParams)
        {
            List<int> notificationIds = [.. emailActionParams.NotificationIds.Where(id => id > 0).Distinct()];
            if (notificationIds.Count > 0)
            {
                List<FwoNotification> notifications = await apiConnection.SendQueryAsync<List<FwoNotification>>(NotificationQueries.getNotifications,
                    new { client = NotificationClient.WfAction.ToString() });
                List<FwoNotification> actionNotifications = [.. notifications.Where(n => notificationIds.Contains(n.Id))];
                List<int> missingNotificationIds = [.. notificationIds.Except(actionNotifications.Select(n => n.Id))];
                if (missingNotificationIds.Count > 0)
                {
                    throw new JsonException($"Referenced notification(s) '{string.Join(", ", missingNotificationIds)}' were not found.");
                }
                return actionNotifications;
            }

            return [emailActionParams.ToNotification()];
        }

        private async Task<WorkflowEmailContent?> CreateWorkflowEmailContent(EmailActionParams emailActionParams, WfStatefulObject statefulObject, WfObjectScopes scope)
        {
            if (emailActionParams.AttachedContent != EmailAttachedContent.RequestedConnections)
            {
                return null;
            }

            Dictionary<int, string> protocolNamesById = await GetProtocolNamesByIdForEmailContent();
            return scope switch
            {
                WfObjectScopes.Ticket when statefulObject is WfTicket ticket => WorkflowEmailContent.FromRequestTasks((await GetTicketForEmailContent(ticket)).Tasks, wfHandler.userConfig, protocolNamesById),
                WfObjectScopes.RequestTask when statefulObject is WfReqTask reqTask =>
                    WorkflowEmailContent.FromRequestTasks(await GetRequestTasksForEmailContent(emailActionParams, reqTask), wfHandler.userConfig, protocolNamesById),
                WfObjectScopes.ImplementationTask when statefulObject is WfImplTask implTask => WorkflowEmailContent.FromImplementationTasks([implTask], wfHandler.userConfig, protocolNamesById),
                WfObjectScopes.Approval when wfHandler.ActReqTask.Id > 0 => WorkflowEmailContent.FromRequestTasks([wfHandler.ActReqTask], wfHandler.userConfig, protocolNamesById),
                _ => null
            };
        }

        private async Task<List<WfReqTask>> GetRequestTasksForEmailContent(EmailActionParams emailActionParams, WfReqTask reqTask)
        {
            if (emailActionParams.RequestTaskBundleMode != EmailRequestTaskBundleMode.SameTaskType || reqTask.TicketId <= 0)
            {
                return [reqTask];
            }
            if (RequestTaskEmailBundle != null)
            {
                return RequestTaskEmailBundle.ToList();
            }

            WfTicket fullTicket = await GetTicketForEmailContent(new WfTicket { Id = reqTask.TicketId });
            List<WfReqTask> bundledTasks = [.. fullTicket.Tasks
                .Where(task => IsSameRequestTaskEmailBundle(task, reqTask))
                .OrderBy(task => task.TaskNumber)];

            return bundledTasks.Count > 0 ? bundledTasks : [reqTask];
        }

        /// <summary>
        /// Decides whether a candidate request task of the same ticket belongs into the email of the
        /// captured request task. Only task-derived properties are compared: the action, owner and
        /// user group are identical for every candidate by construction and cannot discriminate.
        /// </summary>
        /// <param name="candidate">Request task from the ticket to test</param>
        /// <param name="reqTask">Request task the email is being built for</param>
        /// <returns>true if both tasks belong into the same email</returns>
        private static bool IsSameRequestTaskEmailBundle(WfReqTask candidate, WfReqTask reqTask)
        {
            return WorkflowEmailBundleItem.BuildTaskBundleKey(candidate)
                == WorkflowEmailBundleItem.BuildTaskBundleKey(reqTask);
        }

        private async Task<Dictionary<int, string>> GetProtocolNamesByIdForEmailContent()
        {
            try
            {
                List<IpProtocol> protocols = await apiConnection.SendQueryAsync<List<IpProtocol>>(StmQueries.getIpProtocols);
                return protocols
                    .Where(protocol => !string.IsNullOrWhiteSpace(protocol.Name))
                    .ToDictionary(protocol => protocol.Id, protocol => protocol.Name);
            }
            catch (Exception exc)
            {
                Log.WriteWarning("SendEmail", $"Could not load protocol names for workflow email content. Falling back to protocol ids. {exc.Message}");
                return [];
            }
        }

        private async Task<WfTicket> GetTicketForEmailContent(WfTicket ticket)
        {
            if (ticket.Id <= 0)
            {
                return ticket;
            }

            try
            {
                WfTicket fullTicket = await apiConnection.SendQueryAsync<WfTicket>(RequestQueries.getTicketById, new { id = ticket.Id });
                fullTicket.UpdateCidrsInTaskElements();
                return fullTicket.Id > 0 ? fullTicket : ticket;
            }
            catch (Exception exc)
            {
                Log.WriteWarning("SendEmail", $"Could not load full ticket {ticket.Id} for workflow email content. Falling back to current ticket data. {exc.Message}");
                return ticket;
            }
        }

        private WfStatefulObject WorkflowPlaceholderObject(WfStatefulObject statefulObject)
        {
            return wfHandler.ActTicket.Id > 0 ? wfHandler.ActTicket : statefulObject;
        }
    }
}
