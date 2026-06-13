using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CinePlex.Data;

namespace CinePlex.Areas.User.ViewComponents
{
    public class NavCinemaPickerViewComponent : ViewComponent
    {
        private readonly ApplicationDbContext _context;
        public NavCinemaPickerViewComponent(ApplicationDbContext context) => _context = context;

        public async Task<IViewComponentResult> InvokeAsync()
        {
            var cinemas = await _context.Cinemas.OrderBy(c => c.Name).ToListAsync();
            var selectedId = HttpContext.Session.GetInt32("SelectedCinemaId");
            ViewBag.SelectedCinema = cinemas.FirstOrDefault(c => c.CinemaId == selectedId);
            return View(cinemas);
        }
    }
}
