using CinePlex.Models;

namespace CinePlex.ViewModels
{
    public class BookingHistoryItem
    {
        public enum BookingKind { Screening, Marathon }

        public BookingKind Kind { get; set; }
        public DateTime EventDate { get; set; }
        public DateTime PurchaseDate { get; set; }
        public string ReservationCode { get; set; } = string.Empty;
        public decimal PricePaid { get; set; }
        public ReservationStatus Status { get; set; }

        public Reservation? Reservation { get; set; }
    }
}
