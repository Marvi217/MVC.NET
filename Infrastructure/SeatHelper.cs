using CinePlex.Models;

namespace CinePlex.Infrastructure
{
    public static class SeatHelper
    {
        public static bool HasEdgeIsolatedSeat(Hall hall, HallLayoutConfig? layout, HashSet<int> takenSeats)
        {
            IEnumerable<(int Start, int Count)> rows;

            if (layout != null && layout.Rows.Count > 0)
            {
                int cursor = 0;
                rows = layout.Rows.Select(r =>
                {
                    var entry = (cursor + 1, r.SeatCount);
                    cursor += r.SeatCount;
                    return entry;
                }).ToList();
            }
            else
            {
                int cap = hall.Capacity, perRow = 10;
                rows = Enumerable.Range(0, (cap + perRow - 1) / perRow)
                    .Select(r => (r * perRow + 1, Math.Min(perRow, cap - r * perRow)));
            }

            foreach (var (start, count) in rows)
            {
                var free = Enumerable.Range(start, count)
                    .Select(n => !takenSeats.Contains(n))
                    .ToArray();

                int run = 0;
                for (int i = 0; i < free.Length; i++)
                {
                    if (free[i]){ run++;}
                    else{ if (run == 1) return true; run = 0; }
                }
                if (run == 1) return true;
            }
            return false;
        }
    }
}
