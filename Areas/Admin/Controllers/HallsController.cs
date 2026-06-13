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
    public class HallsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public HallsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(int? cinemaId)
        {
            var query = _context.Halls.Include(h => h.Cinema).AsQueryable();
            if (cinemaId.HasValue) query = query.Where(h => h.CinemaId == cinemaId.Value);
            ViewBag.Cinemas  = await _context.Cinemas.OrderBy(c => c.Name).ToListAsync();
            ViewBag.CinemaId = cinemaId;
            return View(await query.OrderBy(h => h.Cinema!.Name).ThenBy(h => h.Number).ToListAsync());
        }

        public async Task<IActionResult> Details(int id)
        {
            var hall = await _context.Halls
                .Include(h => h.Cinema)
                .Include(h => h.Screenings).ThenInclude(s => s.Movie)
                .Include(h => h.Screenings).ThenInclude(s => s.Reservations)
                .FirstOrDefaultAsync(h => h.HallId == id);
            if (hall == null) return NotFound();
            return View(hall);
        }

        public async Task<IActionResult> Create()
        {
            ViewBag.Cinemas = new SelectList(await _context.Cinemas.OrderBy(c => c.Name).ToListAsync(), "CinemaId", "Name");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Hall hall, string? layoutJson)
        {
            if (!string.IsNullOrWhiteSpace(layoutJson))
            {
                hall.LayoutJson = layoutJson;
                var config = HallLayoutConfig.FromJson(layoutJson);
                if (config != null)
                {
                    hall.Capacity = config.TotalSeats;
                    ModelState.Remove(nameof(hall.Capacity));
                }
            }
            if (ModelState.IsValid)
            {
                _context.Halls.Add(hall);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Hall added.";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Cinemas = new SelectList(await _context.Cinemas.OrderBy(c => c.Name).ToListAsync(), "CinemaId", "Name");
            return View(hall);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var hall = await _context.Halls.FindAsync(id);
            if (hall == null) return NotFound();
            ViewBag.Cinemas = new SelectList(await _context.Cinemas.OrderBy(c => c.Name).ToListAsync(), "CinemaId", "Name", hall.CinemaId);
            ViewBag.LayoutConfig = HallLayoutConfig.FromJson(hall.LayoutJson);
            return View(hall);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Hall hall, string? layoutJson)
        {
            if (id != hall.HallId) return NotFound();
            if (!string.IsNullOrWhiteSpace(layoutJson))
            {
                hall.LayoutJson = layoutJson;
                var config = HallLayoutConfig.FromJson(layoutJson);
                if (config != null)
                {
                    hall.Capacity = config.TotalSeats;
                    ModelState.Remove(nameof(hall.Capacity));
                }
            }
            if (ModelState.IsValid)
            {
                _context.Update(hall);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Hall updated.";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Cinemas = new SelectList(await _context.Cinemas.OrderBy(c => c.Name).ToListAsync(), "CinemaId", "Name", hall.CinemaId);
            ViewBag.LayoutConfig = HallLayoutConfig.FromJson(layoutJson);
            return View(hall);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var hall = await _context.Halls.Include(h => h.Cinema).FirstOrDefaultAsync(h => h.HallId == id);
            if (hall == null) return NotFound();
            return View(hall);
        }

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var hall = await _context.Halls.FindAsync(id);
            if (hall != null)
            {
                _context.Halls.Remove(hall);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Hall deleted.";
            }
            return RedirectToAction(nameof(Index));
        }

    }
}
