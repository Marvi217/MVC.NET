using System.ComponentModel.DataAnnotations;

namespace CinePlex.Models
{
    public class BarSet
    {
        public int BarSetId { get; set; }

        [Required, StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [StringLength(300)]
        public string? Description { get; set; }

        [Required]
        [Range(0.01, 9999)]
        [DataType(DataType.Currency)]
        public decimal Price { get; set; }

        [StringLength(500)]
        public string? ImageUrl { get; set; }

        public bool IsAvailable { get; set; } = true;

        public int SortOrder { get; set; }

        public ICollection<BarSetItem> Items { get; set; } = new List<BarSetItem>();
    }

    public class BarSetItem
    {
        public int BarSetItemId { get; set; }

        public int BarSetId { get; set; }
        public BarSet? BarSet { get; set; }

        public int BarItemId { get; set; }
        public BarItem? BarItem { get; set; }

        [Range(1, 20)]
        public int Quantity { get; set; } = 1;
    }
}
