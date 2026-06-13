using System.ComponentModel.DataAnnotations;

namespace CinePlex.Models
{
    public enum BarCategory { Przekaski, Napoje }

    public class BarItem
    {
        public int BarItemId { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(300)]
        public string? Description { get; set; }

        [Required]
        [Range(0.01, 9999, ErrorMessage = "Cena musi być większa od 0")]
        [DataType(DataType.Currency)]
        public decimal Price { get; set; }

        public BarCategory Category { get; set; }

        [StringLength(500)]
        public string? ImageUrl { get; set; }

        public bool IsAvailable { get; set; } = true;

        public int SortOrder { get; set; }
    }
}
