using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using CinePlex.Data;
using CinePlex.Models;

namespace CinePlex.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class UsersController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly ApplicationDbContext _context;

        public UsersController(UserManager<AppUser> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index(string? search)
        {
            var query = _userManager.Users.AsNoTracking().AsQueryable();
            if (!string.IsNullOrEmpty(search)) query = query.Where(u => u.Email!.Contains(search) || u.LastName.Contains(search));
            ViewBag.Search = search;
            return View(await query.OrderBy(u => u.LastName).ToListAsync());
        }

        public async Task<IActionResult> Details(string id)
        {
            var user = await _context.Users
                .Include(u => ((AppUser)u).Reservations).ThenInclude(r => r.Screening).ThenInclude(s => s.Movie)
                .FirstOrDefaultAsync(u => u.Id == id) as AppUser;
            if (user == null) return NotFound();
            ViewBag.Roles = await _userManager.GetRolesAsync(user);
            return View(user);
        }

        [HttpPost]
        public async Task<IActionResult> ChangeRole(string id, string role)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();
            var currentRoles = await _userManager.GetRolesAsync(user);
            await _userManager.RemoveFromRolesAsync(user, currentRoles);
            await _userManager.AddToRoleAsync(user, role);
            TempData["Success"] = $"Role changed to {role}.";
            return RedirectToAction("Details", new { id });
        }

        [HttpPost]
        public async Task<IActionResult> ToggleBlock(string id)
        {
            var user = await _userManager.FindByIdAsync(id) as AppUser;
            if (user == null) return NotFound();
            user.IsBlocked = !user.IsBlocked;
            await _userManager.UpdateAsync(user);
            TempData["Success"] = user.IsBlocked ? "Account blocked." : "Account unblocked.";
            return RedirectToAction("Details", new { id });
        }
    }
}
