using System.ComponentModel.DataAnnotations;

namespace CinePlex.Models
{
    public class Cinema
    {
        public int CinemaId { get; set; }

        [Required(ErrorMessage = "Name is required")]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "City is required")]
        [StringLength(100)]
        public string City { get; set; } = string.Empty;

        [Required(ErrorMessage = "Address is required")]
        [StringLength(300)]
        public string Address { get; set; } = string.Empty;

        [Phone]
        [StringLength(20)]
        public string? Phone { get; set; }

        [EmailAddress]
        [StringLength(200)]
        public string? Email { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public ICollection<Hall> Halls { get; set; } = new List<Hall>();
    }
}
