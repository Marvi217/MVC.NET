using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CinePlex.Data;
using CinePlex.Models;

namespace CinePlex.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class PricingController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PricingController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var pricings = await _context.HallTypePricings.OrderBy(p => p.HallType).ToListAsync();
            return View(pricings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, decimal defaultPrice)
        {
            var pricing = await _context.HallTypePricings.FindAsync(id);
            if (pricing == null) return NotFound();
            if (defaultPrice <= 0)
            {
                TempData["Error"] = "Cena musi być większa od 0.";
                return RedirectToAction(nameof(Index));
            }
            pricing.DefaultPrice = defaultPrice;
            await _context.SaveChangesAsync();
            TempData["Success"] = "Cena zaktualizowana.";
            return RedirectToAction(nameof(Index));
        }
    }
}
