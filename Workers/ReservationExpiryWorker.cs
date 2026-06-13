using Microsoft.EntityFrameworkCore;
using CinePlex.Data;
using CinePlex.Models;

namespace CinePlex.Workers
{
    public class ReservationExpiryWorker : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ReservationExpiryWorker> _logger;
        private static readonly TimeSpan Interval = TimeSpan.FromMinutes(5);

        public ReservationExpiryWorker(IServiceScopeFactory scopeFactory, ILogger<ReservationExpiryWorker> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ExpireReservationsAsync(stoppingToken);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "Error expiring pending reservations.");
                }
                await Task.Delay(Interval, stoppingToken);
            }
        }

        private async Task ExpireReservationsAsync(CancellationToken ct)
        {
            using var scope = _scopeFactory.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var now = DateTime.Now;

            var expired = await context.Reservations
                .Where(r => r.Status == ReservationStatus.Pending && r.ExpiresAt < now)
                .ToListAsync(ct);

            if (expired.Count == 0) return;

            foreach (var r in expired)
                r.Status = ReservationStatus.Cancelled;

            await context.SaveChangesAsync(ct);
            _logger.LogInformation("Expired {Count} pending reservations.", expired.Count);
        }
    }
}
