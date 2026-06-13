using System.Reflection;
using CinePlex.Data;
using CinePlex.Models;
using CinePlex.Tests.Helpers;
using CinePlex.Workers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace CinePlex.Tests.Unit;

public class ReservationExpiryWorkerTests
{

    private static IServiceScopeFactory BuildScopeFactory(ApplicationDbContext ctx)
    {
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider
            .Setup(x => x.GetService(typeof(ApplicationDbContext)))
            .Returns(ctx);

        var scope = new Mock<IServiceScope>();
        scope.Setup(x => x.ServiceProvider).Returns(serviceProvider.Object);

        var scopeFactory = new Mock<IServiceScopeFactory>();
        scopeFactory.Setup(x => x.CreateScope()).Returns(scope.Object);

        return scopeFactory.Object;
    }

    private static ReservationExpiryWorker CreateWorker(ApplicationDbContext ctx)
    {
        var logger = new Mock<ILogger<ReservationExpiryWorker>>().Object;
        return new ReservationExpiryWorker(BuildScopeFactory(ctx), logger);
    }

    private static async Task ExpireAsync(ReservationExpiryWorker worker)
    {
        var method = typeof(ReservationExpiryWorker)
            .GetMethod("ExpireReservationsAsync", BindingFlags.NonPublic | BindingFlags.Instance)!;

        var task = (Task)method.Invoke(worker, new object[] { CancellationToken.None })!;
        await task;
    }

    private static async Task<(int screeningId, int hallId)> SeedMinimalScreeningAsync(
        ApplicationDbContext ctx)
    {
        var director = new Director { FirstName = "Test", LastName = "Director" };
        ctx.Directors.Add(director);
        await ctx.SaveChangesAsync();

        var movie = new Movie
        {
            Title = "Test Movie",
            Description = "Test",
            Duration = 90,
            ReleaseDate = DateTime.Today,
            DirectorId = director.DirectorId,
        };
        ctx.Movies.Add(movie);
        await ctx.SaveChangesAsync();

        var cinema = new Cinema { Name = "Test Cinema", City = "City", Address = "Addr" };
        ctx.Cinemas.Add(cinema);
        await ctx.SaveChangesAsync();

        var hall = new Hall { Number = "1", Capacity = 50, CinemaId = cinema.CinemaId };
        ctx.Halls.Add(hall);
        await ctx.SaveChangesAsync();

        var screening = new Screening
        {
            StartTime = DateTime.Now.AddHours(2),
            HallId = hall.HallId,
            MovieId = movie.MovieId,
        };
        ctx.Screenings.Add(screening);
        await ctx.SaveChangesAsync();

        return (screening.ScreeningId, hall.HallId);
    }

    private static Reservation MakePendingReservation(int screeningId, DateTime expiresAt) =>
        new Reservation
        {
            SeatNumber = Random.Shared.Next(1, 200),
            Status = ReservationStatus.Pending,
            ExpiresAt = expiresAt,
            ScreeningId = screeningId,
            PricePaid = 25m,
        };

    [Fact]
    public async Task NoExpiredReservations_FutureExpiresAt_StaysPending()
    {
        using var ctx = TestDbContextFactory.Create();
        var (screeningId, _) = await SeedMinimalScreeningAsync(ctx);

        var reservation = MakePendingReservation(screeningId, DateTime.Now.AddMinutes(30));
        ctx.Reservations.Add(reservation);
        await ctx.SaveChangesAsync();

        var worker = CreateWorker(ctx);
        await ExpireAsync(worker);

        var updated = await ctx.Reservations.FindAsync(reservation.ReservationId);
        Assert.Equal(ReservationStatus.Pending, updated!.Status);
    }

    [Fact]
    public async Task ExpiredPendingReservation_BecomesCancelled()
    {
        using var ctx = TestDbContextFactory.Create();
        var (screeningId, _) = await SeedMinimalScreeningAsync(ctx);

        var reservation = MakePendingReservation(screeningId, DateTime.Now.AddMinutes(-5));
        ctx.Reservations.Add(reservation);
        await ctx.SaveChangesAsync();

        var worker = CreateWorker(ctx);
        await ExpireAsync(worker);

        var updated = await ctx.Reservations.FindAsync(reservation.ReservationId);
        Assert.Equal(ReservationStatus.Cancelled, updated!.Status);
    }

    [Fact]
    public async Task ConfirmedReservation_PastExpiresAt_StaysConfirmed()
    {
        using var ctx = TestDbContextFactory.Create();
        var (screeningId, _) = await SeedMinimalScreeningAsync(ctx);

        var reservation = new Reservation
        {
            SeatNumber = 5,
            Status = ReservationStatus.Confirmed,
            ExpiresAt = DateTime.Now.AddMinutes(-10),
            ScreeningId = screeningId,
            PricePaid = 25m,
        };
        ctx.Reservations.Add(reservation);
        await ctx.SaveChangesAsync();

        var worker = CreateWorker(ctx);
        await ExpireAsync(worker);

        var updated = await ctx.Reservations.FindAsync(reservation.ReservationId);
        Assert.Equal(ReservationStatus.Confirmed, updated!.Status);
    }

    [Fact]
    public async Task MixedReservations_OnlyExpiredPendingBecomeCancelled()
    {
        using var ctx = TestDbContextFactory.Create();
        var (screeningId, _) = await SeedMinimalScreeningAsync(ctx);

        var expiredPending1 = MakePendingReservation(screeningId, DateTime.Now.AddMinutes(-20));
        var expiredPending2 = MakePendingReservation(screeningId, DateTime.Now.AddMinutes(-1));
        var validPending = MakePendingReservation(screeningId, DateTime.Now.AddMinutes(30));
        var confirmedReservation = new Reservation
        {
            SeatNumber = 10,
            Status = ReservationStatus.Confirmed,
            ExpiresAt = DateTime.Now.AddMinutes(-5),
            ScreeningId = screeningId,
            PricePaid = 25m,
        };

        ctx.Reservations.AddRange(expiredPending1, expiredPending2, validPending, confirmedReservation);
        await ctx.SaveChangesAsync();

        var worker = CreateWorker(ctx);
        await ExpireAsync(worker);

        var all = await ctx.Reservations.ToListAsync();

        Assert.Equal(ReservationStatus.Cancelled,
            all.Single(r => r.ReservationId == expiredPending1.ReservationId).Status);
        Assert.Equal(ReservationStatus.Cancelled,
            all.Single(r => r.ReservationId == expiredPending2.ReservationId).Status);
        Assert.Equal(ReservationStatus.Pending,
            all.Single(r => r.ReservationId == validPending.ReservationId).Status);
        Assert.Equal(ReservationStatus.Confirmed,
            all.Single(r => r.ReservationId == confirmedReservation.ReservationId).Status);
    }

    [Fact]
    public async Task AlreadyCancelledReservation_PastExpiresAt_StaysCancelled()
    {
        using var ctx = TestDbContextFactory.Create();
        var (screeningId, _) = await SeedMinimalScreeningAsync(ctx);

        var reservation = new Reservation
        {
            SeatNumber = 7,
            Status = ReservationStatus.Cancelled,
            ExpiresAt = DateTime.Now.AddMinutes(-15),
            ScreeningId = screeningId,
            PricePaid = 25m,
        };
        ctx.Reservations.Add(reservation);
        await ctx.SaveChangesAsync();

        var worker = CreateWorker(ctx);
        await ExpireAsync(worker);

        var updated = await ctx.Reservations.FindAsync(reservation.ReservationId);
        Assert.Equal(ReservationStatus.Cancelled, updated!.Status);
    }
}
