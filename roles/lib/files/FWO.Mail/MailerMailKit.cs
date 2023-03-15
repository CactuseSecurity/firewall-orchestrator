// source: https://blog.christian-schou.dk/send-emails-with-asp-net-core-with-mailkit/

using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace FWO.Mail
{
    public class MailData
    {
        // Receiver
        public List<string> To { get; }
        public List<string> Bcc { get; }

        public List<string> Cc { get; }

        // Sender
        public string? From { get; }

        public string? DisplayName { get; }

        public string? ReplyTo { get; }

        public string? ReplyToName { get; }

        // Content
        public string Subject { get; }

        public string? Body { get; }

        public MailData(List<string> to, string subject, string? body = null, string? from = null, string? displayName = null, string? replyTo = null, string? replyToName = null, List<string>? bcc = null, List<string>? cc = null)
        {
            // Receiver
            To = to;
            Bcc = bcc ?? new List<string>();
            Cc = cc ?? new List<string>();

            // Sender
            From = from;
            DisplayName = displayName;
            ReplyTo = replyTo;
            ReplyToName = replyToName;
            
            // Content
            Subject = subject;
            Body = body;
        }
    }

    public interface IMailService
    {
        Task<bool> SendAsync(MailData mailData, EmailConnection emailConn, CancellationToken ct);
    }

    public class MailKitMailer
    {
        private EmailConnection EmailConn;

        public MailKitMailer(EmailConnection emailConn)
        {
            EmailConn = emailConn;
        }

        public async Task<bool> SendAsync(MailData mailData, EmailConnection emailConn, CancellationToken ct = default)
        {
            try
            {
                // Initialize a new instance of the MimeKit.MimeMessage class
                var mail = new MimeMessage();
                string senderString = "";
                if (emailConn.SenderEmailAddress != null)
                {
                    senderString = emailConn.SenderEmailAddress;
                }

                #region Sender / Receiver
                // Sender
                mail.From.Add(new MailboxAddress(senderString, senderString));
                mail.Sender = new MailboxAddress(senderString, senderString);

                // Receiver
                foreach (string mailAddress in mailData.To)
                    mail.To.Add(MailboxAddress.Parse(mailAddress));

                // Set Reply to if specified in mail data
                if(!string.IsNullOrEmpty(mailData.ReplyTo))
                    mail.ReplyTo.Add(new MailboxAddress(mailData.ReplyToName, mailData.ReplyTo));

                // BCC
                // Check if a BCC was supplied in the request
                if (mailData.Bcc != null)
                {
                    // Get only addresses where value is not null or with whitespace. x = value of address
                    foreach (string mailAddress in mailData.Bcc.Where(x => !string.IsNullOrWhiteSpace(x)))
                        mail.Bcc.Add(MailboxAddress.Parse(mailAddress.Trim()));
                }

                // CC
                // Check if a CC address was supplied in the request
                if (mailData.Cc != null)
                {
                    foreach (string mailAddress in mailData.Cc.Where(x => !string.IsNullOrWhiteSpace(x)))
                        mail.Cc.Add(MailboxAddress.Parse(mailAddress.Trim()));
                }
                #endregion

                #region Content

                // Add Content to Mime Message
                var body = new BodyBuilder();
                mail.Subject = mailData.Subject;
                body.HtmlBody = mailData.Body;
                mail.Body = body.ToMessageBody();

                #endregion

                #region Send Mail

                using var smtp = new SmtpClient();
                smtp.Timeout = 5000;

                switch (emailConn.Encryption)
                {
                    case EmailEncryptionMethod.None:
                        await smtp.ConnectAsync(emailConn.ServerAddress, emailConn.Port, SecureSocketOptions.None, ct);
                        break;
                    case EmailEncryptionMethod.StartTls:
                        await smtp.ConnectAsync(emailConn.ServerAddress, emailConn.Port, SecureSocketOptions.StartTls, ct);
                        break;
                    case EmailEncryptionMethod.Tls:
                        await smtp.ConnectAsync(emailConn.ServerAddress, emailConn.Port, SecureSocketOptions.SslOnConnect, ct);
                        break;
                }
                if (emailConn.User != null && emailConn.User != "")
                {
                    await smtp.AuthenticateAsync(emailConn.User, emailConn.Password, ct);
                }
                await smtp.SendAsync(mail, ct);
                await smtp.DisconnectAsync(true, ct);
                
                #endregion

                return true;

            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
