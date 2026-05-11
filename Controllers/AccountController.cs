using Microsoft.AspNetCore.Mvc;
using MyAspNetApp.Data;
using MyAspNetApp.Models;
using WebApplication1.ViewModels;
using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using System.Net.Mail;
using System.Net;
using System.Text;
using System.Collections.Generic;
using System.IO;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;

namespace MyAspNetApp.Controllers
{
    public class AccountController : Controller
    {
        private const string SharedUserIdCookie = "NextHorizon.SharedUserId";
        private const string SharedUserEmailCookie = "NextHorizon.SharedUserEmail";
        private const string SharedUserTypeCookie = "NextHorizon.SharedUserType";
        private const string SharedDisplayNameCookie = "NextHorizon.SharedDisplayName";
        private const string LoginAppUrl = "/Account/Login";
        private const string LandingPageUrl = "/";
        private const string ProductsModuleUrl = "/Home/Storefront";
        private const string SellerDashboardUrl = "/Dashboard/SellerDashboard";
        private readonly AppDbContext _context;

        public AccountController(AppDbContext context)
        {
            _context = context;
        }

        // =========================
        // REGISTER GET
        // =========================
        [HttpGet]
        public IActionResult Register()
        {
            TempData.Remove("SuccessMessage");
            return View();
        }

        [HttpGet]
public IActionResult CheckUsername(string username)
{
    bool taken = _context.Consumers.Any(c => c.Username == username);
    return Json(new { taken });
}

[HttpGet]
public IActionResult CheckEmail(string email)
{
    bool taken = _context.Users.Any(u => u.Email == email);
    return Json(new { taken });
}

[HttpGet]
public IActionResult CheckPhone(string phone)
{
    bool taken = _context.Consumers.Any(c => c.PhoneNumber == phone);
    return Json(new { taken });
}

        // =========================
        // REGISTER POST
        // =========================
[HttpPost]
[ValidateAntiForgeryToken]
public IActionResult Register(RegisterViewModel model)
{
    if (!ModelState.IsValid)
    {
        return View(model);
    }

    // Check username
    if (_context.Consumers.Any(u => u.Username == model.Username))
    {
        TempData["RegisterError"] = "username_taken";
        return View(model);
    }

    // Check email
    if (_context.Users.Any(u => u.Email == model.Email))
    {
        TempData["RegisterError"] = "email_taken";
        return View(model);
    }

    // Check phone
    if (_context.Consumers.Any(u => u.PhoneNumber == model.PhoneNumber))
    {
        TempData["RegisterError"] = "phone_taken";
        return View(model);
    }

    // Validate phone format
    var phone = model.PhoneNumber?.Trim();
    bool validPhone = System.Text.RegularExpressions.Regex.IsMatch(
        phone ?? "", @"^(09\d{9}|\+639\d{9})$");

    if (!validPhone)
    {
        TempData["RegisterError"] = "phone_invalid";
        return View(model);
    }

    // Generate salt
    byte[] salt = new byte[128 / 8];
    using (var rng = RandomNumberGenerator.Create())
    {
        rng.GetBytes(salt);
    }

    // Hash the password
    string hashed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
        password: model.Password,
        salt: salt,
        prf: KeyDerivationPrf.HMACSHA256,
        iterationCount: 10000,
        numBytesRequested: 256 / 8));

    string saltBase64 = Convert.ToBase64String(salt);

    var user = new User
    {
        Email = model.Email,
        PasswordHash = hashed + ":" + saltBase64,
        UserType = "Consumer",
        CreatedAt = DateTime.Now
    };

    _context.Users.Add(user);
    _context.SaveChanges();

    var consumer = new Consumer
    {
        UserId = user.UserId,
        Username = model.Username,
        FirstName = model.FirstName,
        MiddleName = model.MiddleName,
        LastName = model.LastName,
        Address = model.Address,
        PhoneNumber = model.PhoneNumber,
        CreatedAt = DateTime.Now
    };

    _context.Consumers.Add(consumer);
    _context.SaveChanges();

TempData["SuccessMessage"] = "Account created successfully!";
return View(model);
}
 

        // =========================
        // LOGIN GET
        // =========================
      // GET: /Account/Login
