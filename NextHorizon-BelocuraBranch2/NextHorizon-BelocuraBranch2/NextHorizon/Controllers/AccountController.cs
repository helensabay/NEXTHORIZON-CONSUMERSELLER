using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Microsoft.AspNetCore.Mvc;
using NextHorizon.Data;
using NextHorizon.Models;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;


namespace NextHorizon.Controllers;

public class AccountController : Controller
{
    private readonly AppDbContext _context;

    public AccountController(AppDbContext context)
    {
        _context = context;
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (HttpContext.Session.GetString("SellerEmail") != null)
        {
            return RedirectToAction("SellerDashboard", "Dashboard");
        }

        return View();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        try
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == model.Email);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                return View(model);
            }

            if (!string.Equals(user.UserType, "Seller", StringComparison.OrdinalIgnoreCase))
            {
                ModelState.AddModelError(string.Empty, "This portal is for sellers only.");
                return View(model);
            }

            if (string.IsNullOrEmpty(user.PasswordHash) || !user.PasswordHash.Contains(':'))
            {
                ModelState.AddModelError(string.Empty, "Account configuration error. Please contact support.");
                return View(model);
            }

            var parts = user.PasswordHash.Split(':');
            var storedHash = parts[0];
            var storedSalt = parts[1];
            var saltBytes = Convert.FromBase64String(storedSalt);

            var enteredHash = Convert.ToBase64String(KeyDerivation.Pbkdf2(
                password: model.Password,
                salt: saltBytes,
                prf: KeyDerivationPrf.HMACSHA256,
                iterationCount: 10000,
                numBytesRequested: 256 / 8));

            if (enteredHash != storedHash)
            {
                ModelState.AddModelError(string.Empty, "Invalid email or password.");
                return View(model);
            }

            var seller = _context.SellerAccounts.FirstOrDefault(s => s.UserId == user.UserId);
            if (seller == null)
            {
                ModelState.AddModelError(string.Empty, "No seller account found for this user.");
                return View(model);
            }
                        var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Email, user.Email),
                new Claim(ClaimTypes.Role, user.UserType)
            };
            var claimsIdentity = new ClaimsIdentity(claims, "Cookies");
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
            };

            await HttpContext.SignInAsync("Cookies", new ClaimsPrincipal(claimsIdentity), authProperties);
            HttpContext.Session.SetString("SellerEmail", seller.BusinessEmail ?? user.Email);
            HttpContext.Session.SetInt32("SellerId", seller.SellerId);
            HttpContext.Session.SetString("SellerName", seller.BusinessName ?? "Seller");
            HttpContext.Session.SetInt32("UserId", user.UserId);
            return RedirectToAction("SellerDashboard", "Dashboard");
        }
        catch (Exception ex)
        {
            // This will print the exact reason to your UI temporarily so we can fix it!
            ModelState.AddModelError(string.Empty, $"DEBUG ERROR: {ex.Message}");
            return View(model);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Logout()
    {
        HttpContext.Session.Clear();
        return RedirectToAction("Login", "Account");
    }
}
