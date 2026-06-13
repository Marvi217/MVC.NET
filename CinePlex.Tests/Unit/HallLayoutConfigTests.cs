using System.Text.Json;
using CinePlex.Models;
using Xunit;

namespace CinePlex.Tests.Unit;

public class HallLayoutConfigTests
{

    private static HallLayoutConfig MakeAB() => new HallLayoutConfig
    {
        Rows = new List<HallRowConfig>
        {
            new() { Label = "A", SeatCount = 5, PriceMultiplier = 1.0m },
            new() { Label = "B", SeatCount = 3, PriceMultiplier = 1.0m }
        }
    };

    [Fact]
    public void TotalSeats_EmptyRows_ReturnsZero()
    {
        var cfg = new HallLayoutConfig();

        Assert.Equal(0, cfg.TotalSeats);
    }

    [Fact]
    public void TotalSeats_SingleRow_ReturnsSeatCount()
    {
        var cfg = new HallLayoutConfig
        {
            Rows = new List<HallRowConfig> { new() { Label = "A", SeatCount = 5 } }
        };

        Assert.Equal(5, cfg.TotalSeats);
    }

    [Fact]
    public void TotalSeats_MultipleRows_ReturnsSum()
    {
        var cfg = MakeAB();

        Assert.Equal(8, cfg.TotalSeats);
    }

    [Fact]
    public void GetRowForSeat_FirstSeat_ReturnsRowA()
    {
        var cfg = MakeAB();

        var row = cfg.GetRowForSeat(1);

        Assert.NotNull(row);
        Assert.Equal("A", row!.Label);
    }

    [Fact]
    public void GetRowForSeat_LastSeatOfFirstRow_ReturnsRowA()
    {
        var cfg = MakeAB();

        var row = cfg.GetRowForSeat(5);

        Assert.NotNull(row);
        Assert.Equal("A", row!.Label);
    }

    [Fact]
    public void GetRowForSeat_FirstSeatOfSecondRow_ReturnsRowB()
    {
        var cfg = MakeAB();

        var row = cfg.GetRowForSeat(6);

        Assert.NotNull(row);
        Assert.Equal("B", row!.Label);
    }

    [Fact]
    public void GetRowForSeat_LastSeatOfSecondRow_ReturnsRowB()
    {
        var cfg = MakeAB();

        var row = cfg.GetRowForSeat(8);

        Assert.NotNull(row);
        Assert.Equal("B", row!.Label);
    }

    [Fact]
    public void GetRowForSeat_OutOfRange_ReturnsNull()
    {
        var cfg = MakeAB();

        var row = cfg.GetRowForSeat(9);

        Assert.Null(row);
    }

    [Fact]
    public void GetRowForSeat_SeatZero_ReturnsNull()
    {
        var cfg = MakeAB();

        var row = cfg.GetRowForSeat(0);

        Assert.Null(row);
    }

    [Fact]
    public void GetSeatLabel_Seat1_ReturnsA1()
    {
        var cfg = MakeAB();

        Assert.Equal("A1", cfg.GetSeatLabel(1));
    }

    [Fact]
    public void GetSeatLabel_Seat5_ReturnsA5()
    {
        var cfg = MakeAB();

        Assert.Equal("A5", cfg.GetSeatLabel(5));
    }

    [Fact]
    public void GetSeatLabel_Seat6_ReturnsB1()
    {
        var cfg = MakeAB();

        Assert.Equal("B1", cfg.GetSeatLabel(6));
    }

    [Fact]
    public void GetSeatLabel_Seat8_ReturnsB3()
    {
        var cfg = MakeAB();

        Assert.Equal("B3", cfg.GetSeatLabel(8));
    }

    [Fact]
    public void GetSeatLabel_OutOfRange_ReturnsSeatNumberString()
    {
        var cfg = MakeAB();

        Assert.Equal("9", cfg.GetSeatLabel(9));
    }

    [Fact]
    public void GetSeatLabel_ReverseNumbering_Seat1_ReturnsA5()
    {
        var cfg = MakeAB();
        cfg.ReverseNumbering = true;

        Assert.Equal("A5", cfg.GetSeatLabel(1));
    }

    [Fact]
    public void GetSeatLabel_ReverseNumbering_Seat5_ReturnsA1()
    {
        var cfg = MakeAB();
        cfg.ReverseNumbering = true;

        Assert.Equal("A1", cfg.GetSeatLabel(5));
    }

    [Fact]
    public void FromJson_NullInput_ReturnsNull()
    {
        Assert.Null(HallLayoutConfig.FromJson(null));
    }

    [Fact]
    public void FromJson_EmptyString_ReturnsNull()
    {
        Assert.Null(HallLayoutConfig.FromJson(""));
    }

    [Fact]
    public void FromJson_Whitespace_ReturnsNull()
    {
        Assert.Null(HallLayoutConfig.FromJson("   "));
    }

    [Fact]
    public void FromJson_ValidJson_ReturnsDeserializedObject()
    {
        const string json = """
            {
                "Rows": [
                    { "Label": "A", "SeatCount": 5, "PriceMultiplier": 1.0 }
                ],
                "AisleAfterColumns": [2],
                "ReverseNumbering": false
            }
            """;

        var cfg = HallLayoutConfig.FromJson(json);

        Assert.NotNull(cfg);
        Assert.Single(cfg!.Rows);
        Assert.Equal("A", cfg.Rows[0].Label);
        Assert.Equal(5, cfg.Rows[0].SeatCount);
        Assert.Contains(2, cfg.AisleAfterColumns);
    }

    [Fact]
    public void FromJson_CaseInsensitiveKeys_Deserializes()
    {
        const string json = """
            {
                "rows": [
                    { "label": "Z", "seatCount": 7, "priceMultiplier": 1.5 }
                ]
            }
            """;

        var cfg = HallLayoutConfig.FromJson(json);

        Assert.NotNull(cfg);
        Assert.Single(cfg!.Rows);
        Assert.Equal("Z", cfg.Rows[0].Label);
        Assert.Equal(7, cfg.Rows[0].SeatCount);
    }

    [Fact]
    public void ToJson_FromJson_RoundTrip_PreservesData()
    {
        var original = new HallLayoutConfig
        {
            ReverseNumbering = true,
            AisleAfterColumns = new List<int> { 3, 6 },
            Rows = new List<HallRowConfig>
            {
                new() { Label = "A", SeatCount = 6, PriceMultiplier = 1.2m },
                new() { Label = "B", SeatCount = 4, PriceMultiplier = 2.0m, IsSofa = true }
            }
        };

        string json = original.ToJson();
        var restored = HallLayoutConfig.FromJson(json);

        Assert.NotNull(restored);
        Assert.Equal(original.ReverseNumbering, restored!.ReverseNumbering);
        Assert.Equal(original.AisleAfterColumns, restored.AisleAfterColumns);
        Assert.Equal(original.Rows.Count, restored.Rows.Count);
        Assert.Equal(original.Rows[0].Label, restored.Rows[0].Label);
        Assert.Equal(original.Rows[0].SeatCount, restored.Rows[0].SeatCount);
        Assert.Equal(original.Rows[0].PriceMultiplier, restored.Rows[0].PriceMultiplier);
        Assert.Equal(original.Rows[1].Label, restored.Rows[1].Label);
        Assert.Equal(original.Rows[1].IsSofa, restored.Rows[1].IsSofa);
        Assert.Equal(original.TotalSeats, restored.TotalSeats);
    }
}