[HttpGet]
public IActionResult Login(string? returnUrl = null)
{
    TempData.Remove("LoginError");
    return View(new LoginViewModel { ReturnUrl = returnUrl });
}

[HttpPost]
[ValidateAntiForgeryToken]
public IActionResult Login(LoginViewModel model)
{
    if (!ModelState.IsValid)
    {
        return View(model);
    }

    try
    {
        var input = model.Email.Trim();

        // 1. Find user by email
        var user = _context.Users.FirstOrDefault(u => u.Email == input);

        // 2. If not found by email, try phone or username via Consumers
        if (user == null)
        {
            var consumer = _context.Consumers.FirstOrDefault(c =>
                c.PhoneNumber == input ||
                c.Username == input);

            if (consumer != null)
            {
                user = _context.Users.FirstOrDefault(u => u.UserId == consumer.UserId);
            }
        }

        // 3. User not found
        if (user == null)
        {
            TempData["LoginError"] = "user_not_found";
            return View(model);
        }

        // 4. Validate hash format
        if (string.IsNullOrEmpty(user.PasswordHash) || !user.PasswordHash.Contains(":"))
        {
            ModelState.AddModelError(string.Empty, "Account configuration error. Please contact support.");
            return View(model);
        }

        // 5. Split hash and salt
        var parts = user.PasswordHash.Split(':');
        var storedHash = parts[0];
        var storedSalt = parts[1];
        byte[] saltBytes = Convert.FromBase64String(storedSalt);

        // 6. Hash the entered password
        string enteredHash = Convert.ToBase64String(
            Microsoft.AspNetCore.Cryptography.KeyDerivation.KeyDerivation.Pbkdf2(
                password: model.Password,
                salt: saltBytes,
                prf: Microsoft.AspNetCore.Cryptography.KeyDerivation.KeyDerivationPrf.HMACSHA256,
                iterationCount: 10000,
                numBytesRequested: 256 / 8));

        // 7. Wrong password
        if (enteredHash != storedHash)
        {
            TempData["LoginError"] = "wrong_password";
            return View(model);
        }

        // 8. Success
        // 8. Check user type and seller status
if (user.UserType == "Seller")
{
    var seller = _context.Sellers.FirstOrDefault(s => s.UserId == user.UserId);

    if (seller == null)
    {
        TempData["LoginError"] = "seller_not_found";
        return View(model);
    }

    if (seller.SellerStatus == "Pending")
    {
        TempData["LoginError"] = "seller_pending";
        return View(model);
    }

    if (seller.SellerStatus == "Rejected")
    {
        TempData["LoginError"] = "seller_rejected";
        return View(model);
    }

    // Seller is approved — set session and redirect to seller dashboard
    HttpContext.Session.SetInt32("UserId", user.UserId);
    HttpContext.Session.SetInt32("SellerId", seller.SellerId);
    HttpContext.Session.SetString("UserType", "Seller");
    HttpContext.Session.SetString("UserEmail", user.Email ?? "");
    HttpContext.Session.SetString("SellerEmail", seller.BusinessEmail ?? user.Email ?? "");
    HttpContext.Session.SetString("SellerName", seller.BusinessName ?? "Seller");
    HttpContext.Session.SetString("BusinessName", seller.BusinessName ?? "Seller");
    HttpContext.Session.SetString("DisplayName", seller.BusinessName ?? "Seller");
    AppendSharedAuthCookies(user.UserId, user.Email, "Seller", seller.BusinessName ?? "Seller");

    TempData["SuccessMessage"] = $"Welcome back, {seller.BusinessName}!";
    return Redirect(SellerDashboardUrl);
}
else
{
    // Consumer login
    var loggedConsumer = _context.Consumers.FirstOrDefault(c => c.UserId == user.UserId);
    string displayName = loggedConsumer != null ? loggedConsumer.FirstName : "User";

    // Set session
    HttpContext.Session.SetInt32("UserId", user.UserId);
    HttpContext.Session.SetString("UserType", "Consumer");
    HttpContext.Session.SetString("Username", loggedConsumer?.Username ?? "");
    HttpContext.Session.SetString("FirstName", loggedConsumer?.FirstName ?? "User");
    HttpContext.Session.SetString("DisplayName", displayName);
    AppendSharedAuthCookies(user.UserId, user.Email, "Consumer", displayName);

    TempData["SuccessMessage"] = $"Welcome, {displayName}!";
    return Redirect(ResolvePostLoginRedirect(model.ReturnUrl));
}
    }
    catch (Exception)
    {
        ModelState.AddModelError(string.Empty, "An error occurred during login. Please try again.");
        return View(model);
    }
}
    
