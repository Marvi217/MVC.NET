using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CinePlex.Data;
using CinePlex.Models;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace CinePlex.Areas.User.Controllers
{
    [Area("User")]

    public class MoviesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const int PageSize = 6;

        public MoviesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? search, Genre? genre, string? sort, int page = 1)
        {
            var query = _context.Movies.AsNoTracking().Include(m => m.Director).AsQueryable();

            if (!string.IsNullOrEmpty(search))
                query = query.Where(m => m.Title.Contains(search) || m.Director.LastName.Contains(search));

            if (genre.HasValue)
                query = query.Where(m => m.Genre == genre.Value);

            query = sort switch
            {
                "title_desc" => query.OrderByDescending(m => m.Title),
                "date" => query.OrderBy(m => m.ReleaseDate),
                "date_desc" => query.OrderByDescending(m => m.ReleaseDate),
                _ => query.OrderBy(m => m.Title)
            };

            var total = await query.CountAsync();
            var movies = await query.Skip((page - 1) * PageSize).Take(PageSize).ToListAsync();

            ViewBag.Search = search;
            ViewBag.Genre = genre;
            ViewBag.Sort = sort;
            ViewBag.Page = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)PageSize);
            ViewBag.Genres = Enum.GetValues<Genre>();

            return View(movies);
        }

        public async Task<IActionResult> Details(int id, int? cinemaId, DateTime? date)
        {
            var movie = await _context.Movies
                .AsNoTracking()
                .Include(m => m.Director)
                .FirstOrDefaultAsync(m => m.MovieId == id);

            if (movie == null) return NotFound();

            if (!string.IsNullOrEmpty(movie.TrailerUrl))
            {
                var m = Regex.Match(movie.TrailerUrl, @"[?&]v=([^&]+)");
                if (m.Success)
                    ViewBag.EmbedUrl = $"https://www.youtube.com/embed/{m.Groups[1].Value}";
            }

            if (!cinemaId.HasValue)
                cinemaId = HttpContext.Session.GetInt32("SelectedCinemaId");

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId != null)
            {
                ViewBag.UserReview = await _context.MovieReviews
                    .AsNoTracking()
                    .FirstOrDefaultAsync(r => r.AppUserId == userId && r.MovieId == id);
            }

            var reviews = await _context.MovieReviews
                .AsNoTracking()
                .Include(r => r.AppUser)
                .Where(r => r.MovieId == id && r.ModerationStatus != ReviewModerationStatus.Hidden)
                .OrderByDescending(r => r.CreatedAt)
                .Take(20)
                .ToListAsync();

            ViewBag.Reviews       = reviews;
            ViewBag.ReviewCount   = reviews.Count;
            ViewBag.AverageRating = reviews.Count > 0 ? reviews.Average(r => r.Rating) : (double?)null;

            var allCinemas = await _context.Cinemas.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
            ViewBag.AllCinemas = allCinemas;
            ViewBag.MovieId = id;

            await PopulateScreeningViewBag(id, cinemaId, date);

            if (cinemaId.HasValue)
                ViewBag.CinemaName = allCinemas.FirstOrDefault(c => c.CinemaId == cinemaId.Value)?.Name;

            return View(movie);
        }

        public async Task<IActionResult> ScreeningsPartial(int id, int? cinemaId, DateTime? date)
        {
            if (!cinemaId.HasValue)
                cinemaId = HttpContext.Session.GetInt32("SelectedCinemaId");

            ViewBag.MovieId = id;
            await PopulateScreeningViewBag(id, cinemaId, date);
            return PartialView("_ScreeningsPartial");
        }

        private async Task PopulateScreeningViewBag(int movieId, int? cinemaId, DateTime? date)
        {
            var selectedDate = date?.Date ?? DateTime.Today;
            ViewBag.DayStrip = Enumerable.Range(0, 7).Select(i => DateTime.Today.AddDays(i)).ToList();
            ViewBag.SelectedDate = selectedDate;
            ViewBag.CinemaId = cinemaId;

            if (!cinemaId.HasValue) return;

            var cinema = await _context.Cinemas.FindAsync(cinemaId.Value);
            if (cinema != null)
            {
                HttpContext.Session.SetInt32("SelectedCinemaId", cinema.CinemaId);
                HttpContext.Session.SetString("SelectedCinemaName", cinema.Name);
                HttpContext.Session.SetString("SelectedCinemaCity", cinema.City);
            }

            var nowMs = DateTime.Now;
            var screenings = await _context.Screenings
                .AsNoTracking()
                .Include(s => s.Hall)
                .Where(s => s.MovieId == movieId
                         && s.Hall.CinemaId == cinemaId.Value
                         && s.StartTime.Date == selectedDate)
                .Where(s => s.IsPublished || (s.PublishDate != null && s.PublishDate <= nowMs))
                .OrderBy(s => s.StartTime)
                .ToListAsync();

            ViewBag.ScreeningGroups = screenings
                .GroupBy(s => new { s.Hall!.Type, s.AudioVersion })
                .Select(hg => new {
                    Label = (hg.Key.Type switch {
                        HallType.IMAX   => "IMAX",
                        HallType.ThreeD => "3D",
                        HallType.FourD  => "4DX",
                        _               => "2D"
                    }) + " · " + (hg.Key.AudioVersion switch {
                        AudioVersion.Dubbed   => "DUBBING",
                        AudioVersion.Original => "ORYGINAŁ",
                        _                     => "NAPISY PL"
                    }),
                    Slots = hg.Select(s => new {
                        s.ScreeningId,
                        TimeLabel = s.StartTime.ToString("HH:mm"),
                        SoldOut = s.AvailableSeats == 0
                    }).ToList()
                }).ToList();
        }
    }
}
