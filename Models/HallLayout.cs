using System.Text.Json;

namespace CinePlex.Models
{
    public class HallLayoutConfig
    {
        public List<HallRowConfig> Rows { get; set; } = new();
        public List<int> AisleAfterColumns { get; set; } = new();
        public bool ReverseNumbering { get; set; }

        public int TotalSeats => Rows.Sum(r => r.SeatCount);

        public HallRowConfig? GetRowForSeat(int seatNumber)
        {
            int cursor = 0;
            foreach (var row in Rows)
            {
                if (seatNumber > cursor && seatNumber <= cursor + row.SeatCount)
                    return row;
                cursor += row.SeatCount;
            }
            return null;
        }

        public string GetSeatLabel(int seatNumber)
        {
            int cursor = 0;
            foreach (var row in Rows)
            {
                if (seatNumber > cursor && seatNumber <= cursor + row.SeatCount)
                {
                    int pos = seatNumber - cursor;
                    if (ReverseNumbering) pos = row.SeatCount - pos + 1;
                    return $"{row.Label}{pos}";
                }
                cursor += row.SeatCount;
            }
            return seatNumber.ToString();
        }

        private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };
        private static readonly JsonSerializerOptions _camelCaseOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

        public static HallLayoutConfig? FromJson(string? json) =>
            string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<HallLayoutConfig>(json, _jsonOpts);

        public string ToJson() => JsonSerializer.Serialize(this);

        public string GetRowsJson() => JsonSerializer.Serialize(Rows, _camelCaseOpts);

        public string GetAislesJson() => JsonSerializer.Serialize(AisleAfterColumns);
    }

    public class HallRowConfig
    {
        public string Label { get; set; } = "";
        public int SeatCount { get; set; }
        public bool ExtraSpacingBefore { get; set; }
        public decimal PriceMultiplier { get; set; } = 1.0m;
        public bool IsSofa { get; set; }
        public bool HasPassageAfter { get; set; } = true;
    }
}
