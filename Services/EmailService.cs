using System;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using ProHair.NL.Models;
using TimeZoneConverter;

namespace ProHair.NL.Services
{
    public class EmailService
    {
        private readonly IConfiguration _cfg;
        private readonly ILogger<EmailService> _log;

        public EmailService(IConfiguration cfg, ILogger<EmailService> log)
        {
            _cfg = cfg;
            _log = log;
        }

        private (SmtpClient client, string from) BuildClient()
        {
            var smtp = _cfg.GetSection("Smtp");
            var host = smtp["Host"];
            var user = smtp["User"];
            var pass = smtp["Pass"];
            var from = smtp["FromAddress"];
            var port = int.TryParse(smtp["Port"], out var p) ? p : 587;
            var enableSsl = bool.TryParse(smtp["EnableSsl"], out var ssl) ? ssl : true;

            if (string.IsNullOrWhiteSpace(host) ||
                string.IsNullOrWhiteSpace(user) ||
                string.IsNullOrWhiteSpace(pass))
            {
                throw new InvalidOperationException("SMTP not configured correctly (Host/User/Pass).");
            }

            // Gmail: From should match the authenticated account (unless alias is verified).
            if (string.IsNullOrWhiteSpace(from) ||
                host.Contains("gmail", StringComparison.OrdinalIgnoreCase))
            {
                from = user;
            }

            var client = new SmtpClient(host, port)
            {
                EnableSsl = enableSsl,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential(user, pass),
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            return (client, from);
        }

        public async Task SendBookingConfirmationAsync(Appointment appt, string tzId = "Europe/Brussels")
        {
            if (appt == null || string.IsNullOrWhiteSpace(appt.ClientEmail))
            {
                _log.LogWarning("No client email provided, skipping confirmation email.");
                return;
            }

            try
            {
                var (client, from) = BuildClient();

                var tz = TZConvert.GetTimeZoneInfo(tzId);

                // appt.StartUtc stored as DateTime (UTC). Ensure Kind=Utc for safe conversion.
                var utc = appt.StartUtc.Kind == DateTimeKind.Utc
                    ? appt.StartUtc
                    : DateTime.SpecifyKind(appt.StartUtc, DateTimeKind.Utc);

                var whenLocal = TimeZoneInfo.ConvertTimeFromUtc(utc, tz);

                var subject = "Bevestiging afspraak – ProHair Studio";
                var body =
$@"Beste {appt.ClientName},

Je afspraak is bevestigd.

Datum: {whenLocal:dddd dd-MM-yyyy}
Tijd:  {whenLocal:HH:mm}
Behandeling: {appt.Service?.Name}

Locatie:
ProHair Studio
(Adres hier)

Mocht je verhinderd zijn, laat het ons tijdig weten door te antwoorden op deze mail.

Tot snel!
ProHair Studio";

                using var msg = new MailMessage(from, appt.ClientEmail, subject, body)
                {
                    From = new MailAddress(from, "ProHair Studio"),
                    IsBodyHtml = false
                };

                await client.SendMailAsync(msg);
                client.Dispose();

                _log.LogInformation("Confirmation email sent to {Email}", appt.ClientEmail);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to send confirmation email to {Email}", appt?.ClientEmail);
                throw;
            }
        }

        public async Task SendContactAsync(string toBusinessInbox, string name, string email, string subject, string message)
        {
            if (string.IsNullOrWhiteSpace(toBusinessInbox))
                throw new ArgumentException("Business inbox address is required.", nameof(toBusinessInbox));

            try
            {
                var (client, from) = BuildClient();

                var html =
$@"<p><strong>Naam:</strong> {WebUtility.HtmlEncode(name)}</p>
<p><strong>Email:</strong> {WebUtility.HtmlEncode(email)}</p>
<p><strong>Onderwerp:</strong> {WebUtility.HtmlEncode(subject)}</p>
<p><strong>Bericht:</strong><br/>{WebUtility.HtmlEncode(message).Replace("\n", "<br/>")}</p>";

                using var msg = new MailMessage
                {
                    From = new MailAddress(from, "ProHair Website"),
                    Subject = $"Contactformulier: {subject}",
                    Body = html,
                    IsBodyHtml = true
                };
                msg.To.Add(toBusinessInbox);

                // Reply-To so you can reply straight to the sender
                if (!string.IsNullOrWhiteSpace(email))
                    msg.ReplyToList.Add(new MailAddress(email, string.IsNullOrWhiteSpace(name) ? email : name));

                await client.SendMailAsync(msg);
                client.Dispose();

                _log.LogInformation("Contact email delivered to business inbox {To}", toBusinessInbox);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Contact mail SMTP error");
                throw;
            }
        }
    }
}
