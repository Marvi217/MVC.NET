using CinePlex.Models;
using Xunit;

namespace CinePlex.Tests.Unit;

public class ScreeningModelTests
{

    private static Hall MakeHall(int capacity) =>
        new Hall { HallId = 1, Number = "1", CinemaId = 1, Capacity = capacity };

    private static Reservation MakeReservation(ReservationStatus status) =>
        new Reservation
        {
            ReservationId = Random.Shared.Next(1, 100_000),
            SeatNumber = 1,
            Status = status,
            ScreeningId = 1,
        };

    private static Screening MakeScreening(Hall? hall, params ReservationStatus[] statuses)
    {
        var screening = new Screening
        {
            ScreeningId = 1,
            MovieId = 1,
            HallId = hall?.HallId ?? 0,
            Hall = hall,
        };
        foreach (var status in statuses)
            screening.Reservations.Add(MakeReservation(status));
        return screening;
    }

    [Fact]
    public void AvailableSeats_NullHall_ReturnsZeroMinusNonCancelledCount()
    {
        var screening = MakeScreening(null);

        Assert.Equal(0, screening.AvailableSeats);
    }

    [Fact]
    public void AvailableSeats_NoReservations_ReturnsFullCapacity()
    {
        var screening = MakeScreening(MakeHall(50));

        Assert.Equal(50, screening.AvailableSeats);
    }

    [Fact]
    public void AvailableSeats_TwoConfirmed_DeductsTwo()
    {
        var screening = MakeScreening(
            MakeHall(50),
            ReservationStatus.Confirmed,
            ReservationStatus.Confirmed);

        Assert.Equal(48, screening.AvailableSeats);
    }

    [Fact]
    public void AvailableSeats_ConfirmedAndCancelled_CancelledNotCounted()
    {
        var screening = MakeScreening(
            MakeHall(50),
            ReservationStatus.Confirmed,
            ReservationStatus.Confirmed,
            ReservationStatus.Cancelled,
            ReservationStatus.Cancelled,
            ReservationStatus.Cancelled);

        Assert.Equal(48, screening.AvailableSeats);
    }

    [Fact]
    public void AvailableSeats_PendingAndConfirmed_PendingCountsAsTaken()
    {
        var screening = MakeScreening(
            MakeHall(50),
            ReservationStatus.Pending,
            ReservationStatus.Pending,
            ReservationStatus.Confirmed);

        Assert.Equal(47, screening.AvailableSeats);
    }

    [Fact]
    public void AvailableSeats_AllCancelled_ReturnsFullCapacity()
    {
        var screening = MakeScreening(
            MakeHall(100),
            ReservationStatus.Cancelled,
            ReservationStatus.Cancelled,
            ReservationStatus.Cancelled);

        Assert.Equal(100, screening.AvailableSeats);
    }

    [Fact]
    public void NewScreening_DefaultTicketPrice_Is25()
    {
        var screening = new Screening();

        Assert.Equal(25m, screening.TicketPrice);
    }

    [Fact]
    public void NewScreening_IsPublished_DefaultsFalse()
    {
        var screening = new Screening();

        Assert.False(screening.IsPublished);
    }

    [Fact]
    public void NewScreening_Reservations_DefaultsToEmptyCollection()
    {
        var screening = new Screening();

        Assert.NotNull(screening.Reservations);
        Assert.Empty(screening.Reservations);
    }

    [Fact]
    public void AudioVersion_Dubbed_IsZero()
    {
        Assert.Equal(0, (int)AudioVersion.Dubbed);
    }

    [Fact]
    public void AudioVersion_Subtitled_IsOne()
    {
        Assert.Equal(1, (int)AudioVersion.Subtitled);
    }

    [Fact]
    public void AudioVersion_Original_IsTwo()
    {
        Assert.Equal(2, (int)AudioVersion.Original);
    }
}
