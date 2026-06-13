using System.ComponentModel.DataAnnotations;

namespace CinePlex.ViewModels
{
    public class CheckoutViewModel
    {
        public int ScreeningId { get; set; }
        public List<int> SeatNumbers { get; set; } = new();

        [MaxLength(100)]
        public string? FirstName { get; set; }
        [MaxLength(100)]
        public string? LastName { get; set; }
        [EmailAddress, MaxLength(200)]
        public string? Email { get; set; }
        [Phone, MaxLength(50)]
        public string? Phone { get; set; }

        public string PaymentMethod { get; set; } = "card";
    }

    public class SeatDetail
    {
        public int SeatNumber { get; set; }
        public string Label { get; set; } = "";
        public decimal Price { get; set; }
    }
}
