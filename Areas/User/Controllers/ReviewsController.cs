using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CinePlex.Data;
using CinePlex.Models;

namespace CinePlex.Areas.User.Controllers
{
    [Area("User")]
    [Authorize]
    public class ReviewsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<AppUser> _userManager;

        public ReviewsController(ApplicationDbContext context, UserManager<AppUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(int movieId, int rating, string? comment)
        {
            if (rating < 1 || rating > 5)
            {
                TempData["Error"] = "Ocena musi być od 1 do 5.";
                return RedirectToAction("Details", "Movies", new { id = movieId });
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var existing = await _context.MovieReviews
                .FirstOrDefaultAsync(r => r.AppUserId == user.Id && r.MovieId == movieId);

            if (existing != null)
            {
                existing.Rating    = rating;
                existing.Comment   = comment?.Trim();
                existing.UpdatedAt = DateTime.Now;
            }
            else
            {
                _context.MovieReviews.Add(new MovieReview
                {
                    AppUserId = user.Id,
                    MovieId   = movieId,
                    Rating    = rating,
                    Comment   = comment?.Trim(),
                    CreatedAt = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Ocena zapisana!";
            return RedirectToAction("Details", "Movies", new { id = movieId, _anchor = "reviews" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int movieId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            var review = await _context.MovieReviews
                .FirstOrDefaultAsync(r => r.AppUserId == user.Id && r.MovieId == movieId);

            if (review != null)
            {
                _context.MovieReviews.Remove(review);
                await _context.SaveChangesAsync();
                TempData["Success"] = "Ocena usunięta.";
            }

            return RedirectToAction("Details", "Movies", new { id = movieId, _anchor = "reviews" });
        }
    }
}