//seller
[HttpGet]
public IActionResult BecomeSeller()
{
    return View(new BecomeSellerViewModel());
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> BecomeSeller(BecomeSellerViewModel model)
{
    if (!ModelState.IsValid) return View(model);

    try
    {
        // Hash password
        byte[] salt = new byte[16];
        using (var rng = RandomNumberGenerator.Create()) { rng.GetBytes(salt); }
        string hashed = Convert.ToBase64String(KeyDerivation.Pbkdf2(
            password: model.Password,
            salt: salt,
            prf: KeyDerivationPrf.HMACSHA256,
            iterationCount: 10000,
            numBytesRequested: 32));
        string passwordHash = hashed + ":" + Convert.ToBase64String(salt);

        // Convert logo to binary
        byte[] logoData = null;
        string logoContentType = null;
        if (model.BrandLogo != null)
        {
            using var ms = new MemoryStream();
            await model.BrandLogo.CopyToAsync(ms);
            logoData = ms.ToArray();
            logoContentType = model.BrandLogo.ContentType;
        }

        // Convert documents to binary
        byte[] dtiData = null, birData = null, permitData = null, additionalData = null;
        string dtiType = null, birType = null, permitType = null, additionalType = null;

        if (model.DtiCertificate != null)
        {
            using var ms = new MemoryStream();
            await model.DtiCertificate.CopyToAsync(ms);
            dtiData = ms.ToArray();
            dtiType = model.DtiCertificate.ContentType;
        }
        if (model.BirCertificate != null)
        {
            using var ms = new MemoryStream();
            await model.BirCertificate.CopyToAsync(ms);
            birData = ms.ToArray();
            birType = model.BirCertificate.ContentType;
        }
        if (model.BusinessPermit != null)
        {
            using var ms = new MemoryStream();
            await model.BusinessPermit.CopyToAsync(ms);
            permitData = ms.ToArray();
            permitType = model.BusinessPermit.ContentType;
        }
        if (model.AdditionalDocument != null)
        {
            using var ms = new MemoryStream();
            await model.AdditionalDocument.CopyToAsync(ms);
            additionalData = ms.ToArray();
            additionalType = model.AdditionalDocument.ContentType;
        }

        // Call stored procedure
        var connectionString = _context.Database.GetConnectionString();
        using (var connection = new Microsoft.Data.SqlClient.SqlConnection(connectionString))
        {
            await connection.OpenAsync();
            using (var cmd = new Microsoft.Data.SqlClient.SqlCommand("sp_RegisterSeller", connection))
            {
                cmd.CommandType = System.Data.CommandType.StoredProcedure;
                cmd.Parameters.AddWithValue("@Email",           model.BusinessEmail);
                cmd.Parameters.AddWithValue("@PasswordHash",    passwordHash);
                cmd.Parameters.AddWithValue("@FirstName",       model.FirstName);
                cmd.Parameters.AddWithValue("@MiddleName",      model.MiddleName ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@LastName",        model.LastName);
                cmd.Parameters.AddWithValue("@Username",        model.Username);
                cmd.Parameters.AddWithValue("@PhoneNumber",     model.PhoneNumber);
                cmd.Parameters.AddWithValue("@BusinessName",    model.BusinessName);
                cmd.Parameters.AddWithValue("@BusinessType",    model.BusinessType);
                cmd.Parameters.AddWithValue("@BusinessEmail",   model.BusinessEmail);
                cmd.Parameters.AddWithValue("@BusinessPhone",   model.BusinessPhone);
                cmd.Parameters.AddWithValue("@TaxId",           model.TaxId);
                cmd.Parameters.AddWithValue("@BusinessAddress", model.BusinessAddress);
                cmd.Parameters.AddWithValue("@LogoData",        logoData        ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@LogoContentType", logoContentType ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@DtiData",         dtiData         ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@DtiContentType",  dtiType         ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@BirData",         birData         ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@BirContentType",  birType         ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@PermitData",      permitData      ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@PermitContentType", permitType    ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@AdditionalData",  additionalData  ?? (object)DBNull.Value);
                cmd.Parameters.AddWithValue("@AdditionalContentType", additionalType ?? (object)DBNull.Value);

                using (var reader = await cmd.ExecuteReaderAsync())
                {
                    if (await reader.ReadAsync())
                    {
                        var status = reader["Status"].ToString();
                        if (status != "Success")
                        {
                            ModelState.AddModelError("", reader["Message"].ToString());
                            return View(model);
                        }
                    }
                }
            }
        }

        TempData["SellerSuccess"] = "true";
        return View(model);
    }
    catch (Exception ex)
    {
        var message = ex.InnerException != null ? ex.InnerException.Message : ex.Message;
        ModelState.AddModelError("", "Database Error: " + message);
        return View(model);
    }
}

// Helper method to keep code clean
private async Task<string> SaveFile(IFormFile file, string folder)
{
    if (file == null) return null;
    var fileName = Guid.NewGuid() + Path.GetExtension(file.FileName);
    var fullPath = Path.Combine(folder, fileName);
    using (var stream = new FileStream(fullPath, FileMode.Create))
    {
        await file.CopyToAsync(stream);
    }
    return "/uploads/" + fileName;
}

// =========================
// LOGOUT
// =========================
[HttpPost]
[ValidateAntiForgeryToken]
public IActionResult Logout()
{
    HttpContext.Session.Clear();
    ClearSharedAuthCookies();
    return Redirect(LandingPageUrl);
}

[HttpGet]
[Route("Account/Logout")]
public IActionResult LogoutGet()
{
    HttpContext.Session.Clear();
    ClearSharedAuthCookies();
    return Redirect(LandingPageUrl);
}

private void AppendSharedAuthCookies(int userId, string? email, string userType, string displayName)
{
    var cookieOptions = new CookieOptions
    {
        HttpOnly = false,
        IsEssential = true,
        SameSite = SameSiteMode.Lax,
        Expires = DateTimeOffset.UtcNow.AddHours(8),
        Path = "/"
    };

    Response.Cookies.Append(SharedUserIdCookie, userId.ToString(), cookieOptions);
    Response.Cookies.Append(SharedUserTypeCookie, userType, cookieOptions);
    Response.Cookies.Append(SharedDisplayNameCookie, displayName, cookieOptions);

    if (!string.IsNullOrWhiteSpace(email))
    {
        Response.Cookies.Append(SharedUserEmailCookie, email, cookieOptions);
    }
}

private void ClearSharedAuthCookies()
{
    var cookieOptions = new CookieOptions { Path = "/" };
    Response.Cookies.Delete(SharedUserIdCookie, cookieOptions);
    Response.Cookies.Delete(SharedUserEmailCookie, cookieOptions);
    Response.Cookies.Delete(SharedUserTypeCookie, cookieOptions);
    Response.Cookies.Delete(SharedDisplayNameCookie, cookieOptions);
}

private string ResolvePostLoginRedirect(string? returnUrl)
{
    if (string.IsNullOrWhiteSpace(returnUrl))
    {
        return ProductsModuleUrl;
    }

    if (Url.IsLocalUrl(returnUrl))
    {
        return returnUrl;
    }

    if (Uri.TryCreate(returnUrl, UriKind.Absolute, out var absoluteUri) &&
        (string.Equals(absoluteUri.Host, "localhost", StringComparison.OrdinalIgnoreCase) ||
         string.Equals(absoluteUri.Host, Request.Host.Host, StringComparison.OrdinalIgnoreCase)))
    {
        return absoluteUri.ToString();
    }

    return ProductsModuleUrl;
}

  // =========================
        // Forgot Password
        // =========================

[HttpGet]
        public IActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model)
        {
            if (!ModelState.IsValid)
                return View(model);

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Email == model.Email);

            if (user == null)
            {
                ModelState.AddModelError("", "No account found with this email.");
                return View(model);
            }

            // Generate 6-digit OTP
            var otp = new Random().Next(100000, 999999);

            // Set expiration: 10 minutes from now
            var expiresAt = DateTime.Now.AddMinutes(10);

            // Save OTP in DB
            var passwordOtp = new PasswordOtp
            {
                Email = model.Email,
                Otp = otp,
                ExpiresAt = expiresAt,
                user_id = user.UserId
            };

            _context.PasswordOtps.Add(passwordOtp);
            await _context.SaveChangesAsync();

            // Send OTP via Email (basic SMTP)
            SendOtpEmail(model.Email, otp);

            // Redirect to Verify OTP page
            return RedirectToAction("VerifyOtp", new { email = model.Email });
        }

         // =========================
        // VerifyOTP
        // =========================
 [HttpGet]
