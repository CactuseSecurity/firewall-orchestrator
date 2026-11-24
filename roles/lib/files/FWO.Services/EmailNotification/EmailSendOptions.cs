using FWO.Basics.Enums;
using Microsoft.AspNetCore.Http;

namespace FWO.Services
{
    /// <summary>
    /// Format, attachment and priority of one email sent by the email helper.
    /// </summary>
    /// <param name="MailFormatHtml">True to send the body as html, false for plain text.</param>
    /// <param name="Attachment">Optional file attached to the email.</param>
    /// <param name="Priority">Priority headers of the email; Normal writes none.</param>
    public sealed record EmailSendOptions(bool MailFormatHtml = true, FormFile? Attachment = null, EmailPriority Priority = EmailPriority.Normal);
}
