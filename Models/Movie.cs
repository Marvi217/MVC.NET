using System.ComponentModel.DataAnnotations;

namespace CinePlex.Models
{
    public enum Genre
    {
        Action, Comedy, Horror, SciFi, Drama, Thriller, Animation, Documentary, Romance, Adventure
    }

    public enum AgeRating
    {
        AllAges, Age7, Age13, Age16, Age18
    }

    public class Movie
    {
        public int MovieId { get; set; }

        [Required(ErrorMessage = "Title is required")]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Description is required")]
        [StringLength(2000)]
        public string Description { get; set; } = string.Empty;

        [Required]
        [Range(1, 600, ErrorMessage = "Duration must be between 1 and 600 minutes")]
        [Display(Name = "Duration (min)")]
        public int Duration { get; set; }

        [Required]
        [Display(Name = "Release Date")]
        [DataType(DataType.Date)]
        public DateTime ReleaseDate { get; set; }

        public Genre Genre { get; set; }

        [Display(Name = "Age Rating")]
        public AgeRating AgeRating { get; set; }

        [StringLength(500)]
        [Display(Name = "Poster URL")]
        public string? PosterUrl { get; set; }

        [StringLength(500)]
        [Display(Name = "Trailer URL")]
        public string? TrailerUrl { get; set; }

        public string? CastJson { get; set; }

        public int? TmdbId { get; set; }

        [Required]
        [Display(Name = "Director")]
        public int DirectorId { get; set; }
        public Director? Director { get; set; }

        public ICollection<Screening> Screenings { get; set; } = new List<Screening>();
    }
}
