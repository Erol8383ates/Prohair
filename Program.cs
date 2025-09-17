using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics; // <-- önemli
using ProHair.NL.Data;
using ProHair.NL.Hubs;
using ProHair.NL.HostedServices;
using ProHair.NL.Services;
using System;
using System.Threading.Tasks; // <-- Task.CompletedTask için

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
    // EF'nin "PendingModelChangesWarning" uyarısını runtime'da ignore et
    opt.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));
});

// ✅ DataProtection keylerini DB'de sakla (antiforgery fix)
builder.Services.AddDataProtection()
    .PersistKeysToDbContext<AppDbContext>();

// Memory cache
builder.Services.AddMemoryCache();

// App services
builder.Services.AddScoped<BookingService>();
builder.Services.AddScoped<EmailService>();
builder.Services.AddHostedService<HoldSweeper>();

// ✅ SignalR (kopma azaltma)
builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = false;
    options.KeepAliveInterval = TimeSpan.FromSeconds(15);     // sunucu pingi
    options.ClientTimeoutInterval = TimeSpan.FromSeconds(60);  // client’tan ping bekleme süresi
});

// Availability rules service
builder.Services.AddScoped<IAvailabilityService, AvailabilityService>();

// SMTP options
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

// ✅ Proxy başlıklarını doğru oku (host dahil)
var fwd = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor
                     | ForwardedHeaders.XForwardedProto
                     | ForwardedHeaders.XForwardedHost,
    RequireHeaderSymmetry = false
};
// Proxy listesi bilinmiyorsa hepsine güven (PaaS/LB arkasında yaygın)
fwd.KnownNetworks.Clear();
fwd.KnownProxies.Clear();
app.UseForwardedHeaders(fwd);

app.UseHttpsRedirection();

// ✅ Canonical redirect: haarmaster.be -> www.haarmaster.be (yalnızca Prod)
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

// Auto-migrate + seed (+ güvenlik ağı)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // Var olan EF migration’ları uygula
    await db.Database.MigrateAsync();

    // ✅ DataProtectionKeys tablosu yoksa oluştur (Postgres)
    await db.Database.ExecuteSqlRawAsync(@"
        CREATE TABLE IF NOT EXISTS ""DataProtectionKeys"" (
            ""Id"" SERIAL PRIMARY KEY,
            ""FriendlyName"" text NULL,
            ""Xml"" text NULL
        );");

    // ✅ Haftalık saatler: Pazartesi kapalı, Pazar açık (idempotent güncelleme)
    await db.Database.ExecuteSqlRawAsync(@"
        UPDATE ""WeeklyOpenHours"" SET ""IsClosed"" = TRUE  WHERE ""Day"" = 1;  -- Monday
        UPDATE ""WeeklyOpenHours"" SET ""IsClosed"" = FALSE WHERE ""Day"" = 0;  -- Sunday
    ");

    await SeedData.EnsureSeededAsync(db);
}

app.Run();
