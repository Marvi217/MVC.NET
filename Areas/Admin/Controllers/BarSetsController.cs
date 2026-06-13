using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CinePlex.Data;
using CinePlex.Models;
using CinePlex.Services;

namespace CinePlex.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class BarSetsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileUploadService _fileUpload;

        public BarSetsController(ApplicationDbContext context, IFileUploadService fileUpload)
        {
            _context = context;
            _fileUpload = fileUpload;
        }

        public IActionResult Index()
        {
            return RedirectToAction("Index", "BarItems");
        }

        [HttpPost]
        public async Task<IActionResult> Reorder([FromBody] int[] ids)
        {
            if (ids == null || ids.Length == 0) return BadRequest();
            var sets = await _context.BarSets.Where(s => ids.Contains(s.BarSetId)).ToListAsync();
            for (int i = 0; i < ids.Length; i++)
            {
                var set = sets.FirstOrDefault(s => s.BarSetId == ids[i]);
                if (set != null) set.SortOrder = i;
            }
            await _context.SaveChangesAsync();
            return Ok();
        }

        public async Task<IActionResult> Create()
        {
            ViewData["Title"] = "Dodaj zestaw";
            ViewBag.AllItems = await _context.BarItems.OrderBy(b => b.Category).ThenBy(b => b.Name).ToListAsync();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BarSet set, IFormFile? image,
            List<int> itemIds, List<int> quantities)
        {
            if (image != null)
                set.ImageUrl = await _fileUpload.SaveBarImageAsync(image);

            if (ModelState.IsValid)
            {
                set.SortOrder = (await _context.BarSets.MaxAsync(s => (int?)s.SortOrder) ?? -1) + 1;
                _context.BarSets.Add(set);
                await _context.SaveChangesAsync();

                for (int i = 0; i < itemIds.Count; i++)
                {
                    if (itemIds[i] == 0) continue;
                    _context.BarSetItems.Add(new BarSetItem
                    {
                        BarSetId  = set.BarSetId,
                        BarItemId = itemIds[i],
                        Quantity  = quantities.Count > i ? Math.Max(1, quantities[i]) : 1
                    });
                }
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Zestaw '{set.Name}' dodany.";
                return RedirectToAction("Index", "BarItems");
            }
            ViewBag.AllItems = await _context.BarItems.OrderBy(b => b.Category).ThenBy(b => b.Name).ToListAsync();
            return View(set);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var set = await _context.BarSets
                .Include(s => s.Items).ThenInclude(i => i.BarItem)
                .FirstOrDefaultAsync(s => s.BarSetId == id);
            if (set == null) return NotFound();
            ViewData["Title"] = "Edytuj zestaw";
            ViewBag.AllItems = await _context.BarItems.OrderBy(b => b.Category).ThenBy(b => b.Name).ToListAsync();
            return View(set);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, BarSet set, IFormFile? image,
            List<int> itemIds, List<int> quantities)
        {
            if (id != set.BarSetId) return BadRequest();

            if (image != null)
            {
                _fileUpload.DeleteFile(set.ImageUrl);
                set.ImageUrl = await _fileUpload.SaveBarImageAsync(image);
            }

            if (ModelState.IsValid)
            {

                var existing = await _context.BarSets.FindAsync(id);
                if (existing == null) return NotFound();
                existing.Name        = set.Name;
                existing.Description = set.Description;
                existing.Price       = set.Price;
                existing.ImageUrl    = set.ImageUrl ?? existing.ImageUrl;
                existing.IsAvailable = set.IsAvailable;

                var old = _context.BarSetItems.Where(i => i.BarSetId == id);
                _context.BarSetItems.RemoveRange(old);

                for (int i = 0; i < itemIds.Count; i++)
                {
                    if (itemIds[i] == 0) continue;
                    _context.BarSetItems.Add(new BarSetItem
                    {
                        BarSetId  = id,
                        BarItemId = itemIds[i],
                        Quantity  = quantities.Count > i ? Math.Max(1, quantities[i]) : 1
                    });
                }
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Zestaw '{existing.Name}' zaktualizowany.";
                return RedirectToAction("Index", "BarItems");
            }
            ViewBag.AllItems = await _context.BarItems.OrderBy(b => b.Category).ThenBy(b => b.Name).ToListAsync();
            return View(set);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var set = await _context.BarSets.FindAsync(id);
            if (set != null)
            {
                _fileUpload.DeleteFile(set.ImageUrl);
                _context.BarSets.Remove(set);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Zestaw '{set.Name}' usuniety.";
            }
            return RedirectToAction(nameof(Index));
        }

    }
}
