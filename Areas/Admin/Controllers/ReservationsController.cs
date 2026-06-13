using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CinePlex.Data;
using CinePlex.Models;

namespace CinePlex.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ReservationsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReservationsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(ReservationStatus? status, string? search, int page = 1)
        {
            const int pageSize = 20;
            var query = _context.Reservations
                .AsNoTracking()
                .Include(r => r.Screening).ThenInclude(s => s.Movie)
                .Include(r => r.Screening).ThenInclude(s => s.Hall).ThenInclude(h => h.Cinema)
                .Include(r => r.AppUser)
                .Where(r => r.ReservationType == CinePlex.Models.ReservationType.Screening)
                .AsQueryable();
            if (status.HasValue) query = query.Where(r => r.Status == status.Value);
            if (!string.IsNullOrEmpty(search)) query = query.Where(r => r.ReservationCode.Contains(search) || (r.AppUser != null && r.AppUser.Email!.Contains(search)));
            var total = await query.CountAsync();
            ViewBag.Status = status; ViewBag.Search = search;
            ViewBag.Page = page; ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
            ViewBag.Statuses = Enum.GetValues<ReservationStatus>();
            return View(await query.OrderByDescending(r => r.PurchaseDate).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync());
        }

        public async Task<IActionResult> Details(int id)
        {
            var reservation = await _context.Reservations
                .AsNoTracking()
                .Include(r => r.Screening).ThenInclude(s => s.Movie)
                .Include(r => r.Screening).ThenInclude(s => s.Hall).ThenInclude(h => h.Cinema)
                .Include(r => r.AppUser)
                .FirstOrDefaultAsync(r => r.ReservationId == id);
            if (reservation == null) return NotFound();
            return View(reservation);
        }

        [HttpPost]
        public async Task<IActionResult> ChangeStatus(int id, ReservationStatus status)
        {
            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation == null) return NotFound();
            reservation.Status = status;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Status updated.";
            return RedirectToAction("Details", new { id });
        }

    }
}
