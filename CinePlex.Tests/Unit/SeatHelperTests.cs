using CinePlex.Infrastructure;
using CinePlex.Models;
using Xunit;

namespace CinePlex.Tests.Unit;

public class SeatHelperTests
{

    private static Hall MakeHall(int capacity) =>
        new Hall { HallId = 1, Number = "1", CinemaId = 1, Capacity = capacity };

    private static HallLayoutConfig MakeLayout(params (string Label, int Seats)[] rows)
    {
        var cfg = new HallLayoutConfig();
        foreach (var (label, seats) in rows)
            cfg.Rows.Add(new HallRowConfig { Label = label, SeatCount = seats, PriceMultiplier = 1.0m });
        return cfg;
    }

    [Fact]
    public void NoLayout_NoTakenSeats_ReturnsFalse()
    {
        var hall = MakeHall(10);

        bool result = SeatHelper.HasEdgeIsolatedSeat(hall, null, new HashSet<int>());

        Assert.False(result);
    }

    [Fact]
    public void NoLayout_LeftEdgeIsolated_ReturnsTrue()
    {
        var hall = MakeHall(10);
        var taken = new HashSet<int>(Enumerable.Range(2, 9));

        bool result = SeatHelper.HasEdgeIsolatedSeat(hall, null, taken);

        Assert.True(result);
    }

    [Fact]
    public void NoLayout_RightEdgeIsolated_ReturnsTrue()
    {
        var hall = MakeHall(10);
        var taken = new HashSet<int>(Enumerable.Range(1, 9));

        bool result = SeatHelper.HasEdgeIsolatedSeat(hall, null, taken);

        Assert.True(result);
    }

    [Fact]
    public void NoLayout_TwoLeftFree_ReturnsFalse()
    {
        var hall = MakeHall(10);
        var taken = new HashSet<int>(Enumerable.Range(3, 8));

        bool result = SeatHelper.HasEdgeIsolatedSeat(hall, null, taken);

        Assert.False(result);
    }

    [Fact]
    public void NoLayout_MultiRow_SecondRowAllTaken_ReturnsFalse()
    {
        var hall = MakeHall(15);
        var taken = new HashSet<int>(Enumerable.Range(11, 5));

        bool result = SeatHelper.HasEdgeIsolatedSeat(hall, null, taken);

        Assert.False(result);
    }

    [Fact]
    public void NoLayout_LeftEdgeIsolatedInSecondRow_ReturnsTrue()
    {
        var hall = MakeHall(15);
        var taken = new HashSet<int>(Enumerable.Range(12, 4));

        bool result = SeatHelper.HasEdgeIsolatedSeat(hall, null, taken);

        Assert.True(result);
    }

    [Fact]
    public void NoLayout_CapacityNotMultipleOfTen_LastRowTwoFree_ReturnsFalse()
    {
        var hall = MakeHall(12);

        bool result = SeatHelper.HasEdgeIsolatedSeat(hall, null, new HashSet<int>());

        Assert.False(result);
    }

    [Fact]
    public void WithLayout_NoTakenSeats_ReturnsFalse()
    {
        var hall = MakeHall(10);
        var layout = MakeLayout(("A", 5), ("B", 5));

        bool result = SeatHelper.HasEdgeIsolatedSeat(hall, layout, new HashSet<int>());

        Assert.False(result);
    }

    [Fact]
    public void WithLayout_LeftEdgeIsolated_ReturnsTrue()
    {
        var hall = MakeHall(10);
        var layout = MakeLayout(("A", 5), ("B", 5));
        var taken = new HashSet<int> { 2, 3, 4, 5, 6, 7, 8, 9, 10 };

        bool result = SeatHelper.HasEdgeIsolatedSeat(hall, layout, taken);

        Assert.True(result);
    }

    [Fact]
    public void WithLayout_AllSeatsTaken_ReturnsFalse()
    {
        var hall = MakeHall(10);
        var layout = MakeLayout(("A", 5), ("B", 5));
        var taken = new HashSet<int>(Enumerable.Range(1, 10));

        bool result = SeatHelper.HasEdgeIsolatedSeat(hall, layout, taken);

        Assert.False(result);
    }

    [Fact]
    public void WithEmptyLayout_FallsBackToCapacityBased_ReturnsFalse()
    {
        var hall = MakeHall(10);
        var layout = new HallLayoutConfig(); // Rows is empty

        bool result = SeatHelper.HasEdgeIsolatedSeat(hall, layout, new HashSet<int>());

        Assert.False(result);
    }

    [Fact]
    public void WithEmptyLayout_LeftEdgeIsolated_ReturnsTrue()
    {
        var hall = MakeHall(10);
        var layout = new HallLayoutConfig();
        var taken = new HashSet<int>(Enumerable.Range(2, 9));

        bool result = SeatHelper.HasEdgeIsolatedSeat(hall, layout, taken);

        Assert.True(result);
    }

    [Fact]
    public void NoLayout_MiddleIsolated_ReturnsTrue()
    {
        var hall = MakeHall(10);
        var taken = new HashSet<int> { 1, 3 };

        bool result = SeatHelper.HasEdgeIsolatedSeat(hall, null, taken);

        Assert.True(result);
    }

    [Fact]
    public void NoLayout_MiddleIsolated_TwoSeatsAppart_ReturnsTrue()
    {
        var hall = MakeHall(10);
        var taken = new HashSet<int> { 2, 4 };

        bool result = SeatHelper.HasEdgeIsolatedSeat(hall, null, taken);

        Assert.True(result);
    }

    [Fact]
    public void NoLayout_TwoFreeInMiddle_ReturnsFalse()
    {
        var hall = MakeHall(10);
        var taken = new HashSet<int> { 1, 4 };

        bool result = SeatHelper.HasEdgeIsolatedSeat(hall, null, taken);

        Assert.False(result);
    }

    [Fact]
    public void WithLayout_MiddleIsolated_ReturnsTrue()
    {
        var layout = MakeLayout(("A", 5));
        var hall = MakeHall(5);
        var taken = new HashSet<int> { 1, 3 };

        bool result = SeatHelper.HasEdgeIsolatedSeat(hall, layout, taken);

        Assert.True(result);
    }

    [Fact]
    public void WithLayout_MultiRow_MiddleIsolatedInSecondRow_ReturnsTrue()
    {
        var layout = MakeLayout(("A", 5), ("B", 5));
        var hall = MakeHall(10);
        var taken = new HashSet<int> { 6, 8 };

        bool result = SeatHelper.HasEdgeIsolatedSeat(hall, layout, taken);

        Assert.True(result);
    }
}
