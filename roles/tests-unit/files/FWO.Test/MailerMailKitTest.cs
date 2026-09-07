using FWO.Mail;
using Microsoft.AspNetCore.Http;
using MimeKit;
using NUnit.Framework;
using System.Reflection;
using System.Text;

namespace FWO.Test
{
    [TestFixture]
    internal class MailerMailKitTest
    {
        [Test]
        public void MailData_InitializesRecipientsAndSubject()
        {
            List<string> recipients = new() { "to@example.test" };

            MailData mail = new(recipients, "Subject")
            {
                Body = "Body"
            };

            Assert.Multiple(() =>
            {
                Assert.That(mail.To, Is.SameAs(recipients));
                Assert.That(mail.Cc, Is.Empty);
                Assert.That(mail.Bcc, Is.Empty);
                Assert.That(mail.Subject, Is.EqualTo("Subject"));
                Assert.That(mail.Body, Is.EqualTo("Body"));
            });
        }

        [Test]
        public void CopyMailData_CopiesCollectionsWithoutSharingThem()
        {
            MailData source = new(new List<string> { "to@example.test" }, "Subject")
            {
                Cc = new List<string> { "cc@example.test" },
                Bcc = new List<string> { "bcc@example.test" },
                From = "from@example.test",
                DisplayName = "Sender",
                ReplyTo = "reply@example.test",
                ReplyToName = "Reply",
                Body = "Body"
            };

            MailData copy = Invoke<MailData>("CopyMailData", source);

            Assert.Multiple(() =>
            {
                Assert.That(copy.To, Is.EqualTo(source.To));
                Assert.That(copy.Cc, Is.EqualTo(source.Cc));
                Assert.That(copy.Bcc, Is.EqualTo(source.Bcc));
                Assert.That(copy.To, Is.Not.SameAs(source.To));
                Assert.That(copy.Cc, Is.Not.SameAs(source.Cc));
                Assert.That(copy.Bcc, Is.Not.SameAs(source.Bcc));
                Assert.That(copy.From, Is.EqualTo(source.From));
                Assert.That(copy.DisplayName, Is.EqualTo(source.DisplayName));
                Assert.That(copy.ReplyTo, Is.EqualTo(source.ReplyTo));
                Assert.That(copy.ReplyToName, Is.EqualTo(source.ReplyToName));
                Assert.That(copy.Body, Is.EqualTo(source.Body));
            });
        }

        [TestCase(false)]
        [TestCase(true)]
        public void HasRecipients_RecognizesPresentAddresses(bool present)
        {
            MailData mail = new(new List<string> { present ? "to@example.test" : " " }, "Subject")
            {
                Cc = new List<string> { present ? "cc@example.test" : " " }
            };

            bool result = Invoke<bool>("HasRecipients", mail);

            Assert.That(result, Is.EqualTo(present));
        }

        [Test]
        public void RemoveRecipient_RemovesMatchingAddressFromAnyRecipientList()
        {
            MailData mail = new(new List<string> { "to@example.test" }, "Subject")
            {
                Cc = new List<string> { " CC@example.test " },
                Bcc = new List<string> { "bcc@example.test" }
            };

            bool removed = Invoke<bool>("RemoveRecipient", mail, "cc@example.test");

            Assert.Multiple(() =>
            {
                Assert.That(removed, Is.True);
                Assert.That(mail.Cc, Is.Empty);
                Assert.That(mail.To, Is.EqualTo(new List<string> { "to@example.test" }));
                Assert.That(mail.Bcc, Is.EqualTo(new List<string> { "bcc@example.test" }));
            });
        }

        [Test]
        public void RemoveRecipient_ReturnsFalseWhenAddressIsUnknown()
        {
            MailData mail = new(new List<string> { "to@example.test" }, "Subject");

            bool removed = Invoke<bool>("RemoveRecipient", mail, "missing@example.test");

            Assert.That(removed, Is.False);
        }

        [Test]
        public void RemoveRecipient_SearchesToAndBccAfterEarlierLists()
        {
            MailData mail = new(new List<string> { "to@example.test" }, "Subject")
            {
                Cc = new List<string> { "cc@example.test" },
                Bcc = new List<string> { "bcc@example.test" }
            };

            bool removedTo = Invoke<bool>("RemoveRecipient", mail, "to@example.test");
            bool removedBcc = Invoke<bool>("RemoveRecipient", mail, "bcc@example.test");

            Assert.Multiple(() =>
            {
                Assert.That(removedTo, Is.True);
                Assert.That(removedBcc, Is.True);
                Assert.That(mail.To, Is.Empty);
                Assert.That(mail.Bcc, Is.Empty);
            });
        }

