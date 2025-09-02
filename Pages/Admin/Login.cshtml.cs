using System;
using System.Collections.Generic;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;

namespace ProHair.NL.Pages.Admin
{
    public class LoginModel : PageModel
    {
        private readonly IConfiguration _config;

        public LoginModel(IConfiguration config) => _config = config;

        [BindProperty] public string Username { get; set; } = string.Empty;
        [BindProperty] public string Password { get; set; } = string.Empty;
        [BindProperty] public bool RememberMe { get; set; }
        [BindProperty(SupportsGet = true)] public string? ReturnUrl { get; set; }

        public void OnGet() { }

        public async Task<IActionResult> OnPost()
        {
            var adminUser = _config["Admin:Username"] ?? "admin";
            var adminPass = _config["Admin:Password"] ?? "changeme";

            if (string.Equals(Username, adminUser, StringComparison.OrdinalIgnoreCase) &&
                Password == adminPass)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, Username),
                    new Claim(ClaimTypes.Role, "Admin")
                };

                var identity = new ClaimsIdentity(
                    claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    principal,
                    new AuthenticationProperties { IsPersistent = RememberMe });

                if (!string.IsNullOrWhiteSpace(ReturnUrl) && Url.IsLocalUrl(ReturnUrl))
                    return Redirect(ReturnUrl);

                return Redirect("/admin/afspraken");
            }

            ViewData["Error"] = "Ongeldige gebruikersnaam of wachtwoord.";
            return Page();
        }
    }
}
