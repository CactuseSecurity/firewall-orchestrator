// source: https://blog.christian-schou.dk/send-emails-with-asp-net-core-with-mailkit/

using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;
using Microsoft.AspNetCore.Http;
using FWO.Encryption;

namespace FWO.Mail
{
    public class MailData(List<string> to, string subject)
    {
        // Receiver
        public List<string> To { get; } = to;
        public List<string> Bcc { get; set; } = [];

        public List<string> Cc { get; set; } = [];

        // Sender
        public string? From { get; set; }

        public string? DisplayName { get; set; }

        public string? ReplyTo { get; set; }

        public string? ReplyToName { get; set; }

        // Content
        public string Subject { get; } = subject;

        public string? Body { get; set; }

        public IFormFileCollection? Attachments { get; set; }
    }

    public interface IMailService
    {
        Task<bool> SendAsync(MailData mailData, EmailConnection emailConn, CancellationToken ct);
    }

    public static class MailKitMailer
    {
        public static async Task<bool> SendAsync(
            MailData mailData,
            EmailConnection emailConn,
            bool mailFormatHtml = false,
            CancellationToken ct = default
        )
        {
            try
            {
                var mail = new MimeMessage();
                AddRecipients(emailConn, mailData, mail);
                await AddContent(mailData, mail, mailFormatHtml);

                using var smtp = new SmtpClient();
                smtp.Timeout = 5000;

                switch (emailConn.Encryption)
                {
                    case EmailEncryptionMethod.None:
                        await smtp.ConnectAsync(
                            emailConn.ServerAddress,
                            emailConn.Port,
                            SecureSocketOptions.None,
                            ct
                        );
                        break;
                    case EmailEncryptionMethod.StartTls:
                        smtp.ServerCertificateValidationCallback = (s, c, h, e) => true; //accept all SSL certificates
                        await smtp.ConnectAsync(
                            emailConn.ServerAddress,
                            emailConn.Port,
                            SecureSocketOptions.StartTls,
                            ct
                        );
                        break;
                    case EmailEncryptionMethod.Tls:
                        smtp.ServerCertificateValidationCallback = (s, c, h, e) => true; //accept all SSL certificates
                        await smtp.ConnectAsync(
                            emailConn.ServerAddress,
                            emailConn.Port,
                            SecureSocketOptions.SslOnConnect,
                            ct
                        );
                        break;
                }
                if (emailConn.User != null && emailConn.User != "")
                {
                    string mainKey = AesEnc.GetMainKey();
                    string decryptedSecret = emailConn.Password ?? "";
                    try
                    {
                        decryptedSecret = AesEnc.Decrypt(emailConn.Password ?? "", mainKey);
                    }
                    catch (Exception)
                    {
                        // decryption failed, password is assumed to be uncrypted
                    }

                    await smtp.AuthenticateAsync(emailConn.User, decryptedSecret, ct);
                }
                await smtp.SendAsync(mail, ct);
                await smtp.DisconnectAsync(true, ct);

                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void AddRecipients(EmailConnection emailConn, MailData mailData, MimeMessage mail)
        {
            string senderString = "";
            if (emailConn.SenderEmailAddress != null)
            {
                senderString = emailConn.SenderEmailAddress;
            }

            // Sender
            mail.From.Add(new MailboxAddress(senderString, senderString));
            mail.Sender = new MailboxAddress(senderString, senderString);

            // Receiver
            foreach (string mailAddress in mailData.To)
            {
                mail.To.Add(MailboxAddress.Parse(mailAddress));
            }

            // Set Reply to if specified in mail data
            if (!string.IsNullOrEmpty(mailData.ReplyTo))
            {
                mail.ReplyTo.Add(new MailboxAddress(mailData.ReplyToName, mailData.ReplyTo));
            }

            // BCC
            // Check if a BCC was supplied in the request
            if (mailData.Bcc != null)
            {
                // Get only addresses where value is not null or with whitespace. x = value of address
                foreach (string mailAddress in mailData.Bcc.Where(x => !string.IsNullOrWhiteSpace(x)))
                {
                    mail.Bcc.Add(MailboxAddress.Parse(mailAddress.Trim()));
                }
            }

            // CC
            // Check if a CC address was supplied in the request
            if (mailData.Cc != null)
            {
                foreach (string mailAddress in mailData.Cc.Where(x => !string.IsNullOrWhiteSpace(x)))
                {
                    mail.Cc.Add(MailboxAddress.Parse(mailAddress.Trim()));
                }
            }
        }

        private static async Task AddContent(MailData mailData, MimeMessage mail, bool mailFormatHtml)
        {
            var body = new BodyBuilder();
            mail.Subject = mailData.Subject;
            if (mailFormatHtml)
            {
                body.HtmlBody = mailData.Body;
            }
            else
            {
                body.TextBody = mailData.Body;

                // Check if we got any attachments and add the to the builder for our message
                await AddAttachments(mailData, body);

                mail.Body = body.ToMessageBody(); // correction compared to source code
            }
        }

        private static async Task AddAttachments(MailData mailData, MimeKit.BodyBuilder body)
        {
            if (mailData.Attachments != null)
            {
                byte[] attachmentFileByteArray;

                foreach (IFormFile attachment in mailData.Attachments)
                {
                    // Check if length of the file in bytes is larger than 0
                    if (attachment.Length > 0)
                    {
                        // Create a new memory stream and attach attachment to mail body
                        using (MemoryStream memoryStream = new())
                        {
                            // Copy the attachment to the stream
                            await attachment.CopyToAsync(memoryStream);
                            attachmentFileByteArray = memoryStream.ToArray();
                        }
                        // Add the attachment from the byte array
                        body.Attachments.Add(attachment.FileName, attachmentFileByteArray, ContentType.Parse(attachment.ContentType));
                    }
                }
            }
        }
    }
}
