using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Logging;
using FWO.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FWO.Middleware.Server.Controllers;

/// <summary>
/// Performs rendered notification email delivery in the trusted middleware process.
/// </summary>
[Authorize]
[ApiController]
[Route("api/notification")]
public class NotificationController(ApiConnection apiConnection, GlobalConfig globalConfig) : ControllerBase
{
    /// <summary>
    /// Processes immediate notifications for applications using a decommissioned interface.
    /// Notification data and recipients are resolved from persisted middleware data.
    /// </summary>
    [HttpPost("interface-decommission")]
    [Authorize(Roles = $"{Roles.Admin}, {Roles.FwAdmin}, {Roles.Modeller}")]
    [ProducesResponseType(typeof(NotificationDeliveryResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<NotificationDeliveryResult>> SendInterfaceDecommission(
        [FromBody] InterfaceDecommissionNotificationParameters parameters)
    {
        try
        {
            if (parameters is null || parameters.ConnectionId <= 0 || string.IsNullOrWhiteSpace(parameters.Reason))
            {
                return BadRequest("A connection ID and decommission reason are required.");
            }

            ModellingConnection? connection = await LoadConnection(parameters.ConnectionId);
            if (connection is null || !connection.IsInterface || !connection.Removed || connection.AppId is not > 0)
            {
                return BadRequest("The decommissioned interface could not be found.");
            }

            if (!CanProcessModellingNotification(connection))
            {
                return Forbid();
            }

            ModellingConnection? replacement = null;
            if (parameters.ReplacementConnectionId is > 0)
            {
                replacement = await LoadConnection(parameters.ReplacementConnectionId.Value);
                if (!IsEligibleReplacement(connection, replacement))
                {
                    return BadRequest("The replacement interface is not eligible for decommissioning.");
                }
            }
            List<ModellingConnection> usingConnections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(
                ModellingQueries.getInterfaceUsers, new { id = connection.Id });
            List<FwoOwner> usingOwners = await LoadUsingOwners(usingConnections, connection.AppId);

            NotificationService notificationService = await NotificationService.CreateAsync(
                NotificationClient.InterfaceDecomm, globalConfig, apiConnection);
            List<FwoNotification> notifications = notificationService.Notifications
                .Where(notification => notification.Deadline == NotificationDeadline.None)
                .ToList();
            NotificationDeliveryResult result = await SendInterfaceDecommissionNotifications(notificationService, notifications,
                connection, replacement, usingConnections, usingOwners, parameters.Reason);
            await notificationService.UpdateNotificationsLastSent();
            return Ok(result);
        }
        catch (Exception exception)
        {
            Log.WriteError("Interface Decommission Notification", "Could not process interface decommission notification.", exception);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    /// <summary>
    /// Processes the configured immediate notification for an existing interface request.
    /// All message data is resolved from the persisted connection and ticket in middleware.
    /// </summary>
    [HttpPost("interface-request")]
    [Authorize(Roles = $"{Roles.Admin}, {Roles.FwAdmin}, {Roles.Modeller}")]
    [ProducesResponseType(typeof(NotificationDeliveryResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<NotificationDeliveryResult>> SendInterfaceRequest(
        [FromBody] InterfaceRequestNotificationParameters parameters)
    {
        try
        {
            if (parameters is null || parameters.ConnectionId <= 0)
            {
                return BadRequest("Connection ID must be greater than zero.");
            }

            ModellingConnection? connection = await LoadConnection(parameters.ConnectionId);
            if (connection is null || !connection.IsInterface || !connection.IsRequested || connection.Removed
                || connection.GetBoolProperty(ConState.Rejected.ToString()) || connection.TicketId is not > 0
                || connection.ProposedAppId is not > 0)
            {
                return BadRequest("The requested interface could not be found.");
            }

            WfTicket? ticket = await apiConnection.SendQueryAsync<WfTicket>(RequestQueries.getTicketById,
                new { id = connection.TicketId.Value });
            if (!CanProcessModellingNotification(connection, ticket, allowRequestCreator: true))
            {
                return Forbid();
            }

            WfReqTask? requestTask = ticket?.Tasks.FirstOrDefault(task => task.TaskType == WfTaskType.new_interface.ToString());
            FwoOwner owner = await LoadOwner(connection.ProposedAppId, includeResponsibles: true) ?? connection.ProposedApp;
            FwoOwner? requestingOwner = await LoadOwner(requestTask?.GetAddInfoIntValue(AdditionalInfoKeys.ReqOwner));
            UiUser? requester = CreateRequesterContext(ticket);
            NotificationPlaceholderResolver.NotificationPlaceholderValues placeholderValues =
                BuildInterfaceRequestPlaceholderValues(connection, ticket, requestTask, owner, requestingOwner, requester);

            NotificationService notificationService = await NotificationService.CreateAsync(
                NotificationClient.InterfaceRequest, globalConfig, apiConnection);
            List<FwoNotification> notifications = notificationService.Notifications
                .Where(notification => notification.Deadline == NotificationDeadline.None
                    && (notification.OwnerId == null || notification.OwnerId == owner.Id))
                .ToList();
            if (notifications.Count == 0)
            {
                return Ok(NotificationDeliveryResult.Suppressed);
            }

            NotificationDeliveryResult result = await SendNotifications(notificationService, notifications, owner, placeholderValues);
            await notificationService.UpdateNotificationsLastSent();
            return Ok(result);
        }
        catch (Exception exception)
        {
            Log.WriteError("Interface Request Notification", "Could not process interface request notification.", exception);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    private async Task<ModellingConnection?> LoadConnection(int connectionId)
    {
        List<ModellingConnection> connections = await apiConnection.SendQueryAsync<List<ModellingConnection>>(
            ModellingQueries.getConnectionForNotification, new { id = connectionId });
        return connections.SingleOrDefault();
    }

    private static bool IsEligibleReplacement(ModellingConnection connection, ModellingConnection? replacement)
    {
        if (replacement == null || replacement.Id == connection.Id)
        {
            return false;
        }

        if (!replacement.IsInterface || !replacement.IsPublished || replacement.Removed)
        {
            return false;
        }

        if (replacement.GetBoolProperty(ConState.Decommissioned.ToString())
            || replacement.GetBoolProperty(ConState.Rejected.ToString()))
        {
            return false;
        }

        return !string.Equals(replacement.InterfacePermission, InterfacePermissions.Private.ToString(),
            StringComparison.OrdinalIgnoreCase);
    }

    private async Task<FwoOwner?> LoadOwner(int? ownerId, bool includeResponsibles = false)
    {
        if (ownerId is not > 0)
        {
            return null;
        }

        string query = OwnerQueries.getOwnerById;
        if (includeResponsibles)
        {
            query = OwnerQueries.getOwnerForNotification;
        }

        return await apiConnection.SendQueryAsync<FwoOwner>(query, new { id = ownerId.Value });
    }

    private async Task<List<FwoOwner>> LoadUsingOwners(List<ModellingConnection> usingConnections, int? decommissionedOwnerId)
    {
        List<FwoOwner> owners = [];
        foreach (ModellingConnection usingConnection in usingConnections
            .Where(usingConnection => usingConnection.AppId is > 0 && usingConnection.AppId != decommissionedOwnerId)
            .DistinctBy(usingConnection => usingConnection.AppId))
        {
            FwoOwner? owner = await LoadOwner(usingConnection.AppId, includeResponsibles: true);
            FwoOwner resolvedOwner = usingConnection.App;
            if (owner is { Id: > 0 })
            {
                resolvedOwner = owner;
            }
            if (resolvedOwner.Id > 0)
            {
                owners.Add(resolvedOwner);
            }
        }
        return owners;
    }

    private bool CanProcessModellingNotification(ModellingConnection connection, WfTicket? ticket = null,
        bool allowRequestCreator = false)
    {
        ClaimsPrincipal caller = ControllerContext.HttpContext.User;
        if (caller.IsInRole(Roles.Admin) || caller.IsInRole(Roles.FwAdmin))
        {
            return true;
        }

        int? targetOwnerId = connection.IsRequested ? connection.ProposedAppId : connection.AppId;
        if (!caller.IsInRole(Roles.Modeller) || targetOwnerId is not > 0)
        {
            return false;
        }

        string callerName = caller.FindFirstValue("unique_name") ?? caller.Identity?.Name ?? "";
        int callerId = JwtClaimParser.ExtractIntClaimValues(caller.Claims, "x-hasura-user-id").FirstOrDefault();
        bool hasCallerName = !string.IsNullOrWhiteSpace(callerName);
        if (allowRequestCreator && ((callerId > 0 && ticket?.Requester?.DbId == callerId)
            || (hasCallerName && string.Equals(ticket?.Requester?.Name, callerName, StringComparison.OrdinalIgnoreCase))
            || (hasCallerName && string.Equals(connection.Creator, callerName, StringComparison.OrdinalIgnoreCase))))
        {
            return true;
        }

        List<int> editableOwnerIds = JwtClaimParser.ExtractIntClaimValues(caller.Claims, "x-hasura-editable-owners");
        bool isEditableOwner = editableOwnerIds.Contains(targetOwnerId.Value);
        if (!isEditableOwner)
        {
            Log.WriteWarning("Interface Request Notification",
                $"Notification scope denied. Caller='{callerName}', callerId={callerId}, connectionId={connection.Id}, " +
                $"connectionCreator='{connection.Creator}', ticketRequester='{ticket?.Requester?.Name}', " +
                $"ticketRequesterId={ticket?.Requester?.DbId}, targetOwnerId={targetOwnerId.Value}, " +
                $"editableOwnerIds='{string.Join(",", editableOwnerIds)}'.");
        }
        return isEditableOwner;
    }


    private static UiUser? CreateRequesterContext(WfTicket? ticket)
    {
        if (ticket?.Requester != null)
        {
            string requesterDn = ticket.Requester.Dn ?? "";
            if (string.IsNullOrWhiteSpace(requesterDn))
            {
                requesterDn = ticket.RequesterDn ?? "";
            }

            UiUser requester = new(ticket.Requester) { Dn = requesterDn };
            return requester;
        }

        if (string.IsNullOrWhiteSpace(ticket?.RequesterDn))
        {
            return null;
        }

        return new UiUser { Dn = ticket.RequesterDn };
    }

    private NotificationPlaceholderResolver.NotificationPlaceholderValues BuildInterfaceRequestPlaceholderValues(
        ModellingConnection connection, WfTicket? ticket, WfReqTask? requestTask, FwoOwner owner, FwoOwner? requestingOwner,
        UiUser? requester)
    {
        string interfaceName = requestTask?.Title ?? connection.Name ?? globalConfig.GetText("interface");
        string interfaceUrl = $"{globalConfig.UiHostName}/{PageName.Modelling}/{owner.ExtAppId}/{connection.Id}";
        string requesterName = ticket?.Requester?.Name ?? ticket?.RequesterDn ?? "";
        return new NotificationPlaceholderResolver.NotificationPlaceholderValues
        {
            Application = owner,
            RequestingOwner = requestingOwner,
            Requester = requester,
            InterfaceName = interfaceName,
            InterfaceLinkText = globalConfig.GetText("request_interface"),
            InterfaceLinkName = interfaceName,
            InterfaceLinkUrl = interfaceUrl,
            NewInterfaceName = interfaceName,
            NewInterfaceLinkText = globalConfig.GetText("request_interface"),
            NewInterfaceLinkName = interfaceName,
            NewInterfaceLinkUrl = interfaceUrl,
            Reason = requestTask?.Reason ?? ticket?.Reason ?? connection.Reason ?? "",
            UserName = requesterName,
            RequesterName = requesterName,
            RequestDate = ticket?.CreationDate.ToString("dd.MM.yyyy") ?? ""
        };
    }

    private static async Task<NotificationDeliveryResult> SendNotifications(NotificationService service,
        List<FwoNotification> notifications, FwoOwner owner,
        NotificationPlaceholderResolver.NotificationPlaceholderValues placeholderValues)
    {
        if (notifications.Count == 0)
        {
            return NotificationDeliveryResult.Suppressed;
        }

        bool delivered = false;
        bool failed = false;
        bool noRecipients = false;
        foreach (FwoNotification notification in notifications)
        {
            NotificationDeliveryResult result = await service.SendNotificationWithResult(notification, owner,
                placeholderValues: placeholderValues);
            delivered |= result == NotificationDeliveryResult.Delivered;
            failed |= result == NotificationDeliveryResult.Failed;
            noRecipients |= result == NotificationDeliveryResult.NoRecipients;
        }

        return CombineDeliveryResults(delivered, failed, noRecipients);
    }

    private async Task<NotificationDeliveryResult> SendInterfaceDecommissionNotifications(NotificationService service,
        List<FwoNotification> notifications, ModellingConnection connection, ModellingConnection? replacement,
        List<ModellingConnection> usingConnections, List<FwoOwner> usingOwners, string reason)
    {
        if (notifications.Count == 0 || usingOwners.Count == 0)
        {
            return NotificationDeliveryResult.Suppressed;
        }

        bool delivered = false;
        bool failed = false;
        bool noRecipients = false;
        FwoOwner interfaceOwner = connection.App;
        foreach (FwoOwner owner in usingOwners)
        {
            foreach (FwoNotification notification in notifications.Where(notification => notification.OwnerId == null || notification.OwnerId == owner.Id))
            {
                string separator = notification.Layout == NotificationLayout.HtmlInBody ? "<br>" : Environment.NewLine;
                string connectionList = string.Join(separator, usingConnections.Where(usingConnection => usingConnection.AppId == owner.Id)
                    .Select(usingConnection => usingConnection.Name));
                NotificationPlaceholderResolver.NotificationPlaceholderValues values = BuildInterfaceDecommissionPlaceholderValues(
                    connection, replacement, interfaceOwner, reason);
                NotificationDeliveryResult result = await service.SendNotificationWithResult(notification, owner, connectionList,
                    placeholderValues: values);
                delivered |= result == NotificationDeliveryResult.Delivered;
                failed |= result == NotificationDeliveryResult.Failed;
                noRecipients |= result == NotificationDeliveryResult.NoRecipients;
            }
        }

        return CombineDeliveryResults(delivered, failed, noRecipients);
    }

    private static NotificationDeliveryResult CombineDeliveryResults(bool delivered, bool failed, bool noRecipients)
    {
        if (failed)
        {
            return NotificationDeliveryResult.Failed;
        }

        if (noRecipients)
        {
            return NotificationDeliveryResult.NoRecipients;
        }

        if (delivered)
        {
            return NotificationDeliveryResult.Delivered;
        }

        return NotificationDeliveryResult.Suppressed;
    }

    private NotificationPlaceholderResolver.NotificationPlaceholderValues BuildInterfaceDecommissionPlaceholderValues(
        ModellingConnection connection, ModellingConnection? replacement, FwoOwner interfaceOwner, string reason)
    {
        string replacementUrl = replacement?.App is not null && replacement.Id > 0
            ? $"{globalConfig.UiHostName}/{PageName.Modelling}/{replacement.App.ExtAppId}/{replacement.Id}"
            : "";
        return new NotificationPlaceholderResolver.NotificationPlaceholderValues
        {
            Application = interfaceOwner,
            InterfaceName = connection.Name ?? "",
            NewInterfaceName = replacement?.Name ?? "",
            InterfaceLinkText = globalConfig.GetText("interface"),
            InterfaceLinkName = connection.Name ?? "",
            InterfaceLinkUrl = $"{globalConfig.UiHostName}/{PageName.Modelling}/{interfaceOwner.ExtAppId}/{connection.Id}",
            NewInterfaceLinkText = globalConfig.GetText("interface"),
            NewInterfaceLinkName = replacement?.Name ?? "",
            NewInterfaceLinkUrl = replacementUrl,
            Reason = reason,
            UserName = ControllerContext.HttpContext.User.Identity?.Name ?? ""
        };
    }

}
