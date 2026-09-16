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
using NUnit.Framework;
using System.Reflection;

namespace FWO.Test;

[TestFixture]
internal class NotificationControllerTest
{
    [Test]
    public async Task Send_RejectsUnknownNotification()
    {
        NotificationControllerApiConnection apiConnection = new() { NotificationExists = false };
        NotificationController controller = new(apiConnection, new GlobalConfig());
        NotificationEmailSendParameters parameters = CreateValidParameters();

        ActionResult<NotificationDeliveryResult> result = await controller.Send(parameters);

        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task Send_RejectsControlCharactersInRenderedContent()
    {
        NotificationController controller = new(new NotificationControllerApiConnection(), new GlobalConfig());
        NotificationEmailSendParameters parameters = CreateValidParameters();
        parameters.Body = "body\nforged-log-entry";

        ActionResult<NotificationDeliveryResult> result = await controller.Send(parameters);

        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task Send_RejectsInvalidRequestId()
    {
        NotificationController controller = new(new NotificationControllerApiConnection(), new GlobalConfig());
        NotificationEmailSendParameters parameters = CreateValidParameters();
        parameters.NotificationId = 0;

        ActionResult<NotificationDeliveryResult> result = await controller.Send(parameters);

        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task Send_RejectsUnknownNotificationId()
    {
        NotificationController controller = new(new NotificationControllerApiConnection { NotificationExists = false }, new GlobalConfig());
        NotificationEmailSendParameters parameters = CreateValidParameters();
        parameters.NotificationId = 99;

        ActionResult<NotificationDeliveryResult> result = await controller.Send(parameters);

        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task Send_RejectsInactiveNotification()
    {
        NotificationControllerApiConnection apiConnection = new() { Active = false };
        NotificationController controller = new(apiConnection, new GlobalConfig());

        ActionResult<NotificationDeliveryResult> result = await controller.Send(CreateValidParameters());

        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task Send_LogOnly_CompletesLogWithoutSmtpDelivery()
    {
        NotificationControllerApiConnection apiConnection = new() { InsertedId = 9, Logging = NotificationLoggingMode.LogOnly };
        NotificationController controller = new(apiConnection, new GlobalConfig());

        ActionResult<NotificationDeliveryResult> result = await controller.Send(CreateValidParameters());

        Assert.That(((OkObjectResult)result.Result!).Value, Is.EqualTo(NotificationDeliveryResult.Suppressed));
        Assert.That(apiConnection.InsertCount, Is.EqualTo(1));
        Assert.That(apiConnection.UpdateCount, Is.EqualTo(1));
    }

    [Test]
    public async Task Send_SendOnly_DeliversWithAllAttachments()
    {
        NotificationControllerApiConnection apiConnection = new() { InsertedId = 9 };
        RecordingNotificationEmailSender emailSender = new() { SendResult = true };
        NotificationController controller = new(apiConnection, new GlobalConfig(), emailSender);
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
        NotificationController controller = new(new NotificationControllerApiConnection(), new GlobalConfig(), emailSender);

        ActionResult<NotificationDeliveryResult> result = await controller.Send(CreateValidParameters());

        Assert.That(((OkObjectResult)result.Result!).Value, Is.EqualTo(NotificationDeliveryResult.Delivered));
        Assert.That(emailSender.Mail?.Attachments, Is.Null);
    }

    [Test]
    public async Task Send_SendAndLog_ReturnsFailedWhenMailerFails()
    {
        NotificationControllerApiConnection apiConnection = new() { InsertedId = 9, Logging = NotificationLoggingMode.SendAndLog };
        NotificationController controller = new(apiConnection, new GlobalConfig(), new RecordingNotificationEmailSender());

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
        NotificationController controller = new(apiConnection, new GlobalConfig(), new RecordingNotificationEmailSender { SendResult = true });

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
        NotificationController controller = new(apiConnection, new GlobalConfig(), new RecordingNotificationEmailSender());
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
        NotificationController controller = new(new NotificationControllerApiConnection(), new GlobalConfig());
        NotificationEmailSendParameters parameters = CreateValidParameters();
        parameters.Attachments = [new NotificationEmailAttachment { ContentBase64 = "not-base64" }];

        ActionResult<NotificationDeliveryResult> result = await controller.Send(parameters);

        Assert.That(result.Result, Is.TypeOf<BadRequestObjectResult>());
    }

    [Test]
    public async Task Send_RejectsAttachmentWithUnsafeFileName()
    {
        NotificationController controller = new(new NotificationControllerApiConnection(), new GlobalConfig());
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
        NotificationController controller = new(new NotificationControllerApiConnection(), new GlobalConfig(),
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

                List<FwoNotification> notifications =
                [
                    new FwoNotification
                    {
                        Id = 4,
                        NotificationClient = this.NotificationClient,
                        Deadline = this.Deadline,
                        Active = this.Active,
                        Logging = this.Logging
                    }
                ];
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
