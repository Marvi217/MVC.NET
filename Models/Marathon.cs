using System.ComponentModel.DataAnnotations;

namespace CinePlex.Models
{
    public class Marathon
    {
        public int MarathonId { get; set; }

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(2000)]
        public string? Description { get; set; }

        [StringLength(500)]
        [Display(Name = "Poster URL")]
        public string? PosterUrl { get; set; }

        [Required]
        public decimal Price { get; set; }

        [Required]
        public int HallId { get; set; }
        public Hall? Hall { get; set; }

        [Required]
        [Display(Name = "Start Time")]
        public DateTime StartTime { get; set; }

        [Display(Name = "Active")]
        public bool IsActive { get; set; }

        [Display(Name = "Publish Date")]
        public DateTime? PublishDate { get; set; }

        public ICollection<Screening> Screenings { get; set; } = new List<Screening>();
        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    }
}
