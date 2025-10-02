using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using ProHair.NL.Data;
using ProHair.NL.Hubs;
using ProHair.NL.HostedServices;
using ProHair.NL.Services;
using System;
using System.Threading.Tasks;

var builder = WebApplication.CreateBuilder(args);

// Npgsql timestamp compatibility
AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

// Bind to PORT on PaaS (Render/Heroku-style)
var portEnv = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrEmpty(portEnv) && int.TryParse(portEnv, out var port))
{
    builder.WebHost.ConfigureKestrel(o => o.ListenAnyIP(port));
}

builder.Services.AddControllersWithViews();
builder.Services.AddRazorPages();

// PostgreSQL connection
var pgConn =
    builder.Configuration.GetConnectionString("DefaultConnection")
    ?? Environment.GetEnvironmentVariable("DATABASE_URL");

builder.Services.AddDbContext<AppDbContext>(opt =>
{
    opt.UseNpgsql(pgConn, o => o.CommandTimeout(15));
    // Ignore EF PendingModelChangesWarning at runtime
    opt.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
});

// ✅ Persist DataProtection keys in DB (antiforgery/session)
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>();

// Memory cache
builder.Services.AddMemoryCache();

// App services
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddHostedService<HoldSweeper>();

// ✅ Needed for SendGrid (HTTP API) used by EmailService
builder.Services.AddHttpClient();

// ✅ SignalR
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = false;
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);
});

// Availability rules service
builder.Services.AddScoped<IAvailabilityService, AvailabilityService>();

// SMTP options (still fine to bind; EmailService reads IConfiguration directly)
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));

// Auth
builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/admin/login";
        options.AccessDeniedPath = "/admin/login";
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = ctx => { ctx.Response.Redirect(ctx.RedirectUri); return Task.CompletedTask; }
        };
    });

builder.Services.AddAuthorization(o => o.AddPolicy("AdminOnly", p => p.RequireRole("Admin")));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// ✅ Log which mail provider is active (check Render logs on startup)
{
    var cfg = app.Configuration;
    var hasSendGrid = !string.IsNullOrWhiteSpace(cfg["SendGrid:ApiKey"]);
    var hasSmtp = !string.IsNullOrWhiteSpace(cfg["Smtp:Host"]) && !string.IsNullOrWhiteSpace(cfg["Smtp:User"]);
    var provider = hasSendGrid ? "SendGrid" : (hasSmtp ? "SMTP" : "None");
    app.Logger.LogInformation("Mail provider selected: {Provider}", provider);
    if (hasSendGrid)
    {
        app.Logger.LogInformation("SendGrid FromEmail={From} Name={Name}",
            cfg["SendGrid:FromEmail"], cfg["SendGrid:FromName"] ?? "ProHair Studio");
    }
    else if (hasSmtp)
    {
        app.Logger.LogInformation("SMTP Host={Host} Port={Port} SSL={SSL} User={User} From={From}",
            cfg["Smtp:Host"], cfg["Smtp:Port"], cfg["Smtp:EnableSsl"], cfg["Smtp:User"], cfg["Smtp:FromAddress"] ?? cfg["Smtp:User"]);
    }
    else
    {
        app.Logger.LogWarning("No email configuration found. Booking/Contact emails will NOT be sent.");
    }
}

// ✅ Respect proxy headers
var fwd = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor
                     | ForwardedHeaders.XForwardedProto
                     | ForwardedHeaders.XForwardedHost,
    RequireHeaderSymmetry = false
};
fwd.KnownNetworks.Clear();
fwd.KnownProxies.Clear();
app.UseForwardedHeaders(fwd);

app.UseHttpsRedirection();

// ✅ Canonical redirect (Prod only)
if (app.Environment.IsProduction())
{
    app.Use(async (ctx, next) =>
    {
        var host = ctx.Request.Host.Host;
        if (string.Equals(host, "haarmaster.be", StringComparison.OrdinalIgnoreCase))
        {
            var newUrl = $"https://www.haarmaster.be{ctx.Request.PathBase}{ctx.Request.Path}{ctx.Request.QueryString}";
            ctx.Response.Redirect(newUrl, permanent: true);
            return;
        }
        await next();
    });
}

app.UseStaticFiles();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();
app.MapHub<BookingHub>("/hubs/booking");

// Health endpoint
app.MapGet("/healthz", async (AppDbContext db) =>
{
    try { await db.Database.ExecuteSqlRawAsync("select 1"); return Results.Ok("ok"); }
    catch (Exception ex) { return Results.Problem(ex.Message, statusCode: 500); }
});

// Auto-migrate + seed (+ safety net)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();

    await db.Database.ExecuteSqlRawAsync(@"
        CREATE TABLE IF NOT EXISTS ""DataProtectionKeys"" (
            ""Id"" SERIAL PRIMARY KEY,
            ""FriendlyName"" text NULL,
            ""Xml"" text NULL
        );");

    await db.Database.ExecuteSqlRawAsync(@"
        UPDATE ""WeeklyOpenHours"" SET ""IsClosed"" = TRUE  WHERE ""Day"" = 1;  -- Monday
        UPDATE ""WeeklyOpenHours"" SET ""IsClosed"" = FALSE WHERE ""Day"" = 0;  -- Sunday
    ");

    await SeedData.EnsureSeededAsync(db);
}

app.Run();
