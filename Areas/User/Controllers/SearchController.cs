using Microsoft.AspNetCore.Mvc;
using CinePlex.Models;
using CinePlex.Services;

namespace CinePlex.Areas.User.Controllers
{
    [Area("User")]
    public class SearchController : Controller
    {
        private readonly ISearchService _searchService;

        public SearchController(ISearchService searchService)
        {
            _searchService = searchService;
        }

        public async Task<IActionResult> Index(string? q)
        {
            ViewBag.Query = q;
            if (string.IsNullOrWhiteSpace(q))
            {
                ViewBag.Movies  = new List<Movie>();
                ViewBag.Cinemas = new List<Cinema>();
                return View();
            }

            var (movies, cinemas) = await _searchService.SearchUserAsync(q);
            ViewBag.Movies  = movies;
            ViewBag.Cinemas = cinemas;
            return View();
        }
    }
}
