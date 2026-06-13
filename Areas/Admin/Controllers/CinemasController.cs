using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CinePlex.Data;
using CinePlex.Models;

namespace CinePlex.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class CinemasController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CinemasController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? city)
        {
            var query = _context.Cinemas.Include(c => c.Halls).AsQueryable();
            if (!string.IsNullOrEmpty(city)) query = query.Where(c => c.City.Contains(city));
            ViewBag.City = city;
            return View(await query.OrderBy(c => c.City).ToListAsync());
        }

        public async Task<IActionResult> Details(int id)
        {
            var cinema = await _context.Cinemas
                .Include(c => c.Halls).ThenInclude(h => h.Screenings).ThenInclude(s => s.Movie)
                .Include(c => c.Halls).ThenInclude(h => h.Screenings).ThenInclude(s => s.Reservations)
                .FirstOrDefaultAsync(c => c.CinemaId == id);
            if (cinema == null) return NotFound();
            return View(cinema);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Cinema cinema)
        {
            if (ModelState.IsValid)
            {
                _context.Cinemas.Add(cinema);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Cinema added.";
                return RedirectToAction(nameof(Index));
            }
            return View(cinema);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var cinema = await _context.Cinemas.FindAsync(id);
            if (cinema == null) return NotFound();
            return View(cinema);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Cinema cinema)
        {
            if (id != cinema.CinemaId) return NotFound();
            if (ModelState.IsValid)
            {
                _context.Update(cinema);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Cinema updated.";
                return RedirectToAction(nameof(Index));
            }
            return View(cinema);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var cinema = await _context.Cinemas.Include(c => c.Halls).FirstOrDefaultAsync(c => c.CinemaId == id);
            if (cinema == null) return NotFound();
            return View(cinema);
        }

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var cinema = await _context.Cinemas.FindAsync(id);
            if (cinema != null)
            {
                _context.Cinemas.Remove(cinema);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Cinema deleted.";
            }
            return RedirectToAction(nameof(Index));
        }

    }
}
