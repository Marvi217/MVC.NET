namespace CinePlex.Models
{
    public class HallTypePricing
    {
        public int Id { get; set; }
        public HallType HallType { get; set; }
        public decimal DefaultPrice { get; set; }
    }
}