public IActionResult VerifyOtp(string email)
{
    var model = new VerifyOtpViewModel { Email = email };
    return View(model);
}

[HttpPost]
public async Task<IActionResult> VerifyOtp(VerifyOtpViewModel model)
{
    if (!ModelState.IsValid)
    {
        ViewData["OtpError"] = "Please enter a valid OTP.";
        return View(model);
    }

    var otpRecord = await _context.PasswordOtps
        .Where(o => o.Email == model.Email && o.Otp == model.Otp)
        .OrderByDescending(o => o.CreatedAt)
        .FirstOrDefaultAsync();

    if (otpRecord == null)
    {
        ViewData["OtpError"] = "The OTP you entered is incorrect.";
        return View(model);
    }

    if (otpRecord.ExpiresAt < DateTime.UtcNow)
    {
        ViewData["OtpError"] = "Your OTP has expired. Please request a new one.";
        return View(model);
    }

    // Stay on page so success modal fires, JS redirects to ResetPassword
    ViewData["OtpSuccess"] = "Your OTP has been verified successfully!";
    ViewData["RedirectEmail"] = model.Email; // pass email for JS redirect
    return View(model);
}

          // =========================
        // RESET PASSWORD
        // =========================
[HttpGet]
public IActionResult ResetPassword(string email)
{
    var model = new ResetPasswordViewModel { Email = email };
    return View(model);
}

