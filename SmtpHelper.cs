using System;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace ExpressBackup
{
    public class Smtp
    {
        public string
            Host,
            User,
            Password,
            Mail;
        public int
            Port = 25;
    }

    static class SmtpHelper
    {        
        public static void Send(Smtp smtp, string to, string topic, string body, bool throwException = false)
        {
            foreach (var mail in (to ?? string.Empty).Split(';').Select(e => e.Trim()).Where(e => e != string.Empty))
                SendInternal(smtp, mail, topic, body, throwException);
        }

        static void SendInternal(Smtp smtp, string to, string topic, string body, bool throwException)
        {
            using (var mail = new MailMessage(new MailAddress(smtp.Mail), new MailAddress(to)))
            {
                try
                {
                    mail.ReplyTo = new MailAddress(smtp.Mail);
                    mail.IsBodyHtml = true;
                    mail.BodyEncoding = Encoding.UTF8;

                    mail.Subject = topic;
                    mail.Body = body;

                    var
                        client = new SmtpClient(smtp.Host)
                        {
                            DeliveryMethod = SmtpDeliveryMethod.Network,
                            Port = smtp.Port
                        };

                    if (!string.IsNullOrEmpty(smtp.User))
                    {
                        client.UseDefaultCredentials = false;
                        client.Credentials = new NetworkCredential(smtp.User, smtp.Password);
                    }

                    client.Send(mail);
                }
                catch (Exception ex)
                {
                    Log.Entry(LogSeverity.Error, "failed to send mail to {0}: {1}", to, ex);
                    if (throwException)
                        throw;
                }
            }
        }
    }
}
