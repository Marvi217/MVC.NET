using System.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using CinePlex.Data;
using CinePlex.Hubs;
using CinePlex.Infrastructure;
using CinePlex.Models;
using CinePlex.Services;

namespace CinePlex.Areas.User.Controllers
{
    [Area("User")]
    public class MarathonsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IEmailService _emailService;
        private readonly IHubContext<SeatHub> _seatHub;

        private const string PendingMarathonSessionKey = "PendingMarathonIds";

        public MarathonsController(ApplicationDbContext context, UserManager<AppUser> userManager,
            IEmailService emailService, IHubContext<SeatHub> seatHub)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
            _seatHub = seatHub;
        }

        public async Task<IActionResult> Index()
        {
            var now = DateTime.Now;
            var marathons = await _context.Marathons
                .Include(m => m.Hall).ThenInclude(h => h.Cinema)
                .Include(m => m.Screenings)
                .Where(m => (m.IsActive || (m.PublishDate != null && m.PublishDate <= now)) && m.StartTime > now)
                .OrderBy(m => m.StartTime)
                .ToListAsync();
            return View(marathons);
        }

        public async Task<IActionResult> Details(int id)
        {
            var now = DateTime.Now;
            var marathon = await _context.Marathons
                .Include(m => m.Hall).ThenInclude(h => h.Cinema)
                .Include(m => m.Screenings).ThenInclude(s => s.Movie)
                .Include(m => m.Reservations)
                .FirstOrDefaultAsync(m => m.MarathonId == id && (m.IsActive || (m.PublishDate != null && m.PublishDate <= now)) && m.StartTime > now);
            if (marathon == null) return NotFound();

            var takenSeats = marathon.Reservations
                .Where(r => r.Status != ReservationStatus.Cancelled)
                .Select(r => r.SeatNumber)
                .ToHashSet();

            ViewBag.TakenSeats = takenSeats;
            ViewBag.Layout = HallLayoutConfig.FromJson(marathon.Hall?.LayoutJson);
            return View(marathon);
        }

        public async Task<IActionResult> Checkout(int id, [FromQuery] List<int> seats)
        {
            if (seats == null || seats.Count == 0)
                return RedirectToAction(nameof(Details), new { id });

            var now = DateTime.Now;
            var marathon = await _context.Marathons
                .Include(m => m.Hall).ThenInclude(h => h.Cinema)
                .Include(m => m.Screenings).ThenInclude(s => s.Movie)
                .Include(m => m.Reservations)
                .FirstOrDefaultAsync(m => m.MarathonId == id && (m.IsActive || (m.PublishDate != null && m.PublishDate <= now)) && m.StartTime > now);
            if (marathon == null) return NotFound();

            var taken = marathon.Reservations
                .Where(r => r.Status != ReservationStatus.Cancelled)
                .Select(r => r.SeatNumber)
                .ToHashSet();

            var conflict = seats.FirstOrDefault(s => taken.Contains(s));
            if (conflict != 0)
            {
                TempData["Error"] = $"Miejsce {conflict} jest już zajęte.";
                return RedirectToAction(nameof(Details), new { id });
            }

            var layout = HallLayoutConfig.FromJson(marathon.Hall?.LayoutJson);
            var allTaken = new HashSet<int>(taken);
            allTaken.UnionWith(seats);
            if (SeatHelper.HasEdgeIsolatedSeat(marathon.Hall!, layout, allTaken))
            {
                TempData["Error"] = "Niedozwolony wybór — po wyborze tych miejsc w rzędzie pozostałoby dokładnie 1 wolne miejsce.";
                return RedirectToAction(nameof(Details), new { id });
            }

            ViewBag.Marathon = marathon;
            ViewBag.Seats = seats;
            ViewBag.SeatLabels = seats.Select(s => layout?.GetSeatLabel(s) ?? s.ToString()).ToList();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ProcessCheckout(int marathonId, List<int> seatNumbers,
            string? firstName, string? lastName, string? email)
        {
            if (seatNumbers == null || seatNumbers.Count == 0)
                return RedirectToAction(nameof(Details), new { id = marathonId });

            var now = DateTime.Now;
            var marathon = await _context.Marathons
                .Include(m => m.Hall)
                .Include(m => m.Reservations)
                .FirstOrDefaultAsync(m => m.MarathonId == marathonId && (m.IsActive || (m.PublishDate != null && m.PublishDate <= now)) && m.StartTime > now);
            if (marathon == null) return NotFound();

            var user = User.Identity?.IsAuthenticated == true
                ? await _userManager.GetUserAsync(User) : null;

            if (user == null && (string.IsNullOrWhiteSpace(firstName) ||
                string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(email)))
            {
                TempData["Error"] = "Uzupełnij imię, nazwisko i adres e-mail.";
                return RedirectToAction(nameof(Checkout), new { id = marathonId, seats = seatNumbers });
            }

            var layout = HallLayoutConfig.FromJson(marathon.Hall?.LayoutJson);
            var guestName  = user == null ? $"{firstName?.Trim()} {lastName?.Trim()}".Trim() : null;
            var guestEmail = user == null ? email?.Trim() : null;
            var now2 = DateTime.Now;
            var reservations = new List<Reservation>();

            foreach (var seatNumber in seatNumbers.Distinct())
            {
                var row = layout?.GetRowForSeat(seatNumber);
                reservations.Add(new Reservation
                {
                    ReservationType = ReservationType.Marathon,
                    MarathonId      = marathonId,
                    AppUserId       = user?.Id,
                    SeatNumber      = seatNumber,
                    SeatRow         = row?.Label ?? "?",
                    GuestName       = guestName,
                    GuestEmail      = guestEmail,
                    PricePaid       = marathon.Price,
                    ReservationCode = Guid.NewGuid().ToString("N")[..8].ToUpper(),
                    PurchaseDate    = now2,
                    Status          = ReservationStatus.Pending,
                    ExpiresAt       = now2.AddMinutes(15)
                });
            }

            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var freshMarathon = await _context.Marathons
                .Include(m => m.Reservations)
                .FirstAsync(m => m.MarathonId == marathonId);
            var taken = freshMarathon.Reservations
                .Where(r => r.Status != ReservationStatus.Cancelled && (r.ExpiresAt == null || r.ExpiresAt > now2))
                .Select(r => r.SeatNumber)
                .ToHashSet();

            var conflict = seatNumbers.FirstOrDefault(s => taken.Contains(s));
            if (conflict != 0)
            {
                await tx.RollbackAsync();
                TempData["Error"] = $"Miejsce {conflict} zostało właśnie zajęte. Wybierz inne.";
                return RedirectToAction(nameof(Details), new { id = marathonId });
            }

            var allTaken = new HashSet<int>(taken);
            allTaken.UnionWith(seatNumbers);
            if (SeatHelper.HasEdgeIsolatedSeat(marathon.Hall!, layout, allTaken))
            {
                await tx.RollbackAsync();
                TempData["Error"] = "Niedozwolony wybór — po wyborze tych miejsc w rzędzie pozostałoby dokładnie 1 wolne miejsce.";
                return RedirectToAction(nameof(Details), new { id = marathonId });
            }

            _context.Reservations.AddRange(reservations);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            var pendingIds = string.Join(",", reservations.Select(r => r.ReservationId));
            HttpContext.Session.SetString(PendingMarathonSessionKey, pendingIds);

            var marathonGroup = $"marathon-{marathonId}";
            foreach (var seatNumber in seatNumbers.Distinct())
                await _seatHub.Clients.Group(marathonGroup).SendAsync("SeatTaken", seatNumber);

            return RedirectToAction(nameof(Pay), new { marathonId, pendingIds });
        }

        public async Task<IActionResult> Pay(int marathonId, string pendingIds)
        {
            var ids = ParseIds(pendingIds);
            if (ids.Count == 0) return RedirectToAction(nameof(Index));

            if (!await OwnsPendingMarathonReservationsAsync(ids))
                return Forbid();

            var reservations = await _context.Reservations
                .Include(r => r.Marathon).ThenInclude(m => m.Hall).ThenInclude(h => h.Cinema)
                .Include(r => r.Marathon).ThenInclude(m => m.Screenings)
                .Where(r => ids.Contains(r.ReservationId))
                .OrderBy(r => r.SeatNumber)
                .ToListAsync();

            if (!reservations.Any()) return RedirectToAction(nameof(Index));

            var layout = HallLayoutConfig.FromJson(reservations[0].Marathon?.Hall?.LayoutJson);

            ViewBag.Reservations = reservations;
            ViewBag.Layout       = layout;
            ViewBag.PendingIds   = pendingIds;
            ViewBag.MarathonId   = marathonId;
            ViewBag.ExpiresAt    = reservations[0].ExpiresAt;
            ViewBag.TotalPrice   = reservations.Sum(r => r.PricePaid);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> SimulateSuccess(string pendingIds, int marathonId) =>
            ProcessSimulatedPayment(pendingIds, marathonId, success: true);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> SimulateFail(string pendingIds, int marathonId) =>
            ProcessSimulatedPayment(pendingIds, marathonId, success: false);

        private async Task<IActionResult> ProcessSimulatedPayment(string pendingIds, int marathonId, bool success)
        {
            var ids = ParseIds(pendingIds);
            if (ids.Count == 0) return RedirectToAction(nameof(Index));

            if (!await OwnsPendingMarathonReservationsAsync(ids))
                return Forbid();

            var reservations = await _context.Reservations
                .Where(r => ids.Contains(r.ReservationId))
                .ToListAsync();

            if (success)
            {
                reservations.ForEach(r => { r.Status = ReservationStatus.Confirmed; r.ExpiresAt = null; });
                await _context.SaveChangesAsync();
                HttpContext.Session.Remove(PendingMarathonSessionKey);

                var forEmail = await _context.Reservations
                    .AsNoTracking()
                    .Include(r => r.Marathon).ThenInclude(m => m.Hall).ThenInclude(h => h.Cinema)
                    .Where(r => ids.Contains(r.ReservationId))
                    .ToListAsync();

                var first = forEmail.FirstOrDefault();
                string recipientEmail = first?.GuestEmail ?? (await _userManager.GetUserAsync(User))?.Email ?? "";
                string recipientName  = first?.GuestName  ?? recipientEmail;

                if (!string.IsNullOrEmpty(recipientEmail))
                    await _emailService.SendMarathonConfirmedAsync(forEmail, recipientEmail, recipientName);

                TempData["MarathonBookingIds"] = pendingIds;
                return RedirectToAction(nameof(Confirmation), new { id = reservations[0].ReservationId });
            }

            reservations.ForEach(r => r.Status = ReservationStatus.Cancelled);
            await _context.SaveChangesAsync();
            HttpContext.Session.Remove(PendingMarathonSessionKey);

            var relGroup = $"marathon-{marathonId}";
            foreach (var r in reservations)
                await _seatHub.Clients.Group(relGroup).SendAsync("SeatReleased", r.SeatNumber);

            TempData["Error"] = "Płatność nieudana. Spróbuj ponownie.";
            return RedirectToAction(nameof(Details), new { id = marathonId });
        }

        private async Task<bool> OwnsPendingMarathonReservationsAsync(IReadOnlyList<int> ids)
        {
            if (ids.Count == 0) return false;

            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId != null)
            {
                var count = await _context.Reservations
                    .CountAsync(r => ids.Contains(r.ReservationId) && r.AppUserId == currentUserId);
                return count == ids.Count;
            }

            var sessionValue = HttpContext.Session.GetString(PendingMarathonSessionKey);
            if (string.IsNullOrEmpty(sessionValue)) return false;

            var allowed = sessionValue
                .Split(',')
                .Select(s => int.TryParse(s, out var n) ? n : 0)
                .ToHashSet();

            return ids.All(allowed.Contains);
        }

        private static List<int> ParseIds(string? csv) =>
            csv?.Split(',')
                .Select(s => int.TryParse(s.Trim(), out var n) ? n : 0)
                .Where(n => n > 0)
                .ToList() ?? new();

        public async Task<IActionResult> Confirmation(int id)
        {
            var ids = new List<int> { id };
            if (TempData["MarathonBookingIds"] is string stored)
                ids = stored.Split(',').Select(int.Parse).ToList();

            var reservations = await _context.Reservations
                .Include(r => r.Marathon).ThenInclude(m => m.Hall).ThenInclude(h => h.Cinema)
                .Include(r => r.Marathon).ThenInclude(m => m.Screenings).ThenInclude(s => s.Movie)
                .Include(r => r.AppUser)
                .Where(r => ids.Contains(r.ReservationId) && r.ReservationType == ReservationType.Marathon)
                .OrderBy(r => r.SeatNumber)
                .ToListAsync();

            if (!reservations.Any()) return NotFound();
            return View(reservations);
        }
    }
}
