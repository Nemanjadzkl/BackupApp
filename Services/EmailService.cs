using System;
using System.Collections.Generic;
using System.Net.Mail;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Linq;
using BackupApp.Models;

namespace BackupApp.Services
{
    public class EmailService
    {
        private readonly EmailSettings _settings;
        public event Action<string, bool>? OnLog;

        public EmailService(EmailSettings settings)
        {
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
        }

        public async Task<bool> SendEmailAsync(string subject, string body, bool isHtml = true)
        {
            if (!_settings.IsValid())
            {
                OnLog?.Invoke("Email podešavanja nisu kompletna", true);
                return false;
            }

            try
            {
                using var client = new SmtpClient(_settings.SmtpServer, _settings.SmtpPort)
                {
                    EnableSsl = _settings.EnableSsl,
                    Credentials = new System.Net.NetworkCredential(_settings.Username, _settings.Password),
                    Timeout = 30000 // 30 sekundi timeout
                };

                using var message = new MailMessage(_settings.FromEmail, _settings.ToEmail, subject, body)
                {
                    IsBodyHtml = isHtml
                };

                OnLog?.Invoke($"Slanje email-a na {_settings.ToEmail}...", false);
                await client.SendMailAsync(message);
                OnLog?.Invoke("Email uspešno poslat!", false);
                return true;
            }
            catch (Exception ex)
            {
                OnLog?.Invoke($"Greška pri slanju: {ex.Message}", true);
                return false;
            }
        }
    }
}
