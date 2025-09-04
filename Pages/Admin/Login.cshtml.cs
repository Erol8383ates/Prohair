using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;

namespace ProHair.NL.Pages.Admin
{
    [Microsoft.AspNetCore.Authorization.AllowAnonymous]
    public class LoginModel : PageModel
    {
        private readonly IConfiguration _config;
        public LoginModel(IConfiguration config) => _config = config;

        [BindProperty, Required] public string Username { get; set; } = "";
        [BindProperty, Required] public string Password { get; set; } = "";
        [BindProperty] public bool RememberMe { get; set; }
        [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

        public void OnGet() { }

        public async Task<IActionResult> OnPost()
        {
            if (!ModelState.IsValid) return Page();

            // Get admin credentials from config or environment
            var adminUser = _config["Admin:Username"] ?? Environment.GetEnvironmentVariable("ADMIN_USER");
            var adminPass = _config["Admin:Password"] ?? Environment.GetEnvironmentVariable("ADMIN_PASS");

            if (string.IsNullOrWhiteSpace(adminUser) || string.IsNullOrWhiteSpace(adminPass))
            {
                ViewData["Error"] = "Admin inlog is niet geconfigureerd (Admin:Username/Password).";
                return Page();
            }

            if (!FixedEquals(Username, adminUser) || !FixedEquals(Password, adminPass))
            {
                ViewData["Error"] = "Onjuiste gebruikersnaam of wachtwoord.";
                return Page();
            }

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, adminUser),
                new Claim(ClaimTypes.Name, adminUser),
                new Claim(ClaimTypes.Role, "Admin")
            };

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var principal = new ClaimsPrincipal(identity);

            var props = new AuthenticationProperties
            {
                IsPersistent = RememberMe,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            };

            await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal, props);

            var dest = string.IsNullOrWhiteSpace(ReturnUrl) ? "/admin/afspraken" : ReturnUrl!;
            return LocalRedirect(dest);
        }

        private static bool FixedEquals(string a, string b)
        {
            var ba = Encoding.UTF8.GetBytes(a ?? "");
            var bb = Encoding.UTF8.GetBytes(b ?? "");
            // pad arrays to same length to avoid early returns
            var max = Math.Max(ba.Length, bb.Length);
            Array.Resize(ref ba, max);
            Array.Resize(ref bb, max);
            return CryptographicOperations.FixedTimeEquals(ba, bb);
        }
    }
}
