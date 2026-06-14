using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using CinePlex.Data;
using CinePlex.Infrastructure;
using CinePlex.Models;
using CinePlex.Services;
using CinePlex.ViewModels;

namespace CinePlex.Areas.User.Controllers
{
    [Area("User")]

    public class ScreeningsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IEmailService _emailService;

        public ScreeningsController(ApplicationDbContext context, UserManager<AppUser> userManager, IEmailService emailService)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
        }

        public async Task<IActionResult> Index(int? cinemaId, int? movieId, DateTime? date, HallType? hallType = null)
        {
            if (!cinemaId.HasValue)
                cinemaId = HttpContext.Session.GetInt32("SelectedCinemaId");

            if (!cinemaId.HasValue)
            {
                var cinemaList = await _context.Cinemas
                    .AsNoTracking()
                    .Include(c => c.Halls)
                    .OrderBy(c => c.City)
                    .ToListAsync();
                ViewBag.MovieId  = movieId;
                ViewBag.HallType = hallType?.ToString();
                if (movieId.HasValue)
                {
                    var movie = await _context.Movies.FindAsync(movieId.Value);
                    ViewBag.MovieTitle  = movie?.Title;
                    ViewBag.MoviePoster = movie?.PosterUrl;
                }
                return View("CinemaPicker", cinemaList);
            }

            var selectedDate = date?.Date ?? DateTime.Today;
            var dayStrip = Enumerable.Range(0, 7).Select(i => DateTime.Today.AddDays(i)).ToList();

            var cinemas = await _context.Cinemas.AsNoTracking().OrderBy(c => c.Name).ToListAsync();
            var selectedCinema = cinemaId.HasValue
                ? cinemas.FirstOrDefault(c => c.CinemaId == cinemaId.Value)
                : null;

            if (selectedCinema != null)
            {
                HttpContext.Session.SetInt32("SelectedCinemaId", selectedCinema.CinemaId);
                HttpContext.Session.SetString("SelectedCinemaName", selectedCinema.Name);
                HttpContext.Session.SetString("SelectedCinemaCity", selectedCinema.City);
            }

            var now = DateTime.Now;
            var query = _context.Screenings
                .AsNoTracking()
                .Include(s => s.Movie).ThenInclude(m => m.Director)
                .Include(s => s.Hall).ThenInclude(h => h.Cinema)
                .Include(s => s.Reservations)
                .Where(s => s.StartTime.Date == selectedDate)
                .Where(s => s.MarathonId == null)
                .Where(s => s.IsPublished || (s.PublishDate != null && s.PublishDate <= now));

            if (cinemaId.HasValue)
                query = query.Where(s => s.Hall.CinemaId == cinemaId.Value);
            if (movieId.HasValue)
                query = query.Where(s => s.MovieId == movieId.Value);
            if (hallType.HasValue)
                query = query.Where(s => s.Hall.Type == hallType.Value);

            var screenings = await query.OrderBy(s => s.StartTime).ToListAsync();

            var groups = screenings
                .GroupBy(s => s.MovieId)
                .Select(g => new RepertoireMovieGroup
                {
                    Movie = g.First().Movie!,
                    HallGroups = g
                        .GroupBy(s => new { s.Hall!.Type, s.AudioVersion })
                        .OrderBy(hg => hg.Key.Type)
                        .Select(hg => new RepertoireHallGroup
                        {
                            HallTypeLabel = hg.Key.Type switch
                            {
                                HallType.IMAX   => "IMAX",
                                HallType.ThreeD => "3D",
                                HallType.FourD  => "4DX",
                                _               => "2D",
                            },
                            AudioLabel = hg.Key.AudioVersion switch
                            {
                                AudioVersion.Dubbed   => "DUBBING",
                                AudioVersion.Original => "WERSJA ORYGINALNA",
                                _                     => "NAPISY PL",
                            },
                            Slots = hg.Select(s => new RepertoireSlot
                            {
                                ScreeningId = s.ScreeningId,
                                StartTime   = s.StartTime,
                                SoldOut     = s.AvailableSeats == 0,
                            }).OrderBy(s => s.StartTime).ToList(),
                        }).ToList(),
                })
                .OrderBy(g => g.Movie.Title)
                .ToList();

            var vm = new RepertoireViewModel
            {
                Cinemas          = cinemas,
                SelectedCinema   = selectedCinema,
                SelectedDate     = selectedDate,
                DayStrip         = dayStrip,
                Movies           = await _context.Movies
                    .AsNoTracking()
                    .Where(m => m.Screenings.Any(s => s.StartTime.Date >= DateTime.Today))
                    .OrderBy(m => m.Title).ToListAsync(),
                SelectedMovieId  = movieId,
                SelectedHallType = hallType,
                Groups           = groups,
            };

            return View(vm);
        }

        public async Task<IActionResult> AvailableDays(int? year, int? month, int? cinemaId)
        {
            var now = DateTime.Now;
            var y = year ?? now.Year;
            var m = month ?? now.Month;
            var from = new DateTime(y, m, 1);
            var to = from.AddMonths(1);

            var query = _context.Screenings
                .AsNoTracking()
                .Where(s => s.StartTime >= from && s.StartTime < to && s.MarathonId == null)
                .Where(s => s.IsPublished || (s.PublishDate != null && s.PublishDate <= now));

            if (cinemaId.HasValue)
                query = query.Where(s => s.Hall.CinemaId == cinemaId.Value);

            var dates = await query
                .Select(s => s.StartTime.Date)
                .Distinct()
                .ToListAsync();

            return Json(dates.Select(d => d.ToString("yyyy-MM-dd")));
        }

        public async Task<IActionResult> Details(int id)
        {
            var screening = await _context.Screenings
                .AsNoTracking()
                .Include(s => s.Movie).ThenInclude(m => m.Director)
                .Include(s => s.Hall).ThenInclude(h => h.Cinema)
                .Include(s => s.Reservations)
                .FirstOrDefaultAsync(s => s.ScreeningId == id);

            if (screening == null) return NotFound();

            var now = DateTime.Now;
            ViewBag.TakenSeats = screening.Reservations
                .Where(r => r.Status == ReservationStatus.Confirmed ||
                           (r.Status == ReservationStatus.Pending && r.ExpiresAt > now))
                .Select(r => r.SeatNumber)
                .ToList();

            return View(screening);
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> Book(int screeningId, List<int> seatNumbers)
        {
            if (seatNumbers == null || !seatNumbers.Any())
                return RedirectToAction("Details", new { id = screeningId });

            var screening = await _context.Screenings
                .Include(s => s.Hall)
                .Include(s => s.Reservations)
                .FirstOrDefaultAsync(s => s.ScreeningId == screeningId);

            if (screening == null) return NotFound();

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (user.IsBlocked)
            {
                TempData["Error"] = "Your account is blocked.";
                return RedirectToAction("Details", new { id = screeningId });
            }

            var layoutConfig = HallLayoutConfig.FromJson(screening.Hall?.LayoutJson);
            var reservations = new List<Reservation>();
            var distinctSeats = seatNumbers.Distinct().ToList();

            foreach (var seatNumber in distinctSeats)
            {
                if (screening.Reservations.Any(r => r.SeatNumber == seatNumber && r.Status != ReservationStatus.Cancelled))
                {
                    TempData["Error"] = $"Miejsce {seatNumber} jest już zajęte.";
                    return RedirectToAction("Details", new { id = screeningId });
                }
                var row = layoutConfig?.GetRowForSeat(seatNumber);
                var pricePaid = screening.TicketPrice * (row?.PriceMultiplier ?? 1.0m);
                reservations.Add(new Reservation
                {
                    ScreeningId     = screeningId,
                    SeatNumber      = seatNumber,
                    AppUserId       = user.Id,
                    Status          = ReservationStatus.Confirmed,
                    PurchaseDate    = DateTime.Now,
                    ReservationCode = Guid.NewGuid().ToString("N")[..8].ToUpper(),
                    PricePaid       = pricePaid
                });
            }

            var existingTaken = screening.Reservations
                .Where(r => r.Status != ReservationStatus.Cancelled)
                .Select(r => r.SeatNumber)
                .ToHashSet();
            existingTaken.UnionWith(distinctSeats);

            if (SeatHelper.HasEdgeIsolatedSeat(screening.Hall!, layoutConfig, existingTaken))
            {
                TempData["Error"] = "Niedozwolony wybór — po wyborze tych miejsc w rzędzie pozostałoby dokładnie 1 wolne miejsce.";
                return RedirectToAction("Details", new { id = screeningId });
            }

            _context.Reservations.AddRange(reservations);
            await _context.SaveChangesAsync();

            var reservationIds = reservations.Select(r => r.ReservationId).ToList();
            var forEmail = await _context.Reservations
                .AsNoTracking()
                .Include(r => r.Screening).ThenInclude(s => s.Movie)
                .Include(r => r.Screening).ThenInclude(s => s.Hall).ThenInclude(h => h.Cinema)
                .Where(r => reservationIds.Contains(r.ReservationId))
                .ToListAsync();
            if (forEmail.Count > 0 && !string.IsNullOrEmpty(user.Email))
                await _emailService.SendReservationConfirmedAsync(forEmail, user.Email, user.Email);

            TempData["BookingIds"] = string.Join(",", reservations.Select(r => r.ReservationId));
            TempData["Success"] = $"Rezerwacja potwierdzona! Miejsc: {reservations.Count}";
            return RedirectToAction("Confirmation", new { id = reservations[0].ReservationId });
        }

        public async Task<IActionResult> Confirmation(int id)
        {
            var ids = new List<int> { id };
            if (TempData["BookingIds"] is string stored)
                ids = stored.Split(',').Select(int.Parse).ToList();

            var reservations = await _context.Reservations
                .Include(r => r.Screening).ThenInclude(s => s.Movie)
                .Include(r => r.Screening).ThenInclude(s => s.Hall).ThenInclude(h => h.Cinema)
                .Where(r => ids.Contains(r.ReservationId))
                .OrderBy(r => r.SeatNumber)
                .ToListAsync();

            if (!reservations.Any()) return NotFound();
            return View(reservations);
        }
    }
}
