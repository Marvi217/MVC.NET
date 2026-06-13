using System.ComponentModel.DataAnnotations;

namespace CinePlex.Models
{
    public enum ReviewModerationStatus
    {
        Approved = 0,  // SQL DEFAULT 0 — existing rows stay visible after migration
        Pending  = 1,
        Hidden   = 2
    }

    public class MovieReview
    {
        [Key]
        public int ReviewId { get; set; }

        public string AppUserId { get; set; } = string.Empty;
        public AppUser? AppUser { get; set; }

        public int MovieId { get; set; }
        public Movie? Movie { get; set; }

        [Range(1, 5)]
        public int Rating { get; set; }

        [StringLength(1000)]
        public string? Comment { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        public ReviewModerationStatus ModerationStatus { get; set; } = ReviewModerationStatus.Pending;
    }
}
