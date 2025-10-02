using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Options;
using System.ComponentModel.DataAnnotations;
using System.Net;
using System.Net.Mail;

namespace ProHair.NL.Pages
{
    public class ContactModel : PageModel
    {
        private readonly SmtpOptions _smtp;
        private readonly ILogger<ContactModel> _log;

        public ContactModel(IOptions<SmtpOptions> smtp, ILogger<ContactModel> log)
        {
            _smtp = smtp.Value;
            _log = log;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new();

        // Honeypot (should remain empty)
        [BindProperty]
        public string? Website { get; set; }

        public bool Sent { get; set; }

        public class InputModel
        {
            [Required, StringLength(80)]
            public string Name { get; set; } = "";

            [Required, EmailAddress, StringLength(160)]
            public string Email { get; set; } = "";

            [Required, StringLength(120)]
            public string Subject { get; set; } = "Algemene vraag";

            [Required, StringLength(4000, MinimumLength = 5)]
            public string Message { get; set; } = "";
        }

        public void OnGet() {}

        public async Task<IActionResult> OnPostAsync()
        {
            // Simple spam block
            if (!string.IsNullOrWhiteSpace(Website))
            {
                Sent = true; // act successful but drop it
                return Page();
            }

            if (!ModelState.IsValid) return Page();

            try
            {
                // FROM: Gmail kuralı -> e-posta adresi AUTH kullanıcıyla aynı olmalı
                var displayName = ExtractDisplayName(_smtp.From) ?? "Haarmaster";
                var fromAddr = new MailAddress(_smtp.User, displayName);

                // Studio'ya giden mail
                using var toStudio = new MailMessage
                {
                    From = fromAddr,
                    Subject = $"[Contact] {Input.Subject} — {Input.Name}",
                    IsBodyHtml = true,
                    Body =
                        $"<h3>Nieuw bericht via contactformulier</h3>" +
                        $"<p><b>Naam:</b> {WebUtility.HtmlEncode(Input.Name)}<br>" +
                        $"<b>E-mail:</b> {WebUtility.HtmlEncode(Input.Email)}<br>" +
                        $"<b>Onderwerp:</b> {WebUtility.HtmlEncode(Input.Subject)}</p>" +
                        $"<p style='white-space:pre-wrap'>{WebUtility.HtmlEncode(Input.Message)}</p>"
                };
                toStudio.To.Add(_smtp.User); // kendi gelen kutuna
                toStudio.ReplyToList.Add(new MailAddress(Input.Email, Input.Name));

                using var client = new SmtpClient(_smtp.Host, _smtp.Port)
                {
                    EnableSsl = _smtp.EnableSsl,
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(_smtp.User, _smtp.Pass),
                    Timeout = 1000 * 30
                };

                await client.SendMailAsync(toStudio);

                // Otomatik "mesaj alındı" yanıtı müşteriye
                using var toClient = new MailMessage
                {
                    From = fromAddr,
                    Subject = "We hebben je bericht ontvangen – Haarmaster",
                    IsBodyHtml = true,
                    Body =
                        $"<p>Hi {WebUtility.HtmlEncode(Input.Name)},</p>" +
                        "<p>Bedankt voor je bericht. We antwoorden meestal binnen 1 werkdag.</p>" +
                        "<p>Groeten,<br>Haarmaster</p>"
                };
                toClient.To.Add(new MailAddress(Input.Email, Input.Name));
                await client.SendMailAsync(toClient);

                Sent = true;
                ModelState.Clear();
                Input = new InputModel(); // formu temizle
                return Page();
            }
            catch (SmtpException ex)
            {
                _log.LogError(ex, "Contact mail SMTP error (Host={Host}, User={User})", _smtp.Host, _smtp.User);
                TempData["ContactErr"] = "Bericht kon niet worden verzonden. Probeer later opnieuw.";
                return Page(); // 500 yerine aynı sayfayı nazik mesajla göster
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Contact mail general error");
                TempData["ContactErr"] = "Er ging iets mis. Probeer later opnieuw.";
                return Page();
            }
        }

        // "Haarstudio <foo@bar>" içinden sadece görünen adı al
        private static string? ExtractDisplayName(string? fromSetting)
        {
            if (string.IsNullOrWhiteSpace(fromSetting)) return null;
            try
            {
                if (fromSetting.Contains('<') && fromSetting.Contains('>'))
                {
                    var name = fromSetting[..fromSetting.IndexOf('<')].Trim();
                    return string.IsNullOrWhiteSpace(name) ? null : name;
                }
                // sade e-posta verilmişse görünen ad yoktur
                return null;
            }
            catch { return null; }
        }
    }
}
