using Microsoft.EntityFrameworkCore;
using ProHair.NL.Data;
using ProHair.NL.Models;


namespace ProHair.NL.HostedServices
{
public class HoldSweeper : BackgroundService
{
private readonly IServiceScopeFactory _scopeFactory;
private readonly ILogger<HoldSweeper> _logger;


public HoldSweeper(IServiceScopeFactory scopeFactory, ILogger<HoldSweeper> logger)
{ _scopeFactory = scopeFactory; _logger = logger; }


protected override async Task ExecuteAsync(CancellationToken stoppingToken)
{
while (!stoppingToken.IsCancellationRequested)
{
try
{
using var scope = _scopeFactory.CreateScope();
var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();


var expired = await db.Appointments
.Where(a => a.Status == AppointmentStatus.Hold && a.HoldUntilUtc < DateTime.UtcNow)
.ToListAsync(stoppingToken);


if (expired.Count > 0)
{
db.Appointments.RemoveRange(expired);
await db.SaveChangesAsync(stoppingToken);
_logger.LogInformation("Removed {Count} expired holds", expired.Count);
}
}
catch (Exception ex)
{
_logger.LogError(ex, "HoldSweeper error");
}


await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
}
}
}
}
