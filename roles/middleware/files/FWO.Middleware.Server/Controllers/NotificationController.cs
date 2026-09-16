using FWO.Api.Client;
using FWO.Api.Client.Queries;
using FWO.Basics;
using FWO.Config.Api;
using FWO.Data;
using FWO.Data.Middleware;
using FWO.Encryption;
using FWO.Logging;
using FWO.Mail;
using FWO.Services;
using FWO.Middleware.Server.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.IO;

namespace FWO.Middleware.Server.Controllers;

/// <summary>
/// Performs rendered notification email delivery in the trusted middleware process.
/// </summary>
[Authorize]
[ApiController]
[Route("api/notification")]
public class NotificationController(ApiConnection apiConnection, GlobalConfig globalConfig,
    INotificationEmailSender? emailSender = null) : ControllerBase
{
    /// <summary>
    /// Sends a rendered email and records its delivery outcome using the middleware API role.
    /// </summary>
    [HttpPost("send")]
    [Authorize(Roles = $"{Roles.Admin}, {Roles.FwAdmin}, {Roles.Modeller}, {Roles.WorkflowRolesList}")]
    [ProducesResponseType(typeof(NotificationDeliveryResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(string), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(string), StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<NotificationDeliveryResult>> Send([FromBody] NotificationEmailSendParameters parameters)
    {
        int logId = 0;
        try
        {
            if (!IsValidRequest(parameters))
            {
                return BadRequest("Notification email request is invalid.");
            }

            FwoNotification? notification = await FindAuthorizedNotification(parameters.NotificationId);
            if (notification is null)
            {
                return BadRequest("Notification is not active or does not match the supplied notification context.");
            }

            MailData mail = CreateMail(parameters);
            if (NotificationLoggingMode.ShouldLog(notification.Logging))
            {
                NotificationLogInsertEntry entry = NotificationLogHelper.CreateEntry(notification, mail.To, mail.Cc, mail.Bcc,
                    mail.Subject, parameters.Deadline);
                logId = await NotificationLogHelper.InsertAsync(apiConnection, entry);
            }

            if (!HasRecipients(mail))
            {
                await CompleteLog(logId, NotificationLogStatus.Failed, "No recipients resolved.");
                return Ok(NotificationDeliveryResult.NoRecipients);
            }

            if (!NotificationLoggingMode.ShouldSend(notification.Logging))
            {
                await CompleteLog(logId, NotificationLogStatus.Suppressed);
                return Ok(NotificationDeliveryResult.Suppressed);
            }

            INotificationEmailSender sender = emailSender ?? new NotificationEmailSender();
            EmailConnection connection = emailSender is null ? CreateEmailConnection() : new EmailConnection();
            bool sent = await sender.SendAsync(mail, connection, parameters.Html);
            NotificationDeliveryResult result = sent ? NotificationDeliveryResult.Delivered : NotificationDeliveryResult.Failed;
            await CompleteLog(logId, sent ? NotificationLogStatus.Sent : NotificationLogStatus.Failed,
                sent ? "" : "SMTP delivery failed.");
            return Ok(result);
        }
        catch (Exception exception)
        {
            await CompleteLogSafely(logId, NotificationLogStatus.Failed, exception.Message);
            Log.WriteError("Notification Email", "Could not deliver notification email.", exception);
            return StatusCode(StatusCodes.Status500InternalServerError, "Internal server error");
        }
    }

    private async Task<FwoNotification?> FindAuthorizedNotification(int notificationId)
    {
        List<FwoNotification> notifications = await apiConnection.SendQueryAsync<List<FwoNotification>>(
            NotificationQueries.getNotificationById, new { id = notificationId });
        FwoNotification? notification = notifications.SingleOrDefault();
        return notification is { Active: true } ? notification : null;
    }

    private static bool IsValidRequest(NotificationEmailSendParameters parameters)
    {
        return parameters is not null
            && parameters.NotificationId > 0
            && IsSafeText(parameters.Subject)
            && IsSafeText(parameters.Body)
            && parameters.Attachments is not null
            && parameters.To.All(IsSafeText)
            && parameters.Cc.All(IsSafeText)
            && parameters.Bcc.All(IsSafeText)
            && parameters.Attachments.All(IsValidAttachment);
    }

    private static bool IsSafeText(string? value)
    {
        return value is not null && value.All(character => !char.IsControl(character));
    }

    private static bool IsValidAttachment(NotificationEmailAttachment? attachment)
    {
        if (attachment is null || !IsSafeText(attachment.FileName) || !IsSafeText(attachment.ContentType)
            || !IsSafeText(attachment.ContentBase64))
        {
            return false;
        }

        try
        {
            Convert.FromBase64String(attachment.ContentBase64);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static MailData CreateMail(NotificationEmailSendParameters parameters)
    {
        MailData mail = new(parameters.To, parameters.Subject)
        {
            Body = parameters.Body,
            Cc = parameters.Cc,
            Bcc = parameters.Bcc
        };
        if (parameters.Attachments.Count > 0)
        {
            FormFileCollection attachments = new();
            foreach (NotificationEmailAttachment attachmentData in parameters.Attachments)
            {
                byte[] content = Convert.FromBase64String(attachmentData.ContentBase64);
                FormFile attachment = new(new MemoryStream(content), 0, content.Length, "attachment", attachmentData.FileName)
                {
                    Headers = new HeaderDictionary(),
                    ContentType = attachmentData.ContentType
                };
                attachments.Add(attachment);
            }
            mail.Attachments = attachments;
        }
        return mail;
    }

    private static bool HasRecipients(MailData mail)
    {
        return mail.To.Count > 0 || mail.Cc.Count > 0 || mail.Bcc.Count > 0;
    }

    private EmailConnection CreateEmailConnection()
    {
        string decryptedSecret = AesEnc.TryDecrypt(globalConfig.EmailPassword, false, "NotificationController",
            "Could not decrypt mailserver password.");
        return new EmailConnection(globalConfig.EmailServerAddress, globalConfig.EmailPort, globalConfig.EmailTls,
            globalConfig.EmailUser, decryptedSecret, globalConfig.EmailSenderAddress);
    }

    private async Task CompleteLog(int logId, NotificationLogStatus status, string error = "")
    {
        if (logId > 0)
        {
            await NotificationLogHelper.UpdateAsync(apiConnection, logId, status, error);
        }
    }

    private async Task CompleteLogSafely(int logId, NotificationLogStatus status, string error)
    {
        try
        {
            await CompleteLog(logId, status, error);
        }
        catch (Exception updateException)
        {
            Log.WriteError("Notification Log", "Could not update notification log after delivery failure.", updateException);
        }
    }
}
