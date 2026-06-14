using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CinePlex.Data;
using CinePlex.Models;
using CinePlex.Services;

namespace CinePlex.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class MoviesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileUploadService _fileUpload;

        public MoviesController(ApplicationDbContext context, IFileUploadService fileUpload)
        {
            _context = context;
            _fileUpload = fileUpload;
        }

        public async Task<IActionResult> Index(string? search, Genre? genre, int page = 1, int pageSize = 20)
        {
            var allowed = new[] { 10, 20, 50, 100 };
            if (!allowed.Contains(pageSize)) pageSize = 20;
            var query = _context.Movies.Include(m => m.Director).AsQueryable();
            if (!string.IsNullOrEmpty(search))
            {
                var matchingGenres = Enum.GetValues<Genre>()
                    .Where(g => g.ToString().Contains(search, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                var matchingRatings = Enum.GetValues<AgeRating>()
                    .Where(r => r.ToString().Contains(search, StringComparison.OrdinalIgnoreCase))
                    .ToList();
                int? yearSearch = int.TryParse(search, out var yr) && yr >= 1900 && yr <= 2100 ? yr : null;
                query = query.Where(m =>
                    m.Title.Contains(search) ||
                    m.Description.Contains(search) ||
                    (m.CastJson != null && (
                        m.CastJson.Contains(":\"" + search) ||
                        m.CastJson.Contains(" " + search + "\"") ||
                        m.CastJson.Contains(" " + search + " ")
                    )) ||
                    m.Director.FirstName.Contains(search) ||
                    m.Director.LastName.Contains(search) ||
                    matchingGenres.Contains(m.Genre) ||
                    matchingRatings.Contains(m.AgeRating) ||
                    (yearSearch.HasValue && m.ReleaseDate.Year == yearSearch.Value)
                );
            }
            if (genre.HasValue) query = query.Where(m => m.Genre == genre.Value);
            var total = await query.CountAsync();

            var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);

            ViewBag.Search = search;
            ViewBag.Genre = genre;
            ViewBag.PageSize = pageSize;
            ViewBag.Page = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.Genres = Enum.GetValues<Genre>();
            TempData["AdminMoviesPageSize"] = pageSize;
            return View(await query.OrderBy(m => m.Title).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync());
        }

        public async Task<IActionResult> Details(int id)
        {
            var movie = await _context.Movies
                .Include(m => m.Director)
                .Include(m => m.Screenings).ThenInclude(s => s.Hall).ThenInclude(h => h.Cinema)
                .Include(m => m.Screenings).ThenInclude(s => s.Reservations)
                .FirstOrDefaultAsync(m => m.MovieId == id);
            if (movie == null) return NotFound();
            return View(movie);
        }

        private async Task<List<Director>> GetDirectorsAsync() =>
            await _context.Directors.OrderBy(d => d.LastName).ToListAsync();

        public async Task<IActionResult> Create()
        {
            ViewBag.Directors = await GetDirectorsAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Movie movie, IFormFile? posterFile)
        {
            if (movie.DirectorId == 0) ModelState.Remove(nameof(movie.DirectorId));
            if (ModelState.IsValid)
            {
                var uploaded = await _fileUpload.SavePosterAsync(posterFile);
                if (uploaded != null) movie.PosterUrl = uploaded;
                _context.Movies.Add(movie);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Movie added successfully.";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Directors = await GetDirectorsAsync();
            return View(movie);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var movie = await _context.Movies.Include(m => m.Director).FirstOrDefaultAsync(m => m.MovieId == id);
            if (movie == null) return NotFound();
            ViewBag.Directors = await GetDirectorsAsync();
            return View(movie);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Movie movie, IFormFile? posterFile)
        {
            if (id != movie.MovieId) return NotFound();
            if (movie.DirectorId == 0) ModelState.Remove(nameof(movie.DirectorId));
            if (ModelState.IsValid)
            {
                var uploaded = await _fileUpload.SavePosterAsync(posterFile);
                if (uploaded != null) movie.PosterUrl = uploaded;
                _context.Update(movie);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Movie updated successfully.";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Directors = await GetDirectorsAsync();
            return View(movie);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var movie = await _context.Movies.Include(m => m.Director).FirstOrDefaultAsync(m => m.MovieId == id);
            if (movie == null) return NotFound();
            return View(movie);
        }

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var movie = await _context.Movies
                .Include(m => m.Screenings)
                    .ThenInclude(s => s.Reservations)
                .FirstOrDefaultAsync(m => m.MovieId == id);
            if (movie != null)
            {
                foreach (var s in movie.Screenings)
                    _context.Reservations.RemoveRange(s.Reservations);
                _context.Screenings.RemoveRange(movie.Screenings);
                _context.Movies.Remove(movie);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Movie deleted.";
            }
            var ps = TempData.Peek("AdminMoviesPageSize") as int? ?? 20;
            return RedirectToAction(nameof(Index), new { pageSize = ps });
        }

    }
}
