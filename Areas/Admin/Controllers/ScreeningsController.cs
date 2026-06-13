using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CinePlex.Data;
using CinePlex.Models;

namespace CinePlex.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ScreeningsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ScreeningsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int? cinemaId, string? date)
        {
            var pivot = date != null && DateTime.TryParse(date, out var parsed) ? parsed.Date : DateTime.Today;
            int dow = (int)pivot.DayOfWeek;
            var weekStart = pivot.AddDays(dow == 0 ? -6 : -(dow - 1));
            var weekEnd   = weekStart.AddDays(7);

            var query = _context.Screenings
                .AsNoTracking()
                .Include(s => s.Movie)
                .Include(s => s.Hall).ThenInclude(h => h.Cinema)
                .Include(s => s.Reservations)
                .Include(s => s.Marathon)
                .Where(s => s.StartTime >= weekStart && s.StartTime < weekEnd)
                .AsQueryable();

            if (cinemaId.HasValue) query = query.Where(s => s.Hall.CinemaId == cinemaId.Value);

            ViewBag.Cinemas   = await _context.Cinemas.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
            ViewBag.CinemaId  = cinemaId;
            ViewBag.WeekStart = weekStart;
            ViewBag.WeekEnd   = weekEnd;
            return View(await query.OrderBy(s => s.StartTime).ThenBy(s => s.Hall.Number).ToListAsync());
        }

        public async Task<IActionResult> Details(int id)
        {
            var screening = await _context.Screenings
                .Include(s => s.Movie).Include(s => s.Hall).ThenInclude(h => h.Cinema)
                .Include(s => s.Reservations).ThenInclude(r => r.AppUser)
                .FirstOrDefaultAsync(s => s.ScreeningId == id);
            if (screening == null) return NotFound();
            return View(screening);
        }

        private static string HallTypeLabel(HallType t) => t switch
        {
            HallType.ThreeD => "3D",
            HallType.IMAX   => "IMAX",
            HallType.FourD  => "4DX",
            _               => "Standard"
        };

        private async Task PopulateScreeningViewBag(int? selectedMovieId = null, int? selectedHallId = null)
        {
            ViewBag.Movies  = new SelectList(await _context.Movies.AsNoTracking().OrderBy(m => m.Title).ToListAsync(), "MovieId", "Title", selectedMovieId);
            ViewBag.Cinemas = new SelectList(await _context.Cinemas.AsNoTracking().OrderBy(c => c.Name).ToListAsync(), "CinemaId", "Name");
            var halls = await _context.Halls.AsNoTracking().Include(h => h.Cinema)
                .OrderBy(h => h.Cinema.Name).ThenBy(h => h.Number).ToListAsync();
            ViewBag.HallsJson = System.Text.Json.JsonSerializer.Serialize(halls.Select(h => new
            {
                id       = h.HallId,
                cinemaId = h.CinemaId,
                label    = $"Sala {h.Number} - {HallTypeLabel(h.Type)}, {h.Capacity} miejsc"
            }));
            ViewBag.SelectedHallId = selectedHallId;
            ViewBag.SelectedCinemaId = selectedHallId.HasValue
                ? halls.FirstOrDefault(h => h.HallId == selectedHallId)?.CinemaId
                : (int?)null;
        }

        private async Task<Screening?> FindCollisionAsync(int hallId, DateTime startTime, int durationMinutes, int excludeId = 0)
        {
            var newEnd = startTime.AddMinutes(durationMinutes + 20);
            var others = await _context.Screenings
                .AsNoTracking()
                .Include(s => s.Movie)
                .Where(s => s.HallId == hallId && s.ScreeningId != excludeId)
                .ToListAsync();
            return others.FirstOrDefault(s =>
            {
                var existEnd = s.StartTime.AddMinutes(s.Movie.Duration + 20);
                return s.StartTime < newEnd && existEnd > startTime;
            });
        }

        public async Task<IActionResult> Create()
        {
            await PopulateScreeningViewBag();
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> AvailableTimes(int hallId, DateTime date, int movieId, int excludeId = 0)
        {
            var movie = await _context.Movies.FindAsync(movieId);
            if (movie == null) return Json(Array.Empty<string>());

            var dayStart = date.Date;
            var dayEnd   = dayStart.AddDays(1);

            var existing = await _context.Screenings
                .AsNoTracking()
                .Include(s => s.Movie)
                .Where(s => s.HallId == hallId && s.ScreeningId != excludeId
                         && s.StartTime >= dayStart && s.StartTime < dayEnd)
                .ToListAsync();

            var available = new List<string>();
            for (int h = 8; h <= 23; h++)
                for (int m = 0; m < 60; m += 15)
                {
                    var slot    = dayStart.AddHours(h).AddMinutes(m);
                    var slotEnd = slot.AddMinutes(movie.Duration + 20);
                    bool hit = existing.Any(s =>
                    {
                        var sEnd = s.StartTime.AddMinutes(s.Movie!.Duration + 20);
                        return s.StartTime < slotEnd && sEnd > slot;
                    });
                    if (!hit) available.Add($"{h:D2}:{m:D2}");
                }

            return Json(available);
        }

        [HttpGet]
        public async Task<IActionResult> HallSchedule(int hallId, DateTime? date)
        {
            DateTime from, until;
            if (date.HasValue)
            {
                from  = date.Value.Date;
                until = from.AddDays(1);
            }
            else
            {
                from  = DateTime.Today;
                until = from.AddDays(7);
            }
            var items = await _context.Screenings
                .AsNoTracking()
                .Include(s => s.Movie)
                .Where(s => s.HallId == hallId && s.StartTime >= from && s.StartTime < until)
                .OrderBy(s => s.StartTime)
                .ToListAsync();
            return Json(items.Select(s => new
            {
                date          = s.StartTime.ToString("dd.MM.yyyy"),
                start         = s.StartTime.ToString("HH:mm"),
                endWithBuffer = s.StartTime.AddMinutes(s.Movie!.Duration + 20).ToString("HH:mm"),
                movie         = s.Movie.Title,
                duration      = s.Movie.Duration,
                screeningId   = s.ScreeningId
            }));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Screening screening)
        {
            if (screening.StartTime < DateTime.Today.AddDays(7))
                ModelState.AddModelError("StartTime", "Seans musi być zaplanowany co najmniej 7 dni do przodu.");
            if (ModelState.IsValid)
            {
                var movie = await _context.Movies.FindAsync(screening.MovieId);
                var collision = movie != null ? await FindCollisionAsync(screening.HallId, screening.StartTime, movie.Duration) : null;
                if (collision != null)
                    ModelState.AddModelError("", $"Kolizja z seansem \"{collision.Movie?.Title}\" o {collision.StartTime:dd.MM.yyyy HH:mm} (bufor do {collision.StartTime.AddMinutes(collision.Movie!.Duration + 20):HH:mm}).");
                else
                {
                    var hall = await _context.Halls.FindAsync(screening.HallId);
                    var pricing = hall != null ? await _context.HallTypePricings.FirstOrDefaultAsync(p => p.HallType == hall.Type) : null;
                    screening.TicketPrice = pricing?.DefaultPrice ?? 25m;
                    _context.Screenings.Add(screening);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Screening added.";
                    return RedirectToAction(nameof(Index));
                }
            }
            await PopulateScreeningViewBag(screening.MovieId, screening.HallId);
            return View(screening);
        }

        public async Task<IActionResult> BulkCreate()
        {
            await PopulateScreeningViewBag();
            var movies = await _context.Movies.ToListAsync();
            ViewBag.MovieDurationsJson = System.Text.Json.JsonSerializer.Serialize(
                movies.ToDictionary(m => m.MovieId.ToString(), m => m.Duration));
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkCreate(
            int movieId, List<int> hallIds,
            DateTime dateFrom, DateTime dateTo,
            List<string> times, List<int> audios, List<int>? excludeDays,
            bool isPublished = false, DateTime? publishDate = null)
        {
            var movie = await _context.Movies.FindAsync(movieId);
            if (movie == null) { TempData["Error"] = "Nie znaleziono filmu."; return RedirectToAction(nameof(BulkCreate)); }
            if (dateFrom.Date < DateTime.Today.AddDays(7))
            {
                TempData["Error"] = "Data 'Od' musi być co najmniej 7 dni do przodu.";
                await PopulateScreeningViewBag(movieId, null);
                var ms = await _context.Movies.ToListAsync();
                ViewBag.MovieDurationsJson = System.Text.Json.JsonSerializer.Serialize(ms.ToDictionary(m => m.MovieId.ToString(), m => m.Duration));
                return View();
            }

            var uniqueHallIds = hallIds.Distinct().ToList();
            var hallsForBulk = await _context.Halls.Where(h => uniqueHallIds.Contains(h.HallId)).ToListAsync();
            var pricings = await _context.HallTypePricings.ToListAsync();
            var hallPriceMap = hallsForBulk.ToDictionary(
                h => h.HallId,
                h => pricings.FirstOrDefault(p => p.HallType == h.Type)?.DefaultPrice ?? 25m);

            var candidates = new List<Screening>();
            for (var d = dateFrom.Date; d <= dateTo.Date; d = d.AddDays(1))
            {
                if (excludeDays != null && excludeDays.Contains((int)d.DayOfWeek)) continue;
                for (int i = 0; i < times.Count; i++)
                {
                    if (!TimeSpan.TryParse(times[i], out var ts)) continue;
                    var hid = hallIds.Count > i ? hallIds[i] : 0;
                    if (hid == 0) continue;
                    var av = audios.Count > i ? (AudioVersion)audios[i] : AudioVersion.Dubbed;
                    hallPriceMap.TryGetValue(hid, out var price);
                    candidates.Add(new Screening
                    {
                        MovieId      = movieId,
                        HallId       = hid,
                        TicketPrice  = price == 0 ? 25m : price,
                        AudioVersion = av,
                        StartTime    = d.Add(ts),
                        IsPublished  = isPublished,
                        PublishDate  = publishDate
                    });
                }
            }

            var relevantHallIds = candidates.Select(c => c.HallId).Distinct().ToList();
            var existingInDb = await _context.Screenings
                .Include(s => s.Movie)
                .Where(s => relevantHallIds.Contains(s.HallId))
                .ToListAsync();

            var accepted = new List<Screening>();
            int skipped = 0;

            foreach (var s in candidates)
            {
                var sEnd = s.StartTime.AddMinutes(movie.Duration + 20);

                bool collidesDb = existingInDb.Any(e =>
                {
                    if (e.HallId != s.HallId) return false;
                    var eEnd = e.StartTime.AddMinutes(e.Movie.Duration + 20);
                    return e.StartTime < sEnd && eEnd > s.StartTime;
                });

                bool collidesBatch = accepted.Any(a =>
                {
                    if (a.HallId != s.HallId) return false;
                    var aEnd = a.StartTime.AddMinutes(movie.Duration + 20);
                    return a.StartTime < sEnd && aEnd > s.StartTime;
                });

                if (collidesDb || collidesBatch) skipped++;
                else accepted.Add(s);
            }

            if (accepted.Count > 0)
            {
                _context.Screenings.AddRange(accepted);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Dodano {accepted.Count} seansów." +
                    (skipped > 0 ? $" Pominięto {skipped} kolidujących." : "");
                return RedirectToAction(nameof(Index));
            }
            TempData["Error"] = "Brak seansów do dodania - wszystkie kolidują lub sprawdź daty i godziny.";
            await PopulateScreeningViewBag(movieId, null);
            return View();
        }

        public async Task<IActionResult> Edit(int id)
        {
            var screening = await _context.Screenings.FindAsync(id);
            if (screening == null) return NotFound();
            if (screening.StartTime < DateTime.Today.AddDays(7))
            {
                TempData["Error"] = "Można edytować tylko seanse zaplanowane co najmniej 7 dni do przodu.";
                return RedirectToAction(nameof(Index));
            }
            await PopulateScreeningViewBag(screening.MovieId, screening.HallId);
            return View(screening);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Screening screening)
        {
            if (id != screening.ScreeningId) return NotFound();
            if (screening.StartTime < DateTime.Today.AddDays(7))
                ModelState.AddModelError("StartTime", "Seans musi być zaplanowany co najmniej 7 dni do przodu.");
            if (ModelState.IsValid)
            {
                var movie = await _context.Movies.FindAsync(screening.MovieId);
                var collision = movie != null ? await FindCollisionAsync(screening.HallId, screening.StartTime, movie.Duration, screening.ScreeningId) : null;
                if (collision != null)
                    ModelState.AddModelError("", $"Kolizja z seansem \"{collision.Movie?.Title}\" o {collision.StartTime:dd.MM.yyyy HH:mm} (bufor do {collision.StartTime.AddMinutes(collision.Movie!.Duration + 20):HH:mm}).");
                else
                {
                    var hall = await _context.Halls.FindAsync(screening.HallId);
                    var pricing = hall != null ? await _context.HallTypePricings.FirstOrDefaultAsync(p => p.HallType == hall.Type) : null;
                    screening.TicketPrice = pricing?.DefaultPrice ?? 25m;
                    _context.Update(screening);
                    await _context.SaveChangesAsync();
                    TempData["Success"] = "Screening updated.";
                    return RedirectToAction(nameof(Index));
                }
            }
            await PopulateScreeningViewBag(screening.MovieId, screening.HallId);
            return View(screening);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var screening = await _context.Screenings
                .Include(s => s.Movie).Include(s => s.Hall).ThenInclude(h => h.Cinema)
                .FirstOrDefaultAsync(s => s.ScreeningId == id);
            if (screening == null) return NotFound();
            return View(screening);
        }

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var screening = await _context.Screenings.FindAsync(id);
            if (screening != null)
            {
                _context.Screenings.Remove(screening);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Screening deleted.";
            }
            return RedirectToAction(nameof(Index));
        }

    }
}
