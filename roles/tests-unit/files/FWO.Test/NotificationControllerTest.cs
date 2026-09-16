using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Middleware.Server.Controllers;
using FWO.Middleware.Server.Services;
using FWO.Mail;
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
    public async Task Send_RejectsUnknownNotification()
    {
        NotificationControllerApiConnection apiConnection = new() { NotificationExists = false };
        NotificationController controller = CreateController(apiConnection, new GlobalConfig());
        NotificationEmailSendParameters parameters = CreateValidParameters();

        ActionResult<NotificationDeliveryResult> result = await controller.Send(parameters);

        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task Send_AllowsMultilineRenderedBody()
    {
        RecordingNotificationEmailSender emailSender = new() { SendResult = true };
        NotificationController controller = CreateController(new NotificationControllerApiConnection(), new GlobalConfig(), emailSender);
        NotificationEmailSendParameters parameters = CreateValidParameters();
        parameters.Body = "first line\r\nsecond line";

        ActionResult<NotificationDeliveryResult> result = await controller.Send(parameters);

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>(), result.Result is ObjectResult objectResult
            ? $"Unexpected HTTP status {objectResult.StatusCode}: {objectResult.Value}"
            : "The controller returned no result.");
        Assert.That(((OkObjectResult)result.Result!).Value, Is.EqualTo(NotificationDeliveryResult.Delivered));
        Assert.That(emailSender.Mail?.Body, Is.EqualTo(parameters.Body));
    }

    [Test]
    public async Task Send_RejectsInvalidRequestId()
    {
        NotificationController controller = CreateController(new NotificationControllerApiConnection(), new GlobalConfig());
        NotificationEmailSendParameters parameters = CreateValidParameters();
        parameters.NotificationId = 0;

        ActionResult<NotificationDeliveryResult> result = await controller.Send(parameters);

        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task Send_RejectsUnknownNotificationId()
    {
        NotificationController controller = CreateController(new NotificationControllerApiConnection { NotificationExists = false }, new GlobalConfig());
        NotificationEmailSendParameters parameters = CreateValidParameters();
        parameters.NotificationId = 99;

        ActionResult<NotificationDeliveryResult> result = await controller.Send(parameters);

        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task Send_RejectsInactiveNotification()
    {
        NotificationControllerApiConnection apiConnection = new() { Active = false };
        NotificationController controller = CreateController(apiConnection, new GlobalConfig());

        ActionResult<NotificationDeliveryResult> result = await controller.Send(CreateValidParameters());

        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task Send_RejectsModellerOutsideEditableOwnerScope()
    {
        List<Claim> claims =
        [
            new Claim(ClaimTypes.Role, Roles.Modeller),
            new Claim("x-hasura-editable-owners", "{ 7 }")
        ];
        NotificationController controller = CreateController(new NotificationControllerApiConnection(), new GlobalConfig(),
            new RecordingNotificationEmailSender { SendResult = true });
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
            }
        };
        NotificationEmailSendParameters parameters = CreateValidParameters();
        parameters.OwnerId = 4;

        ActionResult<NotificationDeliveryResult> result = await controller.Send(parameters);

        Assert.That(result.Result, Is.TypeOf<ForbidResult>());
    }

    [Test]
    public async Task Send_LogOnly_CompletesLogWithoutSmtpDelivery()
    {
        NotificationControllerApiConnection apiConnection = new() { InsertedId = 9, Logging = NotificationLoggingMode.LogOnly };
        NotificationController controller = CreateController(apiConnection, new GlobalConfig());

        ActionResult<NotificationDeliveryResult> result = await controller.Send(CreateValidParameters());

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>(), result.Result is ObjectResult objectResult
            ? $"Unexpected HTTP status {objectResult.StatusCode}: {objectResult.Value}"
            : "The controller returned no result.");
        Assert.That(((OkObjectResult)result.Result!).Value, Is.EqualTo(NotificationDeliveryResult.Suppressed));
        Assert.That(apiConnection.InsertCount, Is.EqualTo(1));
        Assert.That(apiConnection.UpdateCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Send_SendOnly_DeliversWithAllAttachments()
    {
        NotificationControllerApiConnection apiConnection = new() { InsertedId = 9 };
        RecordingNotificationEmailSender emailSender = new() { SendResult = true };
        NotificationController controller = CreateController(apiConnection, new GlobalConfig(), emailSender);
        NotificationEmailSendParameters parameters = CreateValidParameters();
        parameters.Attachments =
        [
            new NotificationEmailAttachment { FileName = "one.txt", ContentType = "text/plain", ContentBase64 = Convert.ToBase64String("one"u8.ToArray()) },
            new NotificationEmailAttachment { FileName = "two.txt", ContentType = "text/plain", ContentBase64 = Convert.ToBase64String("two"u8.ToArray()) }
        ];

        ActionResult<NotificationDeliveryResult> result = await controller.Send(parameters);

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>(), result.Result is ObjectResult objectResult
            ? $"Unexpected HTTP status {objectResult.StatusCode}: {objectResult.Value}"
            : "The controller returned no result.");
        Assert.That(((OkObjectResult)result.Result!).Value, Is.EqualTo(NotificationDeliveryResult.Delivered));
        Assert.That(emailSender.Mail, Is.Not.Null);
        Assert.That(emailSender.Mail!.Attachments, Has.Count.EqualTo(2));
        Assert.That(apiConnection.UpdateCount, Is.EqualTo(0));
    }

    [Test]
    public async Task Send_SendOnly_DeliversWithoutAttachments()
    {
        RecordingNotificationEmailSender emailSender = new() { SendResult = true };
        NotificationController controller = CreateController(new NotificationControllerApiConnection(), new GlobalConfig(), emailSender);

        ActionResult<NotificationDeliveryResult> result = await controller.Send(CreateValidParameters());

        Assert.That(((OkObjectResult)result.Result!).Value, Is.EqualTo(NotificationDeliveryResult.Delivered));
        Assert.That(emailSender.Mail?.Attachments, Is.Null);
    }

    [Test]
    public async Task Send_SendAndLog_ReturnsFailedWhenMailerFails()
    {
        NotificationControllerApiConnection apiConnection = new() { InsertedId = 9, Logging = NotificationLoggingMode.SendAndLog };
        NotificationController controller = CreateController(apiConnection, new GlobalConfig(), new RecordingNotificationEmailSender());

        ActionResult<NotificationDeliveryResult> result = await controller.Send(CreateValidParameters());

        Assert.That(((OkObjectResult)result.Result!).Value, Is.EqualTo(NotificationDeliveryResult.Failed));
        Assert.That(apiConnection.UpdateCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Send_SendAndLog_DerivesNotificationMetadataFromStoredNotification()
    {
        NotificationControllerApiConnection apiConnection = new()
        {
            InsertedId = 9,
            Logging = NotificationLoggingMode.SendAndLog,
            NotificationClient = NotificationClient.AppDecomm,
            Deadline = NotificationDeadline.DecommissionDate
        };
        NotificationController controller = CreateController(apiConnection, new GlobalConfig(), new RecordingNotificationEmailSender { SendResult = true });

        ActionResult<NotificationDeliveryResult> result = await controller.Send(CreateValidParameters());

        Assert.That(result.Result, Is.TypeOf<OkObjectResult>());
        Assert.That(apiConnection.InsertedEntries, Has.Count.EqualTo(1));
        Assert.That(apiConnection.InsertedEntries[0].NotificationType, Is.EqualTo(NotificationClient.AppDecomm.ToString()));
        Assert.That(apiConnection.InsertedEntries[0].DeadlineType, Is.EqualTo(NotificationDeadline.DecommissionDate));
    }

    [Test]
    public async Task Send_ReturnsNoRecipientsAndCompletesFailedLog()
    {
        NotificationControllerApiConnection apiConnection = new() { InsertedId = 9, Logging = NotificationLoggingMode.SendAndLog };
        NotificationController controller = CreateController(apiConnection, new GlobalConfig(), new RecordingNotificationEmailSender());
        NotificationEmailSendParameters parameters = CreateValidParameters();
        parameters.To = [];
        parameters.Cc = [];
        parameters.Bcc = [];

        ActionResult<NotificationDeliveryResult> result = await controller.Send(parameters);

        Assert.That(((OkObjectResult)result.Result!).Value, Is.EqualTo(NotificationDeliveryResult.NoRecipients));
        Assert.That(apiConnection.UpdateCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Send_RejectsMalformedAttachment()
    {
        NotificationController controller = CreateController(new NotificationControllerApiConnection(), new GlobalConfig());
        NotificationEmailSendParameters parameters = CreateValidParameters();
        parameters.Attachments = [new NotificationEmailAttachment { ContentBase64 = "not-base64" }];

        ActionResult<NotificationDeliveryResult> result = await controller.Send(parameters);

        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task Send_RejectsAttachmentWithUnsafeFileName()
    {
        NotificationController controller = CreateController(new NotificationControllerApiConnection(), new GlobalConfig());
        NotificationEmailSendParameters parameters = CreateValidParameters();
        parameters.Attachments =
        [
            new NotificationEmailAttachment
            {
                FileName = "attachment\n.txt",
                ContentType = "text/plain",
                ContentBase64 = Convert.ToBase64String("content"u8.ToArray())
            }
        ];

        ActionResult<NotificationDeliveryResult> result = await controller.Send(parameters);

        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task Send_ReturnsInternalServerErrorWhenMailerThrows()
    {
        NotificationController controller = CreateController(new NotificationControllerApiConnection(), new GlobalConfig(),
            new ThrowingNotificationEmailSender());

        ActionResult<NotificationDeliveryResult> result = await controller.Send(CreateValidParameters());

        Assert.That(result.Result, Is.TypeOf<ObjectResult>());
        Assert.That(((ObjectResult)result.Result!).StatusCode, Is.EqualTo(StatusCodes.Status500InternalServerError));
    }

    [Test]
    public void SendEndpoint_IsRestrictedToModellers()
    {
        MethodInfo method = typeof(NotificationController).GetMethod(nameof(NotificationController.Send))!;
        AuthorizeAttribute? authorize = method.GetCustomAttribute<AuthorizeAttribute>();

        Assert.That(authorize?.Roles, Is.EqualTo($"{Roles.Admin}, {Roles.FwAdmin}, {Roles.Modeller}, {Roles.WorkflowRolesList}"));
    }

    private static NotificationController CreateController(ApiConnection apiConnection, GlobalConfig globalConfig,
        INotificationEmailSender? emailSender = null)
    {
        List<Claim> claims = [new Claim(ClaimTypes.Role, Roles.Admin)];
        NotificationController controller = new(apiConnection, globalConfig, emailSender)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(new ClaimsIdentity(claims, "test"))
                }
            }
        };
        return controller;
    }

    private static NotificationEmailSendParameters CreateValidParameters()
    {
        return new NotificationEmailSendParameters
        {
            NotificationId = 4,
            To = ["recipient@example.test"],
            Subject = "subject",
            Body = "body"
        };
    }

    private sealed class NotificationControllerApiConnection : SimulatedApiConnection
    {
        public int InsertedId { get; init; }
        public string Logging { get; init; } = NotificationLoggingMode.SendOnly;
        public bool Active { get; init; } = true;
        public bool NotificationExists { get; init; } = true;
        public NotificationClient NotificationClient { get; init; } = NotificationClient.InterfaceRequest;
        public NotificationDeadline Deadline { get; init; } = NotificationDeadline.None;
        public List<NotificationLogInsertEntry> InsertedEntries { get; } = [];
        public int InsertCount { get; private set; }
        public int UpdateCount { get; private set; }

        public override Task<QueryResponseType> SendQueryAsync<QueryResponseType>(string query, object? variables = null,
            string? operationName = null, QueryChunkingOptions? chunkingOptions = null)
        {
            if (typeof(QueryResponseType) == typeof(ReturnIdWrapper))
            {
                InsertCount++;
                if (variables?.GetType().GetProperty("entries")?.GetValue(variables)
                    is IEnumerable<NotificationLogInsertEntry> entries)
                {
                    InsertedEntries.AddRange(entries);
                }

                return Task.FromResult((QueryResponseType)(object)new ReturnIdWrapper
                {
                    ReturnIds = [new ReturnId { Id = InsertedId }]
                });
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
                    notification_client = this.NotificationClient.ToString(),
                    deadline = this.Deadline.ToString(),
                    logging = this.Logging,
                    active = this.Active
                });
                List<FwoNotification> notifications = JsonConvert.DeserializeObject<List<FwoNotification>>($"[{response}]")!;
                return Task.FromResult((QueryResponseType)(object)notifications);
            }

            if (typeof(QueryResponseType) == typeof(ReturnId))
            {
                UpdateCount++;
                return Task.FromResult((QueryResponseType)(object)new ReturnId { AffectedRows = 1 });
            }

            throw new InvalidOperationException($"Unexpected query: {query}");
        }
    }

    private sealed class RecordingNotificationEmailSender : INotificationEmailSender
    {
        public bool SendResult { get; init; }
        public MailData? Mail { get; private set; }

        public Task<bool> SendAsync(MailData mail, EmailConnection connection, bool html)
        {
            Mail = mail;
            return Task.FromResult(SendResult);
        }
    }

    private sealed class ThrowingNotificationEmailSender : INotificationEmailSender
    {
        public Task<bool> SendAsync(MailData mail, EmailConnection connection, bool html)
        {
            throw new InvalidOperationException("SMTP failed.");
        }
    }
}
