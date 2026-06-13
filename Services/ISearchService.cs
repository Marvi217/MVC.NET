using CinePlex.Models;

namespace CinePlex.Services
{
    public sealed class AdminSearchData
    {
        public List<Movie> Movies { get; init; } = new();
        public List<Director> Directors { get; init; } = new();
        public List<Cinema> Cinemas { get; init; } = new();
        public List<Hall> Halls { get; init; } = new();
        public List<Screening> Screenings { get; init; } = new();
        public List<Reservation> Reservations { get; init; } = new();
        public List<AppUser> Users { get; init; } = new();
        public List<BarItem> BarItems { get; init; } = new();
        public List<BarSet> BarSets { get; init; } = new();
    }

    public interface ISearchService
    {
        Task<AdminSearchData> SearchAdminAsync(string term);
        Task<(List<Movie> Movies, List<Cinema> Cinemas)> SearchUserAsync(string query);
    }
}
