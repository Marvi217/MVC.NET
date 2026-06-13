using System.ComponentModel.DataAnnotations;

namespace CinePlex.Models
{
    public class Director
    {
        public int DirectorId { get; set; }

        [Required(ErrorMessage = "First name is required")]
        [StringLength(100)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Last name is required")]
        [StringLength(100)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Nationality { get; set; }

        [StringLength(1000)]
        public string? Biography { get; set; }

        [Display(Name = "Full Name")]
        public string FullName => $"{FirstName} {LastName}";

        public ICollection<Movie> Movies { get; set; } = new List<Movie>();
    }
}
