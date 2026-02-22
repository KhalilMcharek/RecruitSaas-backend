using MailKit.Net.Smtp;
using MimeKit;
using System.Threading.Tasks;

namespace Recrutement_api.Services
{
    public class EmailService
    {
        public async Task SendCriticalAlert(string message)
        {
            var email = new MimeMessage();
            email.From.Add(MailboxAddress.Parse("noreply@recruitsaas.com"));
            email.To.Add(MailboxAddress.Parse("admin@recruitsaas.com"));
            email.Subject = " Critical Error Alert";

            email.Body = new TextPart("plain") { Text = message };

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync("smtp.gmail.com", 587, false);
            await smtp.AuthenticateAsync("your-email", "your-password"); //  sécuriser
            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
        }
    }
}
