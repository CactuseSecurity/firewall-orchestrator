using FWO.Mail;

namespace FWO.Middleware.Server.Services;

/// <summary>
/// Sends a prepared notification email.
/// </summary>
public interface INotificationEmailSender
{
    /// <summary>
    /// Sends the prepared email using the supplied connection settings.
    /// </summary>
    /// <param name="mail">Prepared email including recipients, body, and attachments.</param>
    /// <param name="connection">SMTP connection settings.</param>
    /// <param name="html">Whether the body should be sent as HTML.</param>
    /// <returns>True when the mail server accepts the email; otherwise false.</returns>
    Task<bool> SendAsync(MailData mail, EmailConnection connection, bool html);
}

/// <summary>
/// Production notification sender backed by MailKit.
/// </summary>
public sealed class NotificationEmailSender : INotificationEmailSender
{
    /// <inheritdoc />
    public Task<bool> SendAsync(MailData mail, EmailConnection connection, bool html)
    {
        return MailKitMailer.SendAsync(mail, connection, html, new());
    }
}
