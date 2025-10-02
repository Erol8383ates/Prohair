using System;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
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
        private readonly IHttpClientFactory _http;

        // timeouts to avoid UI hangs
        private const int SmtpTimeoutMs = 15000; // 15s
        private static readonly TimeSpan HttpTimeout = TimeSpan.FromSeconds(15);

        public EmailService(IConfiguration cfg, ILogger<EmailService> log, IHttpClientFactory http)
        {
            _cfg = cfg;
            _log = log;
            _http = http;
        }

        // ---------- SENDGRID (HTTP API) ----------
        private bool TryGetSendGrid(out string apiKey, out string fromEmail, out string fromName)
        {
            apiKey   = _cfg["SendGrid:ApiKey"] ?? "";
            fromEmail= _cfg["SendGrid:FromEmail"] ?? _cfg["Smtp:FromAddress"] ?? _cfg["Smtp:User"] ?? "";
            fromName = _cfg["SendGrid:FromName"] ?? "ProHair Studio";
            return !string.IsNullOrWhiteSpace(apiKey) && !string.IsNullOrWhiteSpace(fromEmail);
        }

        private async Task<bool> SendViaSendGridAsync(string toEmail, string subject, string plainText, string html)
        {
            if (!TryGetSendGrid(out var apiKey, out var fromEmail, out var fromName))
                return false;

            var payload = new
            {
                personalizations = new[] {
                    new { to = new[] { new { email = toEmail } } }
                },
                from = new { email = fromEmail, name = fromName },
                subject,
                content = new[] {
                    new { type = "text/plain", value = plainText ?? "" },
                    new { type = "text/html",  value = html ?? plainText ?? "" }
                }
            };

            var json = JsonSerializer.Serialize(payload);
            var req = new HttpRequestMessage(HttpMethod.Post, "https://api.sendgrid.com/v3/mail/send");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", apiKey);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var client = _http.CreateClient();
            client.Timeout = HttpTimeout;

            var res = await client.SendAsync(req);
            if ((int)res.StatusCode is >= 200 and < 300)
            {
                _log.LogInformation("SendGrid mail sent to {To}", toEmail);
                return true;
            }

            var body = await res.Content.ReadAsStringAsync();
            _log.LogError("SendGrid failed ({Status}): {Body}", (int)res.StatusCode, body);
            return false;
        }

        // ---------- SMTP (fallback; for local dev) ----------
        private (SmtpClient client, string from) BuildSmtpClient()
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
                throw new InvalidOperationException("SMTP not configured (Host/User/Pass).");
            }

            // Gmail 'From' must be the authenticated account unless alias is verified
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
                DeliveryMethod = SmtpDeliveryMethod.Network,
                Timeout = SmtpTimeoutMs
            };

            _log.LogInformation("SMTP cfg: host={Host}, port={Port}, ssl={Ssl}, user={User}, from={From}",
                host, port, enableSsl, user, from);

            return (client, from);
        }

        // ---------- PUBLIC METHODS ----------
        public async Task SendBookingConfirmationAsync(Appointment appt, string tzId = "Europe/Brussels")
        {
            if (appt == null || string.IsNullOrWhiteSpace(appt.ClientEmail))
            {
                _log.LogWarning("No client email provided, skipping confirmation email.");
                return;
            }

            var tz = TZConvert.GetTimeZoneInfo(tzId);
            var utc = appt.StartUtc.Kind == DateTimeKind.Utc
                ? appt.StartUtc
                : DateTime.SpecifyKind(appt.StartUtc, DateTimeKind.Utc);
            var whenLocal = TimeZoneInfo.ConvertTimeFromUtc(utc, tz);

            var subject = "Bevestiging afspraak – ProHair Studio";
            var plain =
$@"Beste {appt.ClientName},

Je afspraak is bevestigd.

Datum: {whenLocal:dddd dd-MM-yyyy}
Tijd:  {whenLocal:HH:mm}
Behandeling: {appt.Service?.Name}

Locatie: ProHair Studio

Tot snel!
ProHair Studio";

            var html = $"<p>Beste {WebUtility.HtmlEncode(appt.ClientName)},</p>" +
                       $"<p>Je afspraak is bevestigd.</p>" +
                       $"<p><b>Datum:</b> {whenLocal:dddd dd-MM-yyyy}<br/><b>Tijd:</b> {whenLocal:HH:mm}<br/><b>Behandeling:</b> {WebUtility.HtmlEncode(appt.Service?.Name)}</p>" +
                       "<p>Locatie: ProHair Studio</p><p>Tot snel!<br/>ProHair Studio</p>";

            // Prefer SendGrid on Render
            if (await SendViaSendGridAsync(appt.ClientEmail, subject, plain, html))
                return;

            // Fallback to SMTP (e.g., local dev)
            try
            {
                var (client, from) = BuildSmtpClient();
                using var msg = new MailMessage(from, appt.ClientEmail, subject, plain)
                {
                    From = new MailAddress(from, "ProHair Studio"),
                    IsBodyHtml = false
                };
                await client.SendMailAsync(msg);
                client.Dispose();
                _log.LogInformation("SMTP confirmation email sent to {Email}", appt.ClientEmail);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Failed to send confirmation email to {Email}", appt.ClientEmail);
            }
        }

        public async Task SendContactAsync(string toBusinessInbox, string name, string email, string subject, string message)
        {
            if (string.IsNullOrWhiteSpace(toBusinessInbox))
                throw new ArgumentException("Business inbox address is required.", nameof(toBusinessInbox));

            var html =
$@"<p><strong>Naam:</strong> {WebUtility.HtmlEncode(name)}</p>
<p><strong>Email:</strong> {WebUtility.HtmlEncode(email)}</p>
<p><strong>Onderwerp:</strong> {WebUtility.HtmlEncode(subject)}</p>
<p><strong>Bericht:</strong><br/>{WebUtility.HtmlEncode(message).Replace("\n", "<br/>")}</p>";

            var plain =
$@"Naam: {name}
Email: {email}
Onderwerp: {subject}

Bericht:
{message}";

            if (await SendViaSendGridAsync(toBusinessInbox, $"Contactformulier: {subject}", plain, html))
                return;

            try
            {
                var (client, from) = BuildSmtpClient();
                using var msg = new MailMessage
                {
                    From = new MailAddress(from, "ProHair Website"),
                    Subject = $"Contactformulier: {subject}",
                    Body = html,
                    IsBodyHtml = true
                };
                msg.To.Add(toBusinessInbox);
                if (!string.IsNullOrWhiteSpace(email))
                    msg.ReplyToList.Add(new MailAddress(email, string.IsNullOrWhiteSpace(name) ? email : name));

                await client.SendMailAsync(msg);
                client.Dispose();
                _log.LogInformation("SMTP contact email sent to {To}", toBusinessInbox);
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Contact mail send failed to {To}", toBusinessInbox);
            }
        }
    }
}
