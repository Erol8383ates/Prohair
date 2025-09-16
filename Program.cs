using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ProHair.NL.Data;
using Microsoft.EntityFrameworkCore.Diagnostics; 
using ProHair.NL.Hubs;
using ProHair.NL.HostedServices;
using ProHair.NL.Services;
using System;

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
builder.Services.AddSignalR();

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

app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});

app.UseHttpsRedirection();
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

// Auto-migrate + seed
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
    await SeedData.EnsureSeededAsync(db);
}

app.Run();
