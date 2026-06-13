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
    public class BarItemsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IFileUploadService _fileUpload;

        public BarItemsController(ApplicationDbContext context, IFileUploadService fileUpload)
        {
            _context = context;
            _fileUpload = fileUpload;
        }

        public async Task<IActionResult> Index()
        {
            ViewData["Title"] = "Bar";
            ViewBag.BarSets = await _context.BarSets
                .Include(s => s.Items).ThenInclude(i => i.BarItem)
                .OrderBy(s => s.SortOrder).ThenBy(s => s.Name)
                .ToListAsync();
            return View(await _context.BarItems.OrderBy(b => b.Category).ThenBy(b => b.SortOrder).ThenBy(b => b.Name).ToListAsync());
        }

        [HttpPost]
        public async Task<IActionResult> Reorder([FromBody] int[] ids)
        {
            if (ids == null || ids.Length == 0) return BadRequest();
            var items = await _context.BarItems.Where(b => ids.Contains(b.BarItemId)).ToListAsync();
            for (int i = 0; i < ids.Length; i++)
            {
                var item = items.FirstOrDefault(b => b.BarItemId == ids[i]);
                if (item != null) item.SortOrder = i;
            }
            await _context.SaveChangesAsync();
            return Ok();
        }

        public IActionResult Create()
        {
            ViewData["Title"] = "Dodaj produkt";
            ViewBag.Categories = Enum.GetValues<BarCategory>();
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(BarItem item, IFormFile? image)
        {
            if (image != null)
                item.ImageUrl = await _fileUpload.SaveBarImageAsync(image);

            if (ModelState.IsValid)
            {
                item.SortOrder = await _context.BarItems.Where(b => b.Category == item.Category).MaxAsync(b => (int?)b.SortOrder) + 1 ?? 0;
                _context.BarItems.Add(item);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Produkt '{item.Name}' dodany.";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Categories = Enum.GetValues<BarCategory>();
            return View(item);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var item = await _context.BarItems.FindAsync(id);
            if (item == null) return NotFound();
            ViewData["Title"] = "Edytuj produkt";
            ViewBag.Categories = Enum.GetValues<BarCategory>();
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, BarItem item, IFormFile? image)
        {
            if (id != item.BarItemId) return BadRequest();

            if (image != null)
            {
                _fileUpload.DeleteFile(item.ImageUrl);
                item.ImageUrl = await _fileUpload.SaveBarImageAsync(image);
            }

            if (ModelState.IsValid)
            {
                _context.Update(item);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Produkt '{item.Name}' zaktualizowany.";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Categories = Enum.GetValues<BarCategory>();
            return View(item);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var item = await _context.BarItems.FindAsync(id);
            if (item != null)
            {
                _fileUpload.DeleteFile(item.ImageUrl);
                _context.BarItems.Remove(item);
                await _context.SaveChangesAsync();
                TempData["Success"] = $"Produkt '{item.Name}' usuniety.";
            }
            return RedirectToAction(nameof(Index));
        }

    }
}
