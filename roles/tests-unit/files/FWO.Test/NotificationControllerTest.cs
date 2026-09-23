using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Data.Modelling;
using FWO.Data.Workflow;
using FWO.Middleware.Server;
using FWO.Middleware.Server.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using NUnit.Framework;
using System.Reflection;
using System.Security.Claims;

namespace FWO.Test;

[TestFixture]
internal class NotificationControllerTest
{
    [Test]
    public void InterfaceRequestEndpoint_AllowsBusinessRoles()
    {
        Assert.That(GetRoles(nameof(NotificationController.SendInterfaceRequest)),
            Is.EqualTo($"{Roles.Admin}, {Roles.FwAdmin}, {Roles.Modeller}"));
    }

    [Test]
    public void InterfaceDecommissionEndpoint_AllowsBusinessRoles()
    {
        Assert.That(GetRoles(nameof(NotificationController.SendInterfaceDecommission)),
            Is.EqualTo($"{Roles.Admin}, {Roles.FwAdmin}, {Roles.Modeller}"));
    }

    [Test]
    public async Task SendInterfaceRequest_RejectsInvalidConnectionId()
    {
        NotificationController controller = CreateController(new ControllerApiConnection(), new GlobalConfig());

        ActionResult<NotificationDeliveryResult> result = await controller.SendInterfaceRequest(
            new InterfaceRequestNotificationParameters { ConnectionId = 0 });

        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task SendInterfaceRequest_RejectsNonRequestedConnection()
    {
        ControllerApiConnection apiConnection = new()
        {
            Connection = new ModellingConnection { Id = 10, IsInterface = true, TicketId = 20 }
        };
        NotificationController controller = CreateController(apiConnection, new GlobalConfig());

        ActionResult<NotificationDeliveryResult> result = await controller.SendInterfaceRequest(
            new InterfaceRequestNotificationParameters { ConnectionId = 10 });

        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task SendInterfaceRequest_RejectsRejectedConnection()
    {
        ModellingConnection connection = RequestedConnection();
        connection.AddProperty(ConState.Rejected.ToString());
        ControllerApiConnection apiConnection = new()
        {
            Connection = connection,
            Ticket = new WfTicket()
        };
        NotificationController controller = CreateController(apiConnection, new GlobalConfig());

        ActionResult<NotificationDeliveryResult> result = await controller.SendInterfaceRequest(
            new InterfaceRequestNotificationParameters { ConnectionId = 10 });

        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task SendInterfaceRequest_RejectsModellerOutsideOwnerScope()
    {
        ControllerApiConnection apiConnection = new()
        {
            Connection = RequestedConnection(),
            Ticket = new WfTicket()
        };
        NotificationController controller = CreateController(apiConnection, new GlobalConfig(), Roles.Modeller,
            "{ 7 }");

        ActionResult<NotificationDeliveryResult> result = await controller.SendInterfaceRequest(
            new InterfaceRequestNotificationParameters { ConnectionId = 10 });

        Assert.That(result.Result, Is.TypeOf<ForbidResult>());
    }

    [Test]
    public async Task SendInterfaceRequest_RejectsUnauthorizedCallerBeforeLifecycleValidation()
    {
        ModellingConnection connection = RequestedConnection();
        connection.AddProperty(ConState.Rejected.ToString());
        ControllerApiConnection apiConnection = new()
        {
            Connection = connection,
            Ticket = new WfTicket()
        };
        NotificationController controller = CreateController(apiConnection, new GlobalConfig(), Roles.Modeller,
            "{ 7 }");

        ActionResult<NotificationDeliveryResult> result = await controller.SendInterfaceRequest(
            new InterfaceRequestNotificationParameters { ConnectionId = 10 });

        Assert.That(result.Result, Is.TypeOf<ForbidResult>());
    }

    [Test]
    public async Task SendInterfaceRequest_AllowsTheRequestCreatorWithoutTargetOwnerEditRights()
    {
        ModellingConnection connection = RequestedConnection();
        ControllerApiConnection apiConnection = new()
        {
            NotificationExists = false,
            Connection = connection,
            Ticket = new WfTicket { Requester = new UiUser { DbId = 4, Name = "modeller" } }
        };
        NotificationController controller = CreateController(apiConnection, new SimulatedGlobalConfig(), Roles.Modeller);
        List<Claim> claims =
        [
            new Claim(ClaimTypes.Role, Roles.Modeller),
            new Claim("x-hasura-user-id", "4")
        ];
        controller.ControllerContext.HttpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"));

        ActionResult<NotificationDeliveryResult> result = await controller.SendInterfaceRequest(
            new InterfaceRequestNotificationParameters { ConnectionId = 10 });

        Assert.That(GetResult(result), Is.EqualTo(NotificationDeliveryResult.Suppressed));
    }

    [Test]
    public async Task SendInterfaceRequest_RejectsEmptyCallerNameWhenStoredNamesAreEmpty()
    {
        ControllerApiConnection apiConnection = new()
        {
            NotificationExists = false,
            Connection = RequestedConnection(),
            Ticket = new WfTicket { Requester = new UiUser { Name = "" } }
        };
        NotificationController controller = CreateController(apiConnection, new SimulatedGlobalConfig(), Roles.Modeller);

        ActionResult<NotificationDeliveryResult> result = await controller.SendInterfaceRequest(
            new InterfaceRequestNotificationParameters { ConnectionId = 10 });

        Assert.That(result.Result, Is.TypeOf<ForbidResult>());
    }

    [Test]
    public async Task SendInterfaceRequest_RejectsSameUsernameWithoutMatchingUserId()
    {
        ModellingConnection connection = RequestedConnection();
        connection.Creator = "modeller";
        ControllerApiConnection apiConnection = new()
        {
            NotificationExists = false,
            Connection = connection,
            Ticket = new WfTicket { Requester = new UiUser { Name = "modeller" } }
        };
        NotificationController controller = CreateController(apiConnection, new SimulatedGlobalConfig(), Roles.Modeller);

        ActionResult<NotificationDeliveryResult> result = await controller.SendInterfaceRequest(
            new InterfaceRequestNotificationParameters { ConnectionId = 10 });

        Assert.That(result.Result, Is.TypeOf<ForbidResult>());
    }

    [Test]
    public async Task SendInterfaceRequest_ReturnsSuppressedWhenNoNotificationIsConfigured()
    {
        ControllerApiConnection apiConnection = new()
        {
            NotificationExists = false,
            Connection = RequestedConnection(),
            Ticket = new WfTicket()
        };
        NotificationController controller = CreateController(apiConnection, new SimulatedGlobalConfig());

        ActionResult<NotificationDeliveryResult> result = await controller.SendInterfaceRequest(
            new InterfaceRequestNotificationParameters { ConnectionId = 10 });

        Assert.That(GetResult(result), Is.EqualTo(NotificationDeliveryResult.Suppressed));
    }

    [Test]
    public async Task SendInterfaceRequest_ResolvesTicketAndRequestOwner()
    {
        ControllerApiConnection apiConnection = new()
        {
            NotificationExists = false,
            Owner = new FwoOwner { Id = 9, Name = "Request Owner" },
            Connection = RequestedConnection(),
            Ticket = new WfTicket
            {
                CreationDate = new DateTime(2026, 1, 2),
                RequesterDn = "cn=requester",
                Tasks =
                [
                    new WfReqTask
                    {
                        TaskType = WfTaskType.new_interface.ToString(),
                        Title = "Requested interface",
                        Reason = "Request reason",
                        AdditionalInfo = "{\"ReqOwner\":\"9\"}"
                    }
                ]
            }
        };
        NotificationController controller = CreateController(apiConnection, new SimulatedGlobalConfig());

        ActionResult<NotificationDeliveryResult> result = await controller.SendInterfaceRequest(
            new InterfaceRequestNotificationParameters { ConnectionId = 10 });

        Assert.That(GetResult(result), Is.EqualTo(NotificationDeliveryResult.Suppressed));
        Assert.That(apiConnection.OwnerQueryCount, Is.EqualTo(2));
    }

    [Test]
    public async Task SendInterfaceRequest_ReturnsNoRecipientsAndLogsFailure()
    {
        ControllerApiConnection apiConnection = ConfiguredNoRecipientConnection();
        NotificationController controller = CreateController(apiConnection, new GlobalConfig());

        ActionResult<NotificationDeliveryResult> result = await controller.SendInterfaceRequest(
            new InterfaceRequestNotificationParameters { ConnectionId = 10 });

        Assert.That(GetResult(result), Is.EqualTo(NotificationDeliveryResult.NoRecipients));
        Assert.That(apiConnection.InsertCount, Is.EqualTo(1));
        Assert.That(apiConnection.LastSentUpdateCount, Is.EqualTo(1));
    }

    [Test]
    public async Task SendInterfaceDecommission_RejectsMissingReason()
    {
        NotificationController controller = CreateController(new ControllerApiConnection(), new GlobalConfig());

        ActionResult<NotificationDeliveryResult> result = await controller.SendInterfaceDecommission(
            new InterfaceDecommissionNotificationParameters { ConnectionId = 10 });

        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task SendInterfaceDecommission_RejectsActiveConnection()
    {
        ControllerApiConnection apiConnection = new()
        {
            Connection = new ModellingConnection { Id = 10, IsInterface = true, AppId = 8 }
        };
        NotificationController controller = CreateController(apiConnection, new GlobalConfig());

        ActionResult<NotificationDeliveryResult> result = await controller.SendInterfaceDecommission(
            new InterfaceDecommissionNotificationParameters { ConnectionId = 10, Reason = "reason" });

        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task SendInterfaceDecommission_RejectsModellerOutsideOwnerScope()
    {
        ControllerApiConnection apiConnection = new() { Connection = DecommissionedConnection() };
        NotificationController controller = CreateController(apiConnection, new GlobalConfig(), Roles.Modeller,
            "{ 7 }");

        ActionResult<NotificationDeliveryResult> result = await controller.SendInterfaceDecommission(
            new InterfaceDecommissionNotificationParameters { ConnectionId = 10, Reason = "reason" });

        Assert.That(result.Result, Is.TypeOf<ForbidResult>());
    }

    [Test]
    public async Task SendInterfaceDecommission_ReturnsSuppressedWhenNoUsersOrNotificationsExist()
    {
        ControllerApiConnection apiConnection = new()
        {
            NotificationExists = false,
            Connection = DecommissionedConnection()
        };
        NotificationController controller = CreateController(apiConnection, new SimulatedGlobalConfig());

        ActionResult<NotificationDeliveryResult> result = await controller.SendInterfaceDecommission(
            new InterfaceDecommissionNotificationParameters { ConnectionId = 10, Reason = "reason" });

        Assert.That(GetResult(result), Is.EqualTo(NotificationDeliveryResult.Suppressed));
    }

    [Test]
    public async Task SendInterfaceDecommission_ReturnsNoRecipientsAndLogsFailure()
    {
        ControllerApiConnection apiConnection = ConfiguredNoRecipientConnection();
        apiConnection.Connection = DecommissionedConnection();
        apiConnection.InterfaceUsers = [new ModellingConnection
        {
            Id = 11,
            AppId = 9,
            App = new FwoOwner { Id = 9, ExtAppId = "APP-9" },
            Name = "Using interface"
        }];
        NotificationController controller = CreateController(apiConnection, new GlobalConfig());

        ActionResult<NotificationDeliveryResult> result = await controller.SendInterfaceDecommission(
            new InterfaceDecommissionNotificationParameters { ConnectionId = 10, Reason = "reason" });

        Assert.That(GetResult(result), Is.EqualTo(NotificationDeliveryResult.NoRecipients));
        Assert.That(apiConnection.InsertCount, Is.EqualTo(1));
        Assert.That(apiConnection.LastSentUpdateCount, Is.EqualTo(1));
    }

    [TestCase(10, false, true, "Public", false, null)]
    [TestCase(11, false, true, "Public", false, null)]
    [TestCase(11, true, false, "Public", false, null)]
    [TestCase(11, true, true, "Private", false, null)]
    [TestCase(11, true, true, "Public", true, "Decommissioned")]
    [TestCase(11, true, true, "Public", false, "Rejected")]
    public async Task SendInterfaceDecommission_RejectsIneligibleReplacement(
        int replacementId, bool isInterface, bool isPublished, string interfacePermission, bool removed,
        string? lifecycleProperty)
    {
        ControllerApiConnection apiConnection = new()
        {
            NotificationExists = false,
            Connection = DecommissionedConnection(),
            ReplacementConnection = new ModellingConnection
            {
                Id = replacementId,
                IsInterface = isInterface,
                IsPublished = isPublished,
                Removed = removed,
                Properties = lifecycleProperty == null ? "" : $"{{\"{lifecycleProperty}\":\"\"}}",
                InterfacePermission = interfacePermission
            }
        };
        NotificationController controller = CreateController(apiConnection, new GlobalConfig());

        ActionResult<NotificationDeliveryResult> result = await controller.SendInterfaceDecommission(
            new InterfaceDecommissionNotificationParameters
            {
                ConnectionId = 10,
                ReplacementConnectionId = replacementId,
                Reason = "reason"
            });

        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public void CombineDeliveryResults_PrioritizesPartialFailureOverDelivery()
    {
        MethodInfo method = typeof(NotificationController).GetMethod(
            "CombineDeliveryResults", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(typeof(NotificationController).FullName, "CombineDeliveryResults");
        object?[] arguments = [true, true, true];

        NotificationDeliveryResult result = (NotificationDeliveryResult)method.Invoke(null, arguments)!;

        Assert.That(result, Is.EqualTo(NotificationDeliveryResult.Failed));
    }

    private static string? GetRoles(string methodName)
    {
        MethodInfo method = typeof(NotificationController).GetMethod(methodName)!;
        return method.GetCustomAttribute<AuthorizeAttribute>()?.Roles;
    }

    private static NotificationDeliveryResult GetResult(ActionResult<NotificationDeliveryResult> result)
    {
        return (NotificationDeliveryResult)((OkObjectResult)result.Result!).Value!;
    }

    private static NotificationController CreateController(ControllerApiConnection apiConnection, GlobalConfig globalConfig,
        string role = Roles.Admin, string? editableOwners = null)
    {
        List<Claim> claims = [new Claim(ClaimTypes.Role, role)];
        if (editableOwners != null)
        {
            claims.Add(new Claim("x-hasura-editable-owners", editableOwners));
        }
        return new NotificationController(apiConnection, globalConfig)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
                }
            }
        };
    }

    private static ModellingConnection RequestedConnection()
    {
        return new ModellingConnection
        {
            Id = 10,
            ProposedAppId = 8,
            ProposedApp = new FwoOwner { Id = 8, ExtAppId = "APP-8" },
            IsInterface = true,
            IsRequested = true,
            TicketId = 20,
            Name = "Requested interface"
        };
    }

    private static ModellingConnection DecommissionedConnection()
    {
        return new ModellingConnection
        {
            Id = 10,
            AppId = 8,
            App = new FwoOwner { Id = 8, ExtAppId = "APP-8" },
            IsInterface = true,
            Removed = true,
            Name = "Old interface"
        };
    }

    private static ControllerApiConnection ConfiguredNoRecipientConnection()
    {
        return new ControllerApiConnection
        {
            InsertedId = 9,
            Logging = NotificationLoggingMode.SendAndLog,
            RecipientTo = EmailRecipientOption.None,
            Connection = RequestedConnection(),
            Ticket = new WfTicket()
        };
    }

    private sealed class ControllerApiConnection : SimulatedApiConnection
    {
        public int InsertedId { get; init; }
        public string Logging { get; init; } = NotificationLoggingMode.SendOnly;
        public bool Active { get; init; } = true;
        public bool NotificationExists { get; init; } = true;
        public NotificationClient NotificationClient { get; init; } = NotificationClient.InterfaceRequest;
        public NotificationDeadline Deadline { get; init; } = NotificationDeadline.None;
        public EmailRecipientOption RecipientTo { get; init; } = EmailRecipientOption.OtherAddresses;
        public ModellingConnection? Connection { get; set; }
        public ModellingConnection? ReplacementConnection { get; init; }
        public List<ModellingConnection> InterfaceUsers { get; set; } = [];
        public WfTicket? Ticket { get; init; }
        public FwoOwner? Owner { get; init; }
        public int OwnerQueryCount { get; private set; }
        public int InsertCount { get; private set; }
        public int UpdateCount { get; private set; }
        public int LastSentUpdateCount { get; private set; }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null,
            string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            if (typeof(QueryResponseType) == typeof(List<ModellingConnection>) && query == ModellingQueries.getConnectionForNotification)
            {
                int requestedId = variables?.GetType().GetProperty("id")?.GetValue(variables) is int id ? id : 0;
                ModellingConnection? selectedConnection = requestedId == ReplacementConnection?.Id
                    ? ReplacementConnection
                    : Connection;
                List<ModellingConnection> result = selectedConnection == null ? [] : [selectedConnection];
                return Task.FromResult((QueryResponseType)(object)result);
            }
            if (typeof(QueryResponseType) == typeof(List<ModellingConnection>) && query == ModellingQueries.getInterfaceUsers)
            {
                return Task.FromResult((QueryResponseType)(object)InterfaceUsers);
            }
            if (typeof(QueryResponseType) == typeof(WfTicket) && query == RequestQueries.getTicketById)
            {
                return Task.FromResult((QueryResponseType)(object)(Ticket ?? new WfTicket()));
            }
            if (typeof(QueryResponseType) == typeof(WfTicket) && query == RequestQueries.getTicketRequesterId)
            {
                return Task.FromResult((QueryResponseType)(object)new WfTicket
                {
                    Requester = Ticket?.Requester
                });
            }
            if (typeof(QueryResponseType) == typeof(FwoOwner)
                && (query == OwnerQueries.getOwnerById || query == OwnerQueries.getOwnerForNotification))
            {
                OwnerQueryCount++;
                return Task.FromResult((QueryResponseType)(object)(Owner ?? new FwoOwner()));
            }
            if (typeof(QueryResponseType) == typeof(List<Ldap>) && query == AuthQueries.getLdapConnections)
            {
                return Task.FromResult((QueryResponseType)(object)new List<Ldap>());
            }
            if (typeof(QueryResponseType) == typeof(List<OwnerResponsibleType>) && query == OwnerQueries.getOwnerResponsibleTypes)
            {
                return Task.FromResult((QueryResponseType)(object)new List<OwnerResponsibleType>());
            }
            if (typeof(QueryResponseType) == typeof(List<UiUser>) && query == AuthQueries.getUserEmails)
            {
                return Task.FromResult((QueryResponseType)(object)new List<UiUser>());
            }
            if (typeof(QueryResponseType) == typeof(ReturnIdWrapper))
            {
                InsertCount++;
                return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                {
                    ReturnIds = [new ReturnId { Id = InsertedId }]
                });
            }
            if (typeof(QueryResponseType) == typeof(ReturnId)
                && query == NotificationQueries.updateNotificationsLastSent)
            {
                LastSentUpdateCount++;
                return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = 1 });
            }
            if (typeof(QueryResponseType) == typeof(ReturnId))
            {
                UpdateCount++;
                return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = 1 });
            }
            if (typeof(QueryResponseType) == typeof(List<FwoNotification>))
            {
                if (!NotificationExists)
                {
                    return Task.FromResult((QueryResponseType)(object)new List<FwoNotification>());
                }
                string response = JsonConvert.SerializeObject(new
                {
                    id = 4,
                    notification_client = NotificationClient.ToString(),
                    deadline = Deadline.ToString(),
                    recipient_to = RecipientTo.ToString(),
                    email_address_to = "",
                    recipient_cc = EmailRecipientOption.None.ToString(),
                    email_address_cc = "",
                    recipient_bcc = EmailRecipientOption.None.ToString(),
                    email_address_bcc = "",
                    logging = Logging,
                    active = Active
                });
                List<FwoNotification> notifications = JsonConvert.DeserializeObject<List<FwoNotification>>($"[{response}]")!;
                return Task.FromResult((QueryResponseType)(object)notifications);
            }
            if (typeof(QueryResponseType) == typeof(List<NotificationLogEntry>)
                && (query == NotificationQueries.getNoRecipientNotificationLogs
                    || query == NotificationQueries.getNoRecipientNotificationLogsWithoutDeadline))
            {
                return Task.FromResult((QueryResponseType)(object)new List<NotificationLogEntry>());
            }
            throw new InvalidOperationException($"Unexpected query: {query}");
        }
    }
}
