using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using CinePlex.Data;
using CinePlex.Models;
using System.Globalization;

namespace CinePlex.Areas.Admin.Controllers
{
    [Area("Admin")]
    [Authorize(Roles = "Admin")]
    public class StatisticsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public StatisticsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            ViewBag.TotalMovies = await _context.Movies.AsNoTracking().CountAsync();
            ViewBag.TotalScreenings = await _context.Screenings.AsNoTracking().CountAsync();
            ViewBag.TotalReservations = await _context.Reservations.AsNoTracking().CountAsync(r => r.Status != ReservationStatus.Cancelled);
            ViewBag.TotalUsers = await _context.Users.AsNoTracking().CountAsync();
            ViewBag.TotalRevenue = await _context.Reservations
                .AsNoTracking()
                .Where(r => r.Status != ReservationStatus.Cancelled)
                .SumAsync(r => r.PricePaid);

            ViewBag.TopMovies = await _context.Movies
                .AsNoTracking()
                .Select(m => new { Title = m.Title, ReservationCount = m.Screenings.SelectMany(s => s.Reservations).Count(r => r.Status != ReservationStatus.Cancelled) })
                .OrderByDescending(x => x.ReservationCount).Take(10).ToListAsync();

            var perMonthRaw = await _context.Reservations
                .AsNoTracking()
                .Where(r => r.Status != ReservationStatus.Cancelled && r.PurchaseDate.Year == DateTime.Now.Year)
                .GroupBy(r => r.PurchaseDate.Month)
                .Select(g => new { Month = g.Key, Count = g.Count() })
                .OrderBy(x => x.Month).ToListAsync();
            ViewBag.ReservationsPerMonth = perMonthRaw
                .Select(x => new { Month = CultureInfo.InvariantCulture.DateTimeFormat.GetAbbreviatedMonthName(x.Month), x.Count });

            ViewBag.RevenuePerCinema = await _context.Cinemas
                .AsNoTracking()
                .Select(c => new
                {
                    Name = c.Name,
                    Revenue = c.Halls.SelectMany(h => h.Screenings)
                        .SelectMany(s => s.Reservations)
                        .Where(r => r.Status != ReservationStatus.Cancelled)
                        .Sum(r => r.PricePaid)
                }).ToListAsync();

            return View();
        }
    }
}
