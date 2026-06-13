using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CinePlex.Data;
using CinePlex.Models;

namespace CinePlex.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class ReviewsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReviewsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index(ReviewModerationStatus? status, string? search, int page = 1)
        {
            const int pageSize = 25;
            var query = _context.MovieReviews
                .AsNoTracking()
                .Include(r => r.AppUser)
                .Include(r => r.Movie)
                .AsQueryable();

            if (status.HasValue) query = query.Where(r => r.ModerationStatus == status.Value);
            if (!string.IsNullOrEmpty(search))
                query = query.Where(r => r.Movie!.Title.Contains(search) || (r.AppUser != null && r.AppUser.Email!.Contains(search)));

            var total = await query.CountAsync();
            ViewBag.Status     = status;
            ViewBag.Search     = search;
            ViewBag.Page       = page;
            ViewBag.TotalPages = (int)Math.Ceiling(total / (double)pageSize);
            ViewBag.Statuses   = Enum.GetValues<ReviewModerationStatus>();
            ViewBag.PendingCount = await _context.MovieReviews.AsNoTracking()
                .CountAsync(r => r.ModerationStatus == ReviewModerationStatus.Pending);

            var rawStats = await _context.MovieReviews.AsNoTracking()
                .Where(r => r.ModerationStatus != ReviewModerationStatus.Hidden)
                .GroupBy(r => r.MovieId)
                .Select(g => new { MovieId = g.Key, Avg = g.Average(r => r.Rating), Count = g.Count() })
                .OrderByDescending(x => x.Count).Take(5).ToListAsync();
            var titleMap = rawStats.Count > 0
                ? await _context.Movies.AsNoTracking()
                    .Where(m => rawStats.Select(s => s.MovieId).Contains(m.MovieId))
                    .ToDictionaryAsync(m => m.MovieId, m => m.Title)
                : new Dictionary<int, string>();
            ViewBag.TopRatedMovies = rawStats.Select(s => new {
                Title = titleMap.GetValueOrDefault(s.MovieId, "?"),
                Avg   = Math.Round(s.Avg, 1),
                s.Count
            }).ToList();

            return View(await query.OrderByDescending(r => r.CreatedAt).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Moderate(int id, ReviewModerationStatus status)
        {
            var review = await _context.MovieReviews.FindAsync(id);
            if (review == null) return NotFound();
            review.ModerationStatus = status;
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Review marked as {status}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var review = await _context.MovieReviews.FindAsync(id);
            if (review != null)
            {
                _context.MovieReviews.Remove(review);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Review deleted.";
            }
            return RedirectToAction(nameof(Index));
        }

    }
}
