using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CinePlex.Data;
using CinePlex.Models;

namespace CinePlex.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class DirectorsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public DirectorsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(string? search)
        {
            var query = _context.Directors.Include(d => d.Movies).AsQueryable();
            if (!string.IsNullOrEmpty(search)) query = query.Where(d => d.LastName.Contains(search) || d.FirstName.Contains(search));
            ViewBag.Search = search;
            return View(await query.OrderBy(d => d.LastName).ToListAsync());
        }

        public async Task<IActionResult> Details(int id)
        {
            var director = await _context.Directors.Include(d => d.Movies).FirstOrDefaultAsync(d => d.DirectorId == id);
            if (director == null) return NotFound();
            return View(director);
        }

        public IActionResult Create()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Director director)
        {
            if (ModelState.IsValid)
            {
                _context.Directors.Add(director);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Director added.";
                return RedirectToAction(nameof(Index));
            }
            return View(director);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var director = await _context.Directors.FindAsync(id);
            if (director == null) return NotFound();
            return View(director);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Director director)
        {
            if (id != director.DirectorId) return NotFound();
            if (ModelState.IsValid)
            {
                _context.Update(director);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Director updated.";
                return RedirectToAction(nameof(Index));
            }
            return View(director);
        }

        public async Task<IActionResult> Delete(int id)
        {
            var director = await _context.Directors.Include(d => d.Movies).FirstOrDefaultAsync(d => d.DirectorId == id);
            if (director == null) return NotFound();
            return View(director);
        }

        [HttpPost]
        [ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var director = await _context.Directors.FindAsync(id);
            if (director != null)
            {
                _context.Directors.Remove(director);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Director deleted.";
            }
            return RedirectToAction(nameof(Index));
        }

    }
}
