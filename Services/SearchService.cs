using CinePlex.Data;
using CinePlex.Models;
using Microsoft.EntityFrameworkCore;

namespace CinePlex.Services
{
    public class SearchService : ISearchService
    {
        private readonly ApplicationDbContext _context;

        public SearchService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<AdminSearchData> SearchAdminAsync(string term)
        {
            var movies = await _context.Movies
                .Where(m => m.Title.Contains(term))
                .OrderBy(m => m.Title)
                .Take(5)
                .ToListAsync();

            var directors = await _context.Directors
                .Where(d => d.FirstName.Contains(term) || d.LastName.Contains(term))
                .OrderBy(d => d.LastName)
                .Take(4)
                .ToListAsync();

            var cinemas = await _context.Cinemas
                .Where(c => c.Name.Contains(term) || c.City.Contains(term))
                .OrderBy(c => c.Name)
                .Take(4)
                .ToListAsync();

            var halls = await _context.Halls
                .Include(h => h.Cinema)
                .Where(h => h.Cinema!.Name.Contains(term) || h.Number.ToString().Contains(term))
                .OrderBy(h => h.Cinema!.Name).ThenBy(h => h.Number)
                .Take(4)
                .ToListAsync();

            var screenings = await _context.Screenings
                .Include(s => s.Movie)
                .Include(s => s.Hall).ThenInclude(h => h.Cinema)
                .Where(s => s.Movie!.Title.Contains(term))
                .OrderByDescending(s => s.StartTime)
                .Take(4)
                .ToListAsync();

            var reservations = await _context.Reservations
                .Where(r => r.ReservationCode.Contains(term) ||
                            (r.GuestName != null && r.GuestName.Contains(term)) ||
                            (r.GuestEmail != null && r.GuestEmail.Contains(term)))
                .OrderByDescending(r => r.PurchaseDate)
                .Take(4)
                .ToListAsync();

            var users = await _context.Users
                .Where(u => u.Email!.Contains(term) || u.FirstName.Contains(term) || u.LastName.Contains(term))
                .OrderBy(u => u.LastName)
                .Take(4)
                .ToListAsync();

            var barItems = await _context.BarItems
                .Where(b => b.Name.Contains(term))
                .OrderBy(b => b.Name)
                .Take(4)
                .ToListAsync();

            var barSets = await _context.BarSets
                .Where(s => s.Name.Contains(term))
                .OrderBy(s => s.Name)
                .Take(3)
                .ToListAsync();

            return new AdminSearchData
            {
                Movies = movies,
                Directors = directors,
                Cinemas = cinemas,
                Halls = halls,
                Screenings = screenings,
                Reservations = reservations,
                Users = users,
                BarItems = barItems,
                BarSets = barSets
            };
        }

        public async Task<(List<Movie> Movies, List<Cinema> Cinemas)> SearchUserAsync(string query)
        {
            var movies = await _context.Movies
                .Include(m => m.Director)
                .Where(m => m.Title.Contains(query) || m.Director.LastName.Contains(query) || m.Director.FirstName.Contains(query))
                .OrderBy(m => m.Title)
                .ToListAsync();

            var cinemas = await _context.Cinemas
                .Where(c => c.Name.Contains(query) || c.City.Contains(query) || c.Address.Contains(query))
                .OrderBy(c => c.City)
                .ToListAsync();

            return (movies, cinemas);
        }
    }
}
