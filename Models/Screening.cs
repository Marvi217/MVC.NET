using System.ComponentModel.DataAnnotations;

namespace CinePlex.Models
{
    public enum AudioVersion
    {
        Dubbed, Subtitled, Original
    }

    public class Screening
    {
        public int ScreeningId { get; set; }

        [Required]
        [Display(Name = "Start Time")]
        public DateTime StartTime { get; set; }

        public decimal TicketPrice { get; set; } = 25m;

        [Display(Name = "Audio Version")]
        public AudioVersion AudioVersion { get; set; }

        [Required]
        [Display(Name = "Movie")]
        public int MovieId { get; set; }
        public Movie? Movie { get; set; }

        [Required]
        [Display(Name = "Hall")]
        public int HallId { get; set; }
        public Hall? Hall { get; set; }

        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();

        public int? MarathonId { get; set; }
        public Marathon? Marathon { get; set; }

        public bool IsPublished { get; set; } = false;
        public DateTime? PublishDate { get; set; }

        public int AvailableSeats => (Hall?.Capacity ?? 0) - Reservations.Count(r => r.Status != ReservationStatus.Cancelled);
    }
}
