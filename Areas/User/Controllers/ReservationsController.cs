using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using CinePlex.Data;
using CinePlex.Models;
using CinePlex.ViewModels;

namespace CinePlex.Areas.User.Controllers
{
    [Authorize]
    [Area("User")]
    public class ReservationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public ReservationsController(ApplicationDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var all = await _context.Reservations
                .AsNoTracking()
                .Include(r => r.Screening).ThenInclude(s => s.Movie)
                .Include(r => r.Screening).ThenInclude(s => s.Hall).ThenInclude(h => h.Cinema)
                .Include(r => r.Marathon).ThenInclude(m => m!.Hall).ThenInclude(h => h.Cinema)
                .Include(r => r.Marathon).ThenInclude(m => m!.Screenings).ThenInclude(s => s.Movie)
                .Where(r => r.AppUserId == user.Id)
                .ToListAsync();

            var items = all
                .Select(r => new BookingHistoryItem
                {
                    Kind = r.ReservationType == ReservationType.Marathon
                        ? BookingHistoryItem.BookingKind.Marathon
                        : BookingHistoryItem.BookingKind.Screening,
                    EventDate = r.ReservationType == ReservationType.Marathon
                        ? r.Marathon!.StartTime
                        : r.Screening!.StartTime,
                    PurchaseDate = r.PurchaseDate,
                    ReservationCode = r.ReservationCode,
                    PricePaid = r.PricePaid,
                    Status = r.Status,
                    Reservation = r
                })
                .OrderByDescending(i => i.EventDate)
                .ToList();

            return View(items);
        }

        public async Task<IActionResult> Details(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var reservation = await _context.Reservations
                .AsNoTracking()
                .Include(r => r.Screening).ThenInclude(s => s.Movie)
                .Include(r => r.Screening).ThenInclude(s => s.Hall).ThenInclude(h => h.Cinema)
                .FirstOrDefaultAsync(r => r.ReservationId == id);

            if (reservation == null) return NotFound();
            if (reservation.AppUserId != user?.Id) return Forbid();

            return View(reservation);
        }

        [HttpPost]
        public async Task<IActionResult> Cancel(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            var reservation = await _context.Reservations
                .Include(r => r.Screening)
                .FirstOrDefaultAsync(r => r.ReservationId == id);

            if (reservation == null) return NotFound();
            if (reservation.AppUserId != user?.Id) return Forbid();
            if (reservation.ReservationType == ReservationType.Marathon) return NotFound();

            if (reservation.Screening!.StartTime <= DateTime.Now.AddHours(1))
            {
                TempData["Error"] = "Cannot cancel a reservation less than 1 hour before the screening.";
                return RedirectToAction("Details", new { id });
            }

            reservation.Status = ReservationStatus.Cancelled;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Reservation has been cancelled.";
            return RedirectToAction("Index");
        }

        [AllowAnonymous]
        public IActionResult Check() => View();

        [AllowAnonymous]
        [HttpPost]
        public async Task<IActionResult> Check(string code) => await FindAndShowReservation(code);

        [AllowAnonymous]
        [HttpGet]
        public async Task<IActionResult> CheckResult(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                return RedirectToAction("Check");
            return await FindAndShowReservation(code);
        }

        private async Task<IActionResult> FindAndShowReservation(string code)
        {
            var reservation = await _context.Reservations
                .AsNoTracking()
                .Include(r => r.Screening).ThenInclude(s => s.Movie)
                .Include(r => r.Screening).ThenInclude(s => s.Hall).ThenInclude(h => h.Cinema)
                .Include(r => r.AppUser)
                .FirstOrDefaultAsync(r => r.ReservationCode == code.ToUpper());

            if (reservation == null)
            {
                ViewBag.Error = "No reservation found with this code.";
                return View("Check");
            }

            return View("CheckResult", reservation);
        }
    }
}
