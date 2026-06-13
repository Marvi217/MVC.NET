using System.ComponentModel.DataAnnotations;

namespace CinePlex.Models
{
    public enum HallType
    {
        Standard, ThreeD, IMAX, FourD
    }

    public class Hall
    {
        public int HallId { get; set; }

        [Required(ErrorMessage = "Hall number is required")]
        [StringLength(20)]
        [Display(Name = "Hall Number")]
        public string Number { get; set; } = string.Empty;

        [Required]
        [Range(1, 1000, ErrorMessage = "Capacity must be between 1 and 1000")]
        public int Capacity { get; set; }

        [Display(Name = "Hall Type")]
        public HallType Type { get; set; }

        public string? LayoutJson { get; set; }

        [Required]
        [Display(Name = "Cinema")]
        public int CinemaId { get; set; }
        public Cinema? Cinema { get; set; }

        public ICollection<Screening> Screenings { get; set; } = new List<Screening>();
    }
}
