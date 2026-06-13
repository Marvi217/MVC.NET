using CinePlex.Models;

namespace CinePlex.ViewModels
{
    public class RepertoireViewModel
    {
        public List<Cinema> Cinemas { get; set; } = new();
        public Cinema? SelectedCinema { get; set; }
        public DateTime SelectedDate { get; set; }
        public List<DateTime> DayStrip { get; set; } = new();
        public List<Movie> Movies { get; set; } = new();
        public int? SelectedMovieId { get; set; }
        public HallType? SelectedHallType { get; set; }
        public List<RepertoireMovieGroup> Groups { get; set; } = new();
    }

    public class RepertoireMovieGroup
    {
        public Movie Movie { get; set; } = null!;
        public List<RepertoireHallGroup> HallGroups { get; set; } = new();
    }

    public class RepertoireHallGroup
    {
        public string HallTypeLabel { get; set; } = string.Empty;
        public string AudioLabel { get; set; } = string.Empty;
        public List<RepertoireSlot> Slots { get; set; } = new();
    }

    public class RepertoireSlot
    {
        public int ScreeningId { get; set; }
        public DateTime StartTime { get; set; }
        public bool SoldOut { get; set; }
    }
}