        [Test]
        public void AddRecipients_AddsSenderRecipientsAndReplyTo()
        {
            MailData mail = new(new List<string> { "to@example.test", " " }, "Subject")
            {
                Cc = new List<string> { "cc@example.test" },
                Bcc = new List<string> { " bcc@example.test " },
                ReplyTo = "reply@example.test",
                ReplyToName = "Reply"
            };
            EmailConnection connection = new() { SenderEmailAddress = "sender@example.test" };
            MimeMessage message = new();

            Invoke("AddRecipients", connection, mail, message);

            Assert.Multiple(() =>
            {
                Assert.That(message.From.Mailboxes.Single().Address, Is.EqualTo("sender@example.test"));
                Assert.That(message.Sender?.Address, Is.EqualTo("sender@example.test"));
                Assert.That(message.To.Mailboxes.Select(x => x.Address), Is.EqualTo(new List<string> { "to@example.test" }));
                Assert.That(message.Cc.Mailboxes.Single().Address, Is.EqualTo("cc@example.test"));
                Assert.That(message.Bcc.Mailboxes.Single().Address, Is.EqualTo("bcc@example.test"));
                Assert.That(message.ReplyTo.Mailboxes.Single().Address, Is.EqualTo("reply@example.test"));
                Assert.That(message.ReplyTo.Mailboxes.Single().Name, Is.EqualTo("Reply"));
            });
        }

        [Test]
        public async Task AddContent_CreatesPlainTextBodyAndAttachments()
        {
            FormFile attachment = CreateFile("attachment.txt", "text/plain", "attachment");
            MailData mail = new(new List<string>(), "Subject")
            {
                Body = "Plain body",
                Attachments = new FormFileCollection { attachment }
            };
            MimeMessage message = new();

            await InvokeAsync("AddContent", mail, message, false);

            MimePart attachmentPart = (MimePart)message.Attachments.Single();
            Assert.Multiple(() =>
            {
                Assert.That(message.Subject, Is.EqualTo("Subject"));
                Assert.That(message.TextBody, Is.EqualTo("Plain body"));
                Assert.That(attachmentPart.FileName, Is.EqualTo("attachment.txt"));
                Assert.That(attachmentPart.ContentType.MimeType, Is.EqualTo("text/plain"));
            });
        }

        [Test]
        public async Task AddContent_SkipsEmptyAttachments()
        {
            FormFile attachment = CreateFile("empty.txt", "text/plain", "");
            MailData mail = new(new List<string>(), "Subject")
            {
                Body = "Plain body",
                Attachments = new FormFileCollection { attachment }
            };
            MimeMessage message = new();

            await InvokeAsync("AddContent", mail, message, false);

            Assert.That(message.Attachments, Is.Empty);
        }

        [Test]
        public async Task AddContent_CreatesHtmlBodyWithoutAttachments()
        {
            MailData mail = new(new List<string>(), "Subject") { Body = "<p>HTML body</p>" };
            MimeMessage message = new();

            await InvokeAsync("AddContent", mail, message, true);

            Assert.Multiple(() =>
            {
                Assert.That(message.HtmlBody, Is.EqualTo("<p>HTML body</p>"));
                Assert.That(message.TextBody, Is.Null);
            });
        }

        [TestCase(EmailEncryptionMethod.None)]
        [TestCase(EmailEncryptionMethod.StartTls)]
        [TestCase(EmailEncryptionMethod.Tls)]
        public async Task SendAsync_ReturnsFalseWhenConnectionFails(EmailEncryptionMethod encryption)
        {
            MailData mail = new(new List<string> { "to@example.test" }, "Subject");
            EmailConnection connection = new("127.0.0.1", 1, encryption, "", "", "sender@example.test");

            bool result = await MailKitMailer.SendAsync(mail, connection);

            Assert.That(result, Is.False);
        }

        private static FormFile CreateFile(string fileName, string contentType, string content)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(content);
            MemoryStream stream = new(bytes);
            return new FormFile(stream, 0, bytes.Length, "file", fileName)
            {
                Headers = new HeaderDictionary(),
                ContentType = contentType
            };
        }

        private static MethodInfo GetPrivateMethod(string name, object?[] arguments)
        {
            return typeof(MailKitMailer).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .Single(method => method.Name == name
                    && method.GetParameters().Length == arguments.Length
                    && method.GetParameters().Select((parameter, index) => parameter.ParameterType.IsInstanceOfType(arguments[index])).All(result => result))
                ?? throw new MissingMethodException(typeof(MailKitMailer).FullName, name);
        }

        private static T Invoke<T>(string name, params object?[] arguments)
        {
            return (T)(GetPrivateMethod(name, arguments).Invoke(null, arguments)
                ?? throw new InvalidOperationException($"Private method '{name}' returned null."));
        }

        private static void Invoke(string name, params object?[] arguments)
        {
            GetPrivateMethod(name, arguments).Invoke(null, arguments);
        }

        private static async Task InvokeAsync(string name, params object?[] arguments)
        {
            await (Task)(GetPrivateMethod(name, arguments).Invoke(null, arguments)
                ?? throw new InvalidOperationException($"Private method '{name}' returned null."));
        }
    }
}
