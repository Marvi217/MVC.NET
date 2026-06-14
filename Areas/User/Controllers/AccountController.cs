using CinePlex.Models;
using CinePlex.Services;
using CinePlex.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CinePlex.Areas.User.Controllers
{
    [Area("User")]

    public class AccountController : Controller
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly IEmailService _emailService;

        public AccountController(UserManager<AppUser> userManager, SignInManager<AppUser> signInManager, IEmailService emailService)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailService = emailService;
        }

        public IActionResult Login(string? returnUrl = null)
        {
            ViewBag.ReturnUrl = returnUrl;
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByEmailAsync(model.Email);
            if (user is AppUser appUser && appUser.IsBlocked)
            {
                ModelState.AddModelError("", "Your account has been blocked.");
                return View(model);
            }

            var result = await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, false);
            if (result.Succeeded) return LocalRedirect(returnUrl ?? "/");

            ModelState.AddModelError("", "Invalid email or password.");
            return View(model);
        }

        public IActionResult Register() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (!ModelState.IsValid) return View(model);

            var user = new AppUser
            {
                UserName = model.Email,
                Email = model.Email,
                FirstName = model.FirstName,
                LastName = model.LastName
            };

            var result = await _userManager.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                foreach (var error in result.Errors)
                    ModelState.AddModelError("", error.Description);
                return View(model);
            }

            await _userManager.AddToRoleAsync(user, "User");

            var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
            var confirmLink = Url.Action(
                action: "ConfirmEmail",
                controller: "Account",
                values: new { userId = user.Id, token },
                protocol: Request.Scheme
            );

            await _emailService.SendEmailConfirmationAsync(
                recipientEmail: user.Email!,
                recipientName: $"{model.FirstName} {model.LastName}".Trim(),
                confirmationLink: confirmLink!
            );

            TempData["Info"] = "SprawdŸ skrzynkê — wyœliliœmy link aktywacyjny.";
            return RedirectToAction("Login");
        }

        [HttpGet]
        public async Task<IActionResult> ConfirmEmail(string userId, string token)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(token))
                return BadRequest("Nieprawid³owy link.");

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null) return NotFound();

            var result = await _userManager.ConfirmEmailAsync(user, token);

            if (result.Succeeded)
            {
                TempData["Success"] = "Email potwierdzony! Mo¿esz siê zalogowaæ.";
                return RedirectToAction("Login");
            }

            TempData["Error"] = "Link wygas³ lub jest nieprawid³owy.";
            return RedirectToAction("Login");
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();
            return RedirectToAction("Index", "Home");
        }

        public IActionResult AccessDenied() => View();
    }
}
