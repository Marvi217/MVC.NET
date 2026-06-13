using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CinePlex.Data;

namespace CinePlex.Areas.User.Controllers
{
    [Area("User")]

    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HomeController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);

            var nowShowingIds = await _context.Screenings
                .AsNoTracking()
                .Where(s => s.StartTime >= today)
                .Where(s => s.IsPublished || (s.PublishDate != null && s.PublishDate <= today))
                .Select(s => s.MovieId)
                .Distinct()
                .ToListAsync();

            ViewBag.LatestMovies = await _context.Movies
                .AsNoTracking()
                .Where(m => nowShowingIds.Contains(m.MovieId))
                .OrderByDescending(m => m.ReleaseDate)
                .ToListAsync();

            ViewBag.ComingSoonMovies = await _context.Movies
                .AsNoTracking()
                .Where(m => !m.Screenings.Any() && m.ReleaseDate >= tomorrow)
                .OrderBy(m => m.ReleaseDate)
                .Take(6)
                .ToListAsync();

            ViewBag.Cinemas = await _context.Cinemas
                .AsNoTracking()
                .OrderBy(c => c.Name)
                .ToListAsync();

            return View();
        }

        public async Task<IActionResult> Bar()
        {
            var items = await _context.BarItems
                .AsNoTracking()
                .Where(b => b.IsAvailable)
                .OrderBy(b => b.Category).ThenBy(b => b.SortOrder).ThenBy(b => b.Name)
                .ToListAsync();
            ViewBag.BarSets = await _context.BarSets
                .AsNoTracking()
                .Include(s => s.Items).ThenInclude(i => i.BarItem)
                .Where(s => s.IsAvailable)
                .OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
                .ToListAsync();
            return View(items);
        }

        public async Task<IActionResult> SelectCinema(int? cinemaId, string? returnUrl)
        {
            if (cinemaId.HasValue)
            {
                var cinema = await _context.Cinemas.FindAsync(cinemaId.Value);
                if (cinema != null)
                {
                    HttpContext.Session.SetInt32("SelectedCinemaId", cinema.CinemaId);
                    HttpContext.Session.SetString("SelectedCinemaName", cinema.Name);
                    HttpContext.Session.SetString("SelectedCinemaCity", cinema.City);
                }
            }
            else
            {
                HttpContext.Session.Remove("SelectedCinemaId");
                HttpContext.Session.Remove("SelectedCinemaName");
                HttpContext.Session.Remove("SelectedCinemaCity");
            }
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) && cinemaId.HasValue)
            {
                returnUrl = System.Text.RegularExpressions.Regex.Replace(
                    returnUrl, @"cinemaId=\d+", $"cinemaId={cinemaId.Value}");
            }
            return Redirect(!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/");
        }
    }
}
