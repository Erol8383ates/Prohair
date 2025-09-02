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

        public ContactModel(IOptions<SmtpOptions> smtp)
        {
            _smtp = smtp.Value;
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

            if (!ModelState.IsValid)
            {
                return Page();
            }

            // Compose email to studio
            var toStudio = new MailMessage();
            var fromAddr = ParseFromAddress(_smtp.From, _smtp.User);

            toStudio.From = fromAddr;
            toStudio.To.Add(_smtp.User); // send to your inbox
            toStudio.ReplyToList.Add(new MailAddress(Input.Email, Input.Name));
            toStudio.Subject = $"[Contact] {Input.Subject} — {Input.Name}";
            toStudio.IsBodyHtml = true;
            toStudio.Body =
                $"<h3>Nieuw bericht via contactformulier</h3>" +
                $"<p><b>Naam:</b> {WebUtility.HtmlEncode(Input.Name)}<br>" +
                $"<b>E-mail:</b> {WebUtility.HtmlEncode(Input.Email)}<br>" +
                $"<b>Onderwerp:</b> {WebUtility.HtmlEncode(Input.Subject)}</p>" +
                $"<p style='white-space:pre-wrap'>{WebUtility.HtmlEncode(Input.Message)}</p>";

            using var client = new SmtpClient(_smtp.Host, _smtp.Port)
            {
                EnableSsl = _smtp.EnableSsl,
                Credentials = new NetworkCredential(_smtp.User, _smtp.Pass)
            };
            await client.SendMailAsync(toStudio);

            // Optional: auto-reply to the client
            var toClient = new MailMessage
            {
                From = fromAddr,
                Subject = "We hebben je bericht ontvangen – ProHair Studio",
                IsBodyHtml = true,
                Body = $"<p>Hi {WebUtility.HtmlEncode(Input.Name)},</p>" +
                       "<p>Bedankt voor je bericht. We antwoorden meestal binnen 1 werkdag.</p>" +
                       "<p>Groeten,<br>ProHair Studio</p>"
            };
            toClient.To.Add(new MailAddress(Input.Email, Input.Name));
            await client.SendMailAsync(toClient);

            Sent = true;
            ModelState.Clear();
            Input = new InputModel(); // clear the form
            return Page();
        }

        private static MailAddress ParseFromAddress(string fromSetting, string fallbackUser)
        {
            // supports "Display Name <email@example.com>" or plain email
            try
            {
                if (fromSetting?.Contains("<") == true && fromSetting.Contains(">"))
                {
                    var start = fromSetting.IndexOf('<') + 1;
                    var end = fromSetting.IndexOf('>');
                    var email = fromSetting.Substring(start, end - start).Trim();
                    var name = fromSetting.Substring(0, start - 1).Trim();
                    return new MailAddress(email, name);
                }
                if (!string.IsNullOrWhiteSpace(fromSetting)) return new MailAddress(fromSetting);
            }
            catch { /* fallback */ }
            return new MailAddress(fallbackUser, "ProHair Studio");
        }
    }
}
