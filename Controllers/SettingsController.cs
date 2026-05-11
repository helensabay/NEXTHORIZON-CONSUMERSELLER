using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MyAspNetApp.Data;
using System.Security.Claims;

namespace MyAspNetApp.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SettingsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public SettingsController(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        [HttpGet("{userId:int}")]
        public async Task<IActionResult> GetDashboardSettings(int userId)
        {
            var effectiveUserId = ResolveEffectiveUserId(userId);
            if (effectiveUserId <= 0)
            {
                return BadRequest("User account not found.");
            }

            await EnsureSellerLogoBinaryColumnsAsync();

            var user = await _context.Users.FindAsync(effectiveUserId);
            if (user == null)
            {
                return NotFound("User account not found.");
            }

            var seller = await _context.Sellers
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.UserId == effectiveUserId);

            return Ok(new
            {
                Account = new { user.Email, user.UserType },
                Business = seller == null
                    ? null
                    : new
                    {
                        seller.SellerId,
                        seller.UserId,
                        seller.BusinessType,
                        seller.BusinessName,
                        seller.BusinessEmail,
                        seller.BusinessPhone,
                        seller.TaxId,
                        seller.BusinessAddress,
                        seller.LogoPath,
                        seller.DocumentPath,
                        seller.SellerStatus,
                        seller.CreatedAt,
                        LogoUrl = (Url.Action(nameof(GetLogo), new { userId = effectiveUserId }) ?? $"/api/settings/{effectiveUserId}/logo") + $"?v={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}"
                    }
            });
        }

        [HttpPut("{userId:int}/business")]
        public async Task<IActionResult> UpdateBusinessProfile(int userId, [FromBody] UpdateBusinessProfileDto dto)
        {
            var effectiveUserId = ResolveEffectiveUserId(userId);
            if (effectiveUserId <= 0)
            {
                return NotFound("Business profile not found.");
            }

            var seller = await _context.Sellers.FirstOrDefaultAsync(s => s.UserId == effectiveUserId);
            if (seller == null)
            {
                return NotFound("Business profile not found.");
            }

            seller.BusinessName = dto.BusinessName;
            seller.BusinessType = dto.BusinessType;
            seller.BusinessEmail = dto.BusinessEmail;
            seller.BusinessPhone = dto.BusinessPhone;
            seller.TaxId = dto.TaxId;
            seller.BusinessAddress = dto.BusinessAddress;

            await _context.SaveChangesAsync();
            return Ok(new { message = "Business details saved successfully." });
        }

        [HttpPut("{userId:int}/password")]
        public async Task<IActionResult> ChangePassword(int userId, [FromBody] ChangePasswordDto dto)
        {
            var effectiveUserId = ResolveEffectiveUserId(userId);
            if (effectiveUserId <= 0)
            {
                return NotFound(new { message = "User not found." });
            }

            if (dto.NewPassword != dto.ConfirmNewPassword)
            {
                return BadRequest(new { message = "New passwords do not match." });
            }

            var user = await _context.Users.FindAsync(effectiveUserId);
            if (user == null)
            {
                return NotFound(new { message = "User not found." });
            }

            user.PasswordHash = dto.NewPassword;
            await _context.SaveChangesAsync();
            return Ok(new { message = "Password updated successfully." });
        }

        [HttpPost("{userId:int}/logo")]
        public async Task<IActionResult> UploadLogo(int userId, IFormFile file)
        {
            var effectiveUserId = ResolveEffectiveUserId(userId);
            if (effectiveUserId <= 0)
            {
                return NotFound(new { message = "Seller not found." });
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(new { message = "No file uploaded." });
            }

            if (!IsSupportedImage(file))
            {
                return BadRequest(new { message = "Logo must be a JPG, JPEG, PNG, WEBP, GIF, or BMP image." });
            }

            var seller = await _context.Sellers.FirstOrDefaultAsync(s => s.UserId == effectiveUserId);
            if (seller == null)
            {
                return NotFound(new { message = "Seller not found." });
            }

            await EnsureSellerLogoBinaryColumnsAsync();

            await using var memory = new MemoryStream();
            await file.CopyToAsync(memory);

            var logoBytes = memory.ToArray();
            var logoMimeType = string.IsNullOrWhiteSpace(file.ContentType)
                ? NormalizeMimeType(Path.GetExtension(file.FileName))
                : file.ContentType;

            await _context.Database.ExecuteSqlRawAsync(
                """
                UPDATE dbo.Sellers
                SET logo_data = @logoData,
                    logo_mime_type = @logoMimeType,
                    logo_path = NULL
                WHERE user_id = @userId;
                """,
                new SqlParameter("@logoData", logoBytes),
                new SqlParameter("@logoMimeType", logoMimeType),
                new SqlParameter("@userId", effectiveUserId));

            return Ok(new
            {
                message = "Logo uploaded successfully.",
                logoUrl = (Url.Action(nameof(GetLogo), new { userId = effectiveUserId }) ?? $"/api/settings/{effectiveUserId}/logo") + $"?v={DateTimeOffset.UtcNow.ToUnixTimeSeconds()}"
            });
        }

        [HttpGet("{userId:int}/logo")]
        [ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
        public async Task<IActionResult> GetLogo(int userId)
        {
            var effectiveUserId = ResolveEffectiveUserId(userId);
            await EnsureSellerLogoBinaryColumnsAsync();
            Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
            Response.Headers.Pragma = "no-cache";

            var logo = await GetSellerLogoAsync(effectiveUserId > 0 ? effectiveUserId : userId);
            if (logo == null)
            {
                return Redirect("/images/new-logo.png");
            }

            var logoValue = logo.Value;
            if (logoValue.LogoData is { Length: > 0 })
            {
                return File(logoValue.LogoData, string.IsNullOrWhiteSpace(logoValue.LogoMimeType) ? "image/jpeg" : logoValue.LogoMimeType);
            }

            if (!string.IsNullOrWhiteSpace(logoValue.LogoPath))
            {
                var physicalPath = ResolvePhysicalPath(logoValue.LogoPath);
                if (!string.IsNullOrWhiteSpace(physicalPath) && System.IO.File.Exists(physicalPath))
                {
                    return PhysicalFile(physicalPath, NormalizeMimeType(Path.GetExtension(physicalPath)));
                }

                return Redirect(logoValue.LogoPath);
            }

            return Redirect("/images/new-logo.png");
        }

        private async Task EnsureSellerLogoBinaryColumnsAsync()
        {
            await _context.Database.ExecuteSqlRawAsync(
                """
                IF COL_LENGTH('dbo.Sellers', 'logo_data') IS NULL
                    ALTER TABLE dbo.Sellers ADD logo_data varbinary(max) NULL;
                IF COL_LENGTH('dbo.Sellers', 'logo_mime_type') IS NULL
                    ALTER TABLE dbo.Sellers ADD logo_mime_type nvarchar(100) NULL;
                """);
        }

        private async Task<(byte[]? LogoData, string? LogoMimeType, string? LogoPath)?> GetSellerLogoAsync(int userId)
        {
            var connection = _context.Database.GetDbConnection();
            if (connection.State != System.Data.ConnectionState.Open)
            {
                await connection.OpenAsync();
            }

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT TOP (1) logo_data, logo_mime_type, logo_path FROM dbo.Sellers WHERE user_id = @userId;";

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@userId";
            parameter.Value = userId;
            command.Parameters.Add(parameter);

            await using var reader = await command.ExecuteReaderAsync();
            if (!await reader.ReadAsync())
            {
                return null;
            }

            var logoData = reader.IsDBNull(0) ? null : (byte[])reader.GetValue(0);
            var logoMimeType = reader.IsDBNull(1) ? null : reader.GetString(1);
            var logoPath = reader.IsDBNull(2) ? null : reader.GetString(2);
            return (logoData, logoMimeType, logoPath);
        }

        private string? ResolvePhysicalPath(string? imagePath)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return null;
            }

            var relativePath = imagePath.TrimStart('/').Replace('/', Path.DirectorySeparatorChar);
            return Path.Combine(_environment.WebRootPath, relativePath);
        }

        private static bool IsSupportedImage(IFormFile file)
        {
            var extension = Path.GetExtension(file.FileName);
            var mimeType = string.IsNullOrWhiteSpace(file.ContentType)
                ? NormalizeMimeType(extension)
                : file.ContentType;

            return mimeType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                && extension.ToLowerInvariant() is ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" or ".bmp";
        }

        private static string NormalizeMimeType(string? extension)
        {
            return extension?.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => "image/jpeg",
                ".png" => "image/png",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".bmp" => "image/bmp",
                _ => "application/octet-stream"
            };
        }

        private int ResolveEffectiveUserId(int routeUserId)
        {
            var sessionUserId = HttpContext.Session.GetInt32("UserId");
            if (sessionUserId.HasValue && sessionUserId.Value > 0)
            {
                return sessionUserId.Value;
            }

            var claimValue = User?.FindFirstValue(ClaimTypes.NameIdentifier);
            if (int.TryParse(claimValue, out var claimUserId) && claimUserId > 0)
            {
                return claimUserId;
            }

            return routeUserId;
        }

        public sealed class UpdateBusinessProfileDto
        {
            public string? BusinessName { get; set; }
            public string? BusinessType { get; set; }
            public string? BusinessEmail { get; set; }
            public string? BusinessPhone { get; set; }
            public string? TaxId { get; set; }
            public string? BusinessAddress { get; set; }
        }

        public sealed class ChangePasswordDto
        {
            public string? CurrentPassword { get; set; }
            public string? NewPassword { get; set; }
            public string? ConfirmNewPassword { get; set; }
        }
    }
}
