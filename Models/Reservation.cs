using System.ComponentModel.DataAnnotations;

namespace CinePlex.Models
{
    public enum ReservationType { Screening = 0, Marathon = 1 }

    public enum ReservationStatus{ Pending, Confirmed, Cancelled }

    public class Reservation
    {
        public int ReservationId { get; set; }

        [Required]
        [Range(1, 1000)]
        [Display(Name = "Seat Number")]
        public int SeatNumber { get; set; }

        [StringLength(10)]
        public string? SeatRow { get; set; }

        [Display(Name = "Status")]
        public ReservationStatus Status { get; set; } = ReservationStatus.Confirmed;

        [Display(Name = "Purchase Date")]
        public DateTime PurchaseDate { get; set; } = DateTime.UtcNow;

        public DateTime? ExpiresAt { get; set; }

        [Display(Name = "Price Paid")]
        public decimal PricePaid { get; set; }

        [Required]
        [StringLength(50)]
        [Display(Name = "Reservation Code")]
        public string ReservationCode { get; set; } = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();

        public ReservationType ReservationType { get; set; } = ReservationType.Screening;

        [Display(Name = "Screening")]
        public int? ScreeningId { get; set; }
        public Screening? Screening { get; set; }

        public int? MarathonId { get; set; }
        public Marathon? Marathon { get; set; }

        [Display(Name = "User")]
        public string? AppUserId { get; set; }
        public AppUser? AppUser { get; set; }

        [StringLength(200)]
        public string? GuestEmail { get; set; }
        [StringLength(100)]
        public string? GuestName { get; set; }
        [StringLength(50)]
        public string? GuestPhone { get; set; }
    }
}
