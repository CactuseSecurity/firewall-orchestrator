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
            Log.WriteDebug("SendEmail", "Perform Action");
            EmailActionParams? emailActionParams = null;
            try
            {
                emailActionParams = JsonSerializer.Deserialize<EmailActionParams>(action.ExternalParams) ?? throw new JsonException("Extparams could not be parsed.");
                List<FwoNotification> actionNotifications = await ResolveActionNotifications(emailActionParams);
                int sentEmailCount = 0;
                List<int> sentNotificationIds = [];
                foreach (FwoNotification actionNotification in actionNotifications)
                {
                    await SetScope(statefulObject, scope, actionNotification);
                    WorkflowEmailContent? workflowContent = await CreateWorkflowEmailContent(emailActionParams.AttachedContent, statefulObject, scope);
                    EmailHelper emailHelper = new(apiConnection, wfHandler.MiddlewareClient, wfHandler.userConfig, wfHandler.DisplayMessage, UserGroups, useInMwServer, workflowRecipientResolver);
                    await emailHelper.Init(ScopedUserTo, ScopedUserCc, ScopedUserBcc, ScopedUserEmailTo, ScopedUserEmailCc, ScopedUserEmailBcc);
                    WfStatefulObject placeholderObject = WorkflowPlaceholderObject(statefulObject);
                    if (await emailHelper.SendWorkflowActionEmail(actionNotification, statefulObject, owner, userGrpDn, workflowContent, placeholderObject))
                    {
                        ++sentEmailCount;
                        if (actionNotification.Id > 0)
                        {
                            sentNotificationIds.Add(actionNotification.Id);
                        }
                    }
                }
                await UpdateSentNotificationTimestamps(sentNotificationIds);
                Log.WriteInfo("SendEmail", $"Sent {sentEmailCount} workflow action email(s).");
                DisplaySentEmailConfirmation(emailActionParams, sentEmailCount);
            }
            catch (Exception exc)
            {
                Log.WriteError("Send Email", $"Could not send email: ", exc);
                if (emailActionParams?.ConfirmSentMail ?? false)
                {
                    wfHandler.DisplayMessage(exc, wfHandler.userConfig.GetText("send_email"), "", true);
                }
            }
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

        private async Task<WorkflowEmailContent?> CreateWorkflowEmailContent(EmailAttachedContent attachedContent, WfStatefulObject statefulObject, WfObjectScopes scope)
        {
            if (attachedContent != EmailAttachedContent.RequestedConnections)
            {
                return null;
            }

            return scope switch
            {
                WfObjectScopes.Ticket when statefulObject is WfTicket ticket => WorkflowEmailContent.FromRequestTasks((await GetTicketForEmailContent(ticket)).Tasks, wfHandler.userConfig),
                WfObjectScopes.RequestTask when statefulObject is WfReqTask reqTask => WorkflowEmailContent.FromRequestTasks([reqTask], wfHandler.userConfig),
                WfObjectScopes.ImplementationTask when statefulObject is WfImplTask implTask => WorkflowEmailContent.FromImplementationTasks([implTask], wfHandler.userConfig),
                WfObjectScopes.Approval when wfHandler.ActReqTask.Id > 0 => WorkflowEmailContent.FromRequestTasks([wfHandler.ActReqTask], wfHandler.userConfig),
                _ => null
            };
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
