using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CinePlex.Data;
using CinePlex.Models;

namespace CinePlex.Areas.User.Controllers
{
    [Area("User")]
    public class CinemasController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CinemasController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var cinemas = await _context.Cinemas.AsNoTracking().Include(c => c.Halls).OrderBy(c => c.City).ToListAsync();
            return View(cinemas);
        }

        public async Task<IActionResult> Details(int id)
        {
            var cinema = await _context.Cinemas
                .AsNoTracking()
                .Include(c => c.Halls).ThenInclude(h => h.Screenings).ThenInclude(s => s.Movie)
                .Include(c => c.Halls).ThenInclude(h => h.Screenings).ThenInclude(s => s.Reservations)
                .FirstOrDefaultAsync(c => c.CinemaId == id);

            if (cinema == null) return NotFound();

            ViewBag.UpcomingScreenings = cinema.Halls
                .SelectMany(h => h.Screenings)
                .Where(s => s.StartTime >= DateTime.Now)
                .OrderBy(s => s.StartTime)
                .Take(10)
                .ToList();

            return View(cinema);
        }
    }
}
