using System.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using CinePlex.Data;
using CinePlex.Hubs;
using CinePlex.Models;
using CinePlex.Services;
using CinePlex.ViewModels;

namespace CinePlex.Areas.User.Controllers
{
    [Area("User")]
    public class CheckoutController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;
        private readonly IEmailService _emailService;
        private readonly IHubContext<SeatHub> _seatHub;

        private const string PendingReservationSessionKey = "PendingCheckoutIds";

        public CheckoutController(ApplicationDbContext context, UserManager<AppUser> userManager,
            IEmailService emailService, IHubContext<SeatHub> seatHub)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
            _seatHub = seatHub;
        }

        public async Task<IActionResult> Index(int screeningId, string seats, string? cancelPendingIds = null)
        {
            if (!string.IsNullOrEmpty(cancelPendingIds))
            {
                var ids = ParseIds(cancelPendingIds);
                if (ids.Count > 0 && await OwnsPendingReservationsAsync(ids))
                {
                    var toCancel = await _context.Reservations
                        .Where(r => ids.Contains(r.ReservationId) && r.Status == ReservationStatus.Pending)
                        .ToListAsync();
                    toCancel.ForEach(r => r.Status = ReservationStatus.Cancelled);
                    await _context.SaveChangesAsync();
                    HttpContext.Session.Remove(PendingReservationSessionKey);
                    var group = $"screening-{screeningId}";
                    foreach (var r in toCancel)
                        await _seatHub.Clients.Group(group).SendAsync("SeatReleased", r.SeatNumber);
                }
            }

            var seatList = ParseSeats(seats);
            if (!seatList.Any())
                return RedirectToAction("Index", "Screenings");

            var screening = await _context.Screenings
                .Include(s => s.Movie)
                .Include(s => s.Hall).ThenInclude(h => h.Cinema)
                .FirstOrDefaultAsync(s => s.ScreeningId == screeningId);
            if (screening == null) return NotFound();

            var taken = await TakenSeats(screeningId);
            if (seatList.Intersect(taken).Any())
            {
                TempData["Error"] = "Niektóre miejsca zostały właśnie zajęte. Wybierz inne.";
                return RedirectToAction("Details", "Screenings", new { id = screeningId });
            }

            var layout = HallLayoutConfig.FromJson(screening.Hall?.LayoutJson);
            var seatDetails = seatList.Select(n => new SeatDetail
            {
                SeatNumber = n,
                Label = layout?.GetSeatLabel(n) ?? n.ToString(),
                Price = screening.TicketPrice * (layout?.GetRowForSeat(n)?.PriceMultiplier ?? 1.0m)
            }).ToList();

            ViewBag.Screening    = screening;
            ViewBag.SeatDetails  = seatDetails;
            ViewBag.TotalPrice   = seatDetails.Sum(s => s.Price);
            ViewBag.PaymentError = TempData["PaymentError"] as string;

            var vm = new CheckoutViewModel { ScreeningId = screeningId, SeatNumbers = seatList };
            return View(vm);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Process(CheckoutViewModel vm)
        {
            var screening = await _context.Screenings
                .Include(s => s.Movie)
                .Include(s => s.Hall).ThenInclude(h => h.Cinema)
                .FirstOrDefaultAsync(s => s.ScreeningId == vm.ScreeningId);
            if (screening == null) return NotFound();

            var user = User.Identity?.IsAuthenticated == true
                ? await _userManager.GetUserAsync(User) : null;

            if (user == null && (
                string.IsNullOrWhiteSpace(vm.FirstName) ||
                string.IsNullOrWhiteSpace(vm.LastName) ||
                string.IsNullOrWhiteSpace(vm.Email)))
            {
                TempData["Error"] = "Uzupełnij imię, nazwisko i adres e-mail.";
                return RedirectToAction("Index", new { screeningId = vm.ScreeningId, seats = string.Join(",", vm.SeatNumbers) });
            }

            var layout = HallLayoutConfig.FromJson(screening.Hall?.LayoutJson);
            var now = DateTime.Now;
            var guestName = user == null
                ? $"{vm.FirstName?.Trim()} {vm.LastName?.Trim()}".Trim()
                : null;

            var reservations = vm.SeatNumbers.Select(seatNum =>
            {
                var row = layout?.GetRowForSeat(seatNum);
                return new Reservation
                {
                    ScreeningId     = vm.ScreeningId,
                    SeatNumber      = seatNum,
                    AppUserId       = user?.Id,
                    GuestName       = guestName,
                    GuestEmail      = user == null ? vm.Email?.Trim() : null,
                    GuestPhone      = user == null ? vm.Phone?.Trim() : null,
                    Status          = ReservationStatus.Pending,
                    ExpiresAt       = now.AddMinutes(15),
                    PurchaseDate    = now,
                    ReservationCode = Guid.NewGuid().ToString("N")[..8].ToUpper(),
                    PricePaid       = screening.TicketPrice * (row?.PriceMultiplier ?? 1.0m)
                };
            }).ToList();

            await using var tx = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);
            var taken = await TakenSeats(vm.ScreeningId);
            if (vm.SeatNumbers.Intersect(taken).Any())
            {
                await tx.RollbackAsync();
                TempData["Error"] = "Niektóre miejsca zostały właśnie zajęte. Wybierz inne.";
                return RedirectToAction("Details", "Screenings", new { id = vm.ScreeningId });
            }

            _context.Reservations.AddRange(reservations);
            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            var pendingIds = string.Join(",", reservations.Select(r => r.ReservationId));
            HttpContext.Session.SetString(PendingReservationSessionKey, pendingIds);

            var seatGroup = $"screening-{vm.ScreeningId}";
            foreach (var seatNum in vm.SeatNumbers)
                await _seatHub.Clients.Group(seatGroup).SendAsync("SeatTaken", seatNum);

            if (user == null)
            {
                TempData["GuestName"]  = guestName;
                TempData["GuestEmail"] = vm.Email;
            }

            var seats = string.Join(",", vm.SeatNumbers);
            return RedirectToAction("Pay", new { screeningId = vm.ScreeningId, seats, pendingIds });
        }

        public async Task<IActionResult> Pay(int screeningId, string seats, string pendingIds)
        {
            var ids = ParseIds(pendingIds);
            if (ids.Count == 0) return RedirectToAction("Index", "Screenings");

            if (!await OwnsPendingReservationsAsync(ids))
                return Forbid();

            var reservations = await _context.Reservations
                .Include(r => r.Screening).ThenInclude(s => s.Movie)
                .Include(r => r.Screening).ThenInclude(s => s.Hall).ThenInclude(h => h.Cinema)
                .Where(r => ids.Contains(r.ReservationId))
                .OrderBy(r => r.SeatNumber)
                .ToListAsync();

            if (!reservations.Any())
                return RedirectToAction("Index", "Screenings");

            var layout = HallLayoutConfig.FromJson(reservations[0].Screening.Hall?.LayoutJson);

            ViewBag.Reservations = reservations;
            ViewBag.Layout       = layout;
            ViewBag.PendingIds   = pendingIds;
            ViewBag.ScreeningId  = screeningId;
            ViewBag.Seats        = seats;
            ViewBag.ExpiresAt    = reservations[0].ExpiresAt;
            ViewBag.TotalPrice   = reservations.Sum(r => r.PricePaid);
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> SimulateSuccess(string pendingIds, int screeningId, string seats) =>
            ProcessSimulatedPayment(pendingIds, screeningId, seats, success: true);

        [HttpPost]
        [ValidateAntiForgeryToken]
        public Task<IActionResult> SimulateFail(string pendingIds, int screeningId, string seats) =>
            ProcessSimulatedPayment(pendingIds, screeningId, seats, success: false);

        private async Task<IActionResult> ProcessSimulatedPayment(
            string pendingIds, int screeningId, string seats, bool success)
        {
            var ids = ParseIds(pendingIds);
            if (ids.Count == 0) return RedirectToAction("Index", "Screenings");

            if (!await OwnsPendingReservationsAsync(ids))
                return Forbid();

            var reservations = await _context.Reservations
                .Where(r => ids.Contains(r.ReservationId))
                .ToListAsync();

            if (success)
            {
                reservations.ForEach(r => { r.Status = ReservationStatus.Confirmed; r.ExpiresAt = null; });
                await _context.SaveChangesAsync();
                HttpContext.Session.Remove(PendingReservationSessionKey);
                TempData["BookingIds"] = pendingIds;

                var forEmail = await _context.Reservations
                    .AsNoTracking()
                    .Include(r => r.Screening).ThenInclude(s => s.Movie)
                    .Include(r => r.Screening).ThenInclude(s => s.Hall).ThenInclude(h => h.Cinema)
                    .Where(r => ids.Contains(r.ReservationId))
                    .ToListAsync();

                string recipientEmail, recipientName;
                if (forEmail.Count > 0 && forEmail[0].GuestEmail != null)
                {
                    recipientEmail = forEmail[0].GuestEmail!;
                    recipientName  = forEmail[0].GuestName ?? recipientEmail;
                }
                else
                {
                    var user = await _userManager.GetUserAsync(User);
                    recipientEmail = user?.Email ?? "";
                    recipientName  = recipientEmail;
                }

                if (!string.IsNullOrEmpty(recipientEmail))
                    await _emailService.SendReservationConfirmedAsync(forEmail, recipientEmail, recipientName);

                return RedirectToAction("Confirmation", "Screenings", new { id = reservations[0].ReservationId });
            }

            TempData["PaymentError"] = "Płatność nieudana. Ponów próbę lub zmień metodę płatności.";
            return RedirectToAction("Index", new { screeningId, seats, cancelPendingIds = pendingIds });
        }

        private async Task<bool> OwnsPendingReservationsAsync(IReadOnlyList<int> ids)
        {
            if (ids.Count == 0) return false;

            var currentUserId = _userManager.GetUserId(User);
            if (currentUserId != null)
            {
                var ownedCount = await _context.Reservations
                    .CountAsync(r => ids.Contains(r.ReservationId) && r.AppUserId == currentUserId);
                return ownedCount == ids.Count;
            }

            var sessionValue = HttpContext.Session.GetString(PendingReservationSessionKey);
            if (string.IsNullOrEmpty(sessionValue)) return false;

            var allowed = sessionValue
                .Split(',')
                .Select(s => int.TryParse(s, out var n) ? n : 0)
                .ToHashSet();

            return ids.All(allowed.Contains);
        }

        private async Task<HashSet<int>> TakenSeats(int screeningId)
        {
            var now = DateTime.Now;
            return (await _context.Reservations
                .Where(r => r.ScreeningId == screeningId &&
                           (r.Status == ReservationStatus.Confirmed ||
                            (r.Status == ReservationStatus.Pending && r.ExpiresAt > now)))
                .Select(r => r.SeatNumber)
                .ToListAsync()).ToHashSet();
        }

        private static List<int> ParseIds(string? csv) =>
            csv?.Split(',')
                .Select(s => int.TryParse(s.Trim(), out var n) ? n : 0)
                .Where(n => n > 0)
                .ToList() ?? new();

        private static List<int> ParseSeats(string? seats) => ParseIds(seats);
    }
}
