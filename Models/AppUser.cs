using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;

namespace CinePlex.Models
{
    public class AppUser : IdentityUser
    {
        [Required]
        [StringLength(100)]
        [Display(Name = "First Name")]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [Display(Name = "Last Name")]
        public string LastName { get; set; } = string.Empty;

        [Display(Name = "Registered At")]
        public DateTime RegisteredAt { get; set; } = DateTime.UtcNow;

        [Display(Name = "Blocked")]
        public bool IsBlocked { get; set; } = false;

        [Display(Name = "Full Name")]
        public string FullName => $"{FirstName} {LastName}";

        public ICollection<Reservation> Reservations { get; set; } = new List<Reservation>();
    }
}
