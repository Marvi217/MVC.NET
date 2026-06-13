using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CinePlex.Data;
using CinePlex.Models;
using CinePlex.Services;

namespace CinePlex.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class MarathonsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileUploadService _fileUpload;

        public MarathonsController(ApplicationDbContext context, IFileUploadService fileUpload)
        {
            _context = context;
            _fileUpload = fileUpload;
        }

        public async Task<IActionResult> Index()
        {
            var marathons = await _context.Marathons
                .AsNoTracking()
                .Include(m => m.Hall).ThenInclude(h => h.Cinema)
                .Include(m => m.Screenings)
                .Include(m => m.Reservations)
                .OrderByDescending(m => m.StartTime)
                .ToListAsync();
            return View(marathons);
        }

        public async Task<IActionResult> Details(int id)
        {
            var marathon = await _context.Marathons
                .AsNoTracking()
                .Include(m => m.Hall).ThenInclude(h => h.Cinema)
                .Include(m => m.Screenings).ThenInclude(s => s.Movie)
                .Include(m => m.Reservations).ThenInclude(r => r.AppUser)
                .FirstOrDefaultAsync(m => m.MarathonId == id);
            if (marathon == null) return NotFound();
            return View(marathon);
        }

        public async Task<IActionResult> Create()
        {
            await PopulateViewBag();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Marathon marathon, List<int> movieIds, IFormFile? posterFile)
        {
            if (movieIds.Count < 2)
                ModelState.AddModelError("", "Maraton musi zawierać co najmniej 2 filmy.");
            if (marathon.StartTime < DateTime.Today.AddDays(7))
                ModelState.AddModelError("StartTime", "Maraton musi być zaplanowany co najmniej 7 dni do przodu.");

            if (ModelState.IsValid)
            {
                var movies = await _context.Movies
                    .Where(m => movieIds.Contains(m.MovieId))
                    .ToDictionaryAsync(m => m.MovieId);

                var screenings = BuildScreenings(marathon, movieIds, movies);
                var collision = await CheckCollisionsAsync(marathon.HallId, screenings, movies);
                if (collision != null)
                {
                    ModelState.AddModelError("", $"Kolizja z istniejącym seansem około {collision}.");
                    await PopulateViewBag();
                    return View(marathon);
                }

                var uploaded = await _fileUpload.SavePosterAsync(posterFile);
                if (uploaded != null) marathon.PosterUrl = uploaded;
                marathon.Screenings = screenings;
                _context.Marathons.Add(marathon);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Maraton '{marathon.Name}' dodany. Wygenerowano {screenings.Count} seansów.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.SelectedMovieIds = movieIds;
            await PopulateViewBag();
            return View(marathon);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var marathon = await _context.Marathons
                .Include(m => m.Screenings).ThenInclude(s => s.Movie)
                .Include(m => m.Reservations)
                .FirstOrDefaultAsync(m => m.MarathonId == id);
            if (marathon == null) return NotFound();
            await PopulateViewBag();
            return View(marathon);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, string name, string? description, string? posterUrl, bool isActive, DateTime? publishDate, IFormFile? posterFile)
        {
            var marathon = await _context.Marathons.FindAsync(id);
            if (marathon == null) return NotFound();

            marathon.Name = name;
            marathon.Description = description;
            marathon.IsActive = isActive;
            marathon.PublishDate = publishDate;
            marathon.PosterUrl = (await _fileUpload.SavePosterAsync(posterFile)) ?? posterUrl;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Maraton zaktualizowany.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Delete(int id)
        {
            var marathon = await _context.Marathons
                .Include(m => m.Hall).ThenInclude(h => h.Cinema)
                .Include(m => m.Screenings).ThenInclude(s => s.Movie)
                .Include(m => m.Reservations)
                .FirstOrDefaultAsync(m => m.MarathonId == id);
            if (marathon == null) return NotFound();
            return View(marathon);
        }

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var marathon = await _context.Marathons
                .Include(m => m.Reservations)
                .FirstOrDefaultAsync(m => m.MarathonId == id);
            if (marathon == null) return NotFound();

            var hasActive = marathon.Reservations.Any(r => r.Status != ReservationStatus.Cancelled);
            if (hasActive)
            {
                TempData["Error"] = "Nie można usunąć maratonu z aktywnymi rezerwacjami.";
                return RedirectToAction(nameof(Delete), new { id });
            }

            _context.Marathons.Remove(marathon);
            await _context.SaveChangesAsync();
            TempData["Success"] = "Maraton usunięty.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(int id)
        {
            var marathon = await _context.Marathons.FindAsync(id);
            if (marathon == null) return NotFound();
            marathon.IsActive = !marathon.IsActive;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> GetHallSchedule(int hallId, string date)
        {
            if (!DateTime.TryParse(date, out var selectedDate))
                return Json(Array.Empty<object>());

            var screenings = await _context.Screenings
                .AsNoTracking()
                .Include(s => s.Movie)
                .Include(s => s.Marathon)
                .Where(s => s.HallId == hallId && s.StartTime.Date == selectedDate.Date)
                .OrderBy(s => s.StartTime)
                .ToListAsync();

            return Json(screenings.Select(s => new {
                title        = s.Movie?.Title ?? "?",
                start        = s.StartTime.ToString("HH:mm"),
                end          = s.StartTime.AddMinutes(s.Movie?.Duration ?? 0).ToString("HH:mm"),
                marathonName = s.Marathon?.Name
            }));
        }

        [HttpGet]
        public async Task<IActionResult> PreviewSchedule(int hallId, DateTime startTime, string movieIdsJson)
        {
            int[] movieIds;
            try { movieIds = System.Text.Json.JsonSerializer.Deserialize<int[]>(movieIdsJson) ?? Array.Empty<int>(); }
            catch { return Json(Array.Empty<object>()); }

            var movies = await _context.Movies
                .AsNoTracking()
                .Where(m => movieIds.Contains(m.MovieId))
                .ToDictionaryAsync(m => m.MovieId);

            var slot = startTime;
            var result = new List<object>();
            foreach (var mid in movieIds)
            {
                if (!movies.TryGetValue(mid, out var movie)) continue;
                result.Add(new
                {
                    title = movie.Title,
                    start = slot.ToString("HH:mm"),
                    end   = slot.AddMinutes(movie.Duration).ToString("HH:mm"),
                    endWithBuffer = slot.AddMinutes(movie.Duration + 20).ToString("HH:mm")
                });
                slot = slot.AddMinutes(movie.Duration + 20);
            }
            return Json(result);
        }

        private static List<Screening> BuildScreenings(Marathon marathon, List<int> movieIds, Dictionary<int, Movie> movies)
        {
            var screenings = new List<Screening>();
            var slot = marathon.StartTime;
            foreach (var mid in movieIds)
            {
                if (!movies.TryGetValue(mid, out var movie)) continue;
                screenings.Add(new Screening
                {
                    MovieId      = mid,
                    HallId       = marathon.HallId,
                    StartTime    = slot,
                    TicketPrice  = 0m,
                    AudioVersion = AudioVersion.Original
                });
                slot = slot.AddMinutes(movie.Duration + 20);
            }
            return screenings;
        }

        private async Task<string?> CheckCollisionsAsync(int hallId, List<Screening> candidates, Dictionary<int, Movie> movies)
        {
            var existing = await _context.Screenings
                .AsNoTracking()
                .Include(s => s.Movie)
                .Where(s => s.HallId == hallId && s.MarathonId == null)
                .ToListAsync();

            foreach (var c in candidates)
            {
                if (!movies.TryGetValue(c.MovieId, out var movie)) continue;
                var cEnd = c.StartTime.AddMinutes(movie.Duration + 20);

                bool hit = existing.Any(e =>
                {
                    var eEnd = e.StartTime.AddMinutes(e.Movie!.Duration + 20);
                    return e.StartTime < cEnd && eEnd > c.StartTime;
                });
                if (hit) return c.StartTime.ToString("HH:mm dd.MM.yyyy");
            }
            return null;
        }

        private async Task PopulateViewBag()
        {
            var halls = await _context.Halls.AsNoTracking().Include(h => h.Cinema)
                .OrderBy(h => h.Cinema.Name).ThenBy(h => h.Number).ToListAsync();
            ViewBag.Cinemas  = await _context.Cinemas.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
            ViewBag.HallsJson = System.Text.Json.JsonSerializer.Serialize(halls.Select(h => new
            {
                id       = h.HallId,
                cinemaId = h.CinemaId,
                label    = $"Sala {h.Number} ({h.Capacity} miejsc)"
            }));
            ViewBag.Movies = await _context.Movies.AsNoTracking().OrderBy(m => m.Title)
                .Select(m => new { m.MovieId, m.Title, m.Duration }).ToListAsync();
        }
    }
}
