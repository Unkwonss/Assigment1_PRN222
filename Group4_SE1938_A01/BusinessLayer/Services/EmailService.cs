using System;
using System.IO;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace BusinessLayer.Services
{
    public class EmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string body)
        {
            string smtpHost = _configuration["EmailSettings:SmtpHost"] ?? "";
            string smtpPortStr = _configuration["EmailSettings:SmtpPort"] ?? "587";
            string senderEmail = _configuration["EmailSettings:SenderEmail"] ?? "";
            string senderPassword = _configuration["EmailSettings:SenderPassword"] ?? "";
            string enableSslStr = _configuration["EmailSettings:EnableSsl"] ?? "true";

            // If SMTP configurations are missing, fallback to writing to local file (SentEmails log)
            if (string.IsNullOrEmpty(smtpHost) || string.IsNullOrEmpty(senderEmail))
            {
                await LogEmailToFileAsync(toEmail, subject, body);
                return;
            }

            try
            {
                int.TryParse(smtpPortStr, out int smtpPort);
                bool.TryParse(enableSslStr, out bool enableSsl);

                using (var mail = new MailMessage())
                {
                    mail.From = new MailAddress(senderEmail, "Hệ thống RAG LMS FPT");
                    mail.To.Add(toEmail);
                    mail.Subject = subject;
                    mail.Body = body;
                    mail.IsBodyHtml = true;

                    using (var smtp = new SmtpClient(smtpHost, smtpPort))
                    {
                        smtp.Credentials = new NetworkCredential(senderEmail, senderPassword);
                        smtp.EnableSsl = enableSsl;
                        await smtp.SendMailAsync(mail);
                    }
                }
            }
            catch (Exception ex)
            {
                // Fallback log to file if real email dispatch fails
                Console.WriteLine($"[EMAIL ERROR] Failed to send SMTP mail: {ex.Message}. Logging to file instead.");
                await LogEmailToFileAsync(toEmail, subject, body);
            }
        }

        private async Task LogEmailToFileAsync(string toEmail, string subject, string body)
        {
            try
            {
                // Write email details into a file for debugging/evaluating
                string folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SentEmails");
                if (!Directory.Exists(folderPath))
                {
                    Directory.CreateDirectory(folderPath);
                }

                string fileName = $"Email_{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid().ToString().Substring(0, 8)}.txt";
                string filePath = Path.Combine(folderPath, fileName);

                StringBuilder sb = new StringBuilder();
                sb.AppendLine($"Date: {DateTime.Now}");
                sb.AppendLine($"To: {toEmail}");
                sb.AppendLine($"Subject: {subject}");
                sb.AppendLine("--------------------------------------------------");
                sb.AppendLine(body);
                sb.AppendLine("--------------------------------------------------");

                await File.WriteAllTextAsync(filePath, sb.ToString(), Encoding.UTF8);
                Console.WriteLine($"[EMAIL MOCK] Email saved to: {filePath}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[EMAIL ERROR] Failed to write mock email to file: {ex.Message}");
            }
        }
    }
}
