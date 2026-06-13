using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CinePlex.Data;
using CinePlex.Models;
using CinePlex.ViewModels;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace CinePlex.Areas.User.Controllers
{
    [Area("User")]

    public class ComingSoonController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ComingSoonController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var tomorrow = DateTime.Today.AddDays(1);
            var movies = await _context.Movies
                .Include(m => m.Director)
                .Where(m => !m.Screenings.Any() && m.ReleaseDate >= tomorrow)
                .OrderBy(m => m.ReleaseDate)
                .ToListAsync();

            return View(movies);
        }

        public async Task<IActionResult> Details(int id)
        {
            var movie = await _context.Movies
                .Include(m => m.Director)
                .FirstOrDefaultAsync(m => m.MovieId == id);

            if (movie == null) return NotFound();

            var cast = new List<CastMemberViewModel>();
            if (!string.IsNullOrEmpty(movie.CastJson))
            {
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                try
                {
                    cast = JsonSerializer.Deserialize<List<CastMemberViewModel>>(movie.CastJson, options)
                           ?? new List<CastMemberViewModel>();
                }
                catch (JsonException)
                {
                    var names = JsonSerializer.Deserialize<List<string>>(movie.CastJson) ?? new List<string>();
                    cast = names.Select(n => new CastMemberViewModel { Name = n, Character = "" }).ToList();
                }
            }

            string? embedUrl = null;
            if (!string.IsNullOrEmpty(movie.TrailerUrl))
            {
                var match = Regex.Match(movie.TrailerUrl, @"[?&]v=([^&]+)");
                if (match.Success)
                    embedUrl = $"https://www.youtube.com/embed/{match.Groups[1].Value}";
            }

            ViewBag.Cast = cast;
            ViewBag.EmbedUrl = embedUrl;

            return View(movie);
        }
    }
}