[HttpPost]
public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model)
{
    if (!ModelState.IsValid)
    {
        ViewData["ResetError"] = "Please fill in all required fields correctly.";
        return View(model);
    }

    var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == model.Email);
    if (user == null)
    {
        ViewData["ResetError"] = "No account found with that email address.";
        return View(model);
    }

    byte[] salt = new byte[128 / 8];
    using (var rng = RandomNumberGenerator.Create())
    {
        rng.GetBytes(salt);
    }

    string hashed = Convert.ToBase64String(
        Microsoft.AspNetCore.Cryptography.KeyDerivation.KeyDerivation.Pbkdf2(
            password: model.NewPassword,
            salt: salt,
            prf: Microsoft.AspNetCore.Cryptography.KeyDerivation.KeyDerivationPrf.HMACSHA256,
            iterationCount: 10000,
            numBytesRequested: 256 / 8));

    string saltBase64 = Convert.ToBase64String(salt);
    user.PasswordHash = $"{hashed}:{saltBase64}";
    _context.Users.Update(user);
    await _context.SaveChangesAsync();

    // NO redirect — stay on page so modal fires, JS handles redirect
    ViewData["ResetSuccess"] = "Your password has been reset successfully!";
    return View(model);
}

        // ------------------------------
        // Helper Methods
        // ------------------------------
        private void SendOtpEmail(string email, int otp)
        {
            // Example using SmtpClient (configure properly in production)
            try
            {
                var smtp = new SmtpClient("smtp.gmail.com")
                {
                    Port = 587,
                    Credentials = new System.Net.NetworkCredential("nexthorizon398", "cgyc glxf tfrr yyvo"),
                    EnableSsl = true
                };

                var message = new MailMessage("nexthorizon398@gmail.com", email)
                {
                    Subject = "Your OTP for Password Reset",
                    Body = $"Your OTP is: {otp}. It will expire in 10 minutes."
                };

                smtp.Send(message);
            }
            catch
            {
                // Log email sending failure
            }
        }

        private string HashPassword(string password)
        {
            using var sha256 = SHA256.Create();
            var bytes = Encoding.UTF8.GetBytes(password);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToBase64String(hash);
        }
    }
}





    

