using System.Net;
using System.Net.Mail;
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

        public async Task SendBookingConfirmationAsync(Appointment appt, string tzId = "Europe/Brussels")
        {
            try
            {
                var smtp = _cfg.GetSection("Smtp");
                var host = smtp["Host"];
                var user = smtp["User"];
                var pass = smtp["Pass"];
                var from = string.IsNullOrWhiteSpace(smtp["From"]) ? user : smtp["From"];
                var port = int.TryParse(smtp["Port"], out var p) ? p : 587;
                var enableSsl = bool.TryParse(smtp["EnableSsl"], out var ssl) ? ssl : true;

                if (string.IsNullOrWhiteSpace(host) ||
                    string.IsNullOrWhiteSpace(user) ||
                    string.IsNullOrWhiteSpace(pass) ||
                    string.IsNullOrWhiteSpace(appt?.ClientEmail))
                {
                    _log.LogWarning("SMTP not configured or missing recipient. Skipping email.");
                    return;
                }

                // Gmail requires From == Gmail user unless you've verified an alias in Gmail
                if (host.Contains("gmail", StringComparison.OrdinalIgnoreCase))
                    from = user;

                var tz = TZConvert.GetTimeZoneInfo(tzId);
                var whenLocal = TimeZoneInfo.ConvertTimeFromUtc(appt.StartUtc, tz);

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

                using var client = new SmtpClient(host, port)
                {
                    EnableSsl = enableSsl,
                    Credentials = new NetworkCredential(user, pass)
                };

                using var msg = new MailMessage(from!, appt.ClientEmail, subject, body);
                await client.SendMailAsync(msg);

                _log.LogInformation("Confirmation email sent to {Email}", appt.ClientEmail);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to send confirmation email to {Email}", appt?.ClientEmail);
            }
        }
    }
}
