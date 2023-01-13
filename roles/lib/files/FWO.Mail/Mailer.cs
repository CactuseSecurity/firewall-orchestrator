using System.Net.Mail;

namespace FWO.Mail
{
    public class Mailer
    {

        EmailConnection EmailConn;
        public Mailer(EmailConnection conn)
        {
            EmailConn = conn;
        }

        public void SendMail(string subject, string body, string mailTo)
        {
            MailMessage mm = new MailMessage();
            SmtpClient smtp = new SmtpClient();

            mm.From = new MailAddress(EmailConn.SenderEmailAddress, "", System.Text.Encoding.UTF8);
            mm.To.Add(new MailAddress(mailTo));
            mm.Subject = subject;
            mm.Body = body;
            mm.IsBodyHtml = true;
            smtp.Host = EmailConn.ServerAddress;
            smtp.EnableSsl = EmailConn.Tls;
            smtp.Port = EmailConn.Port;
            smtp.Timeout = 5000;

            if (EmailConn.User != "")
            {
                System.Net.NetworkCredential NetworkCred = new System.Net.NetworkCredential();
                NetworkCred.UserName = EmailConn.User;
                NetworkCred.Password = EmailConn.Password;
                smtp.Credentials = NetworkCred;
            }
            else
            {
                smtp.UseDefaultCredentials = true;
            }
            smtp.Send(mm);
        }
    }

}
