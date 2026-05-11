using System.Data;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MyAspNetApp.Data;
using MyAspNetApp.Models;
using MyAspNetApp.Models.ViewModels;
using MyAspNetApp.Services;

namespace MyAspNetApp.Controllers
{
    public class AccountProfileController : Controller
    {
        private const string SharedUserIdCookie = "NextHorizon.SharedUserId";

        private readonly AppDbContext _dbContext;
        private readonly OrderService _orderService;
        private readonly IWebHostEnvironment _environment;
        private readonly MediaPathService _mediaPathService;

        public AccountProfileController(AppDbContext dbContext, OrderService orderService, IWebHostEnvironment environment, MediaPathService mediaPathService)
        {
            _dbContext = dbContext;
            _orderService = orderService;
            _environment = environment;
            _mediaPathService = mediaPathService;
        }

        public async Task<IActionResult> ProfileView(CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(ProfileView), "AccountProfile") });
            }

            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId.Value, cancellationToken);
            if (user is null)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(ProfileView), "AccountProfile") });
            }

            var consumer = await _dbContext.Consumers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId.Value, cancellationToken);

            var activityRecords = await _dbContext.LeaderboardRecords
                .AsNoTracking()
                .Where(x => x.UserId == userId.Value)
                .OrderByDescending(x => x.ActivityDate ?? x.CreatedAtUtc)
                .ThenByDescending(x => x.CreatedAtUtc)
                .Take(20)
                .ToListAsync(cancellationToken);

            var todayLocal = DateTime.Today;
            var todayRecords = activityRecords
                .Where(x => (x.ActivityDate ?? x.CreatedAtUtc).Date == todayLocal)
                .ToList();

            var rankedRecords = await _dbContext.LeaderboardRecords
                .AsNoTracking()
                .Where(x => x.IsActive)
                .OrderByDescending(x => x.DistanceKm)
                .ThenBy(x => x.DurationSeconds)
                .Select(x => new { x.UserId })
                .ToListAsync(cancellationToken);

            var globalRankIndex = rankedRecords.FindIndex(x => x.UserId == userId.Value);

            var displayName = BuildDisplayName(user.Email, consumer?.Username, consumer?.FullName, userId.Value);
            var username = !string.IsNullOrWhiteSpace(consumer?.Username)
                ? consumer.Username!.Trim()
                : displayName;

            var model = new ProfileViewPageViewModel
            {
                DisplayName = displayName,
                Username = username.ToUpperInvariant(),
                Motto = username,
                AvatarUrl = BuildProfileImageUrl(user),
                GlobalRankText = globalRankIndex >= 0 ? ToOrdinal(globalRankIndex + 1) : "Unranked",
                TodayDistanceKm = todayRecords.Sum(x => x.DistanceKm),
                TodayTimeSeconds = todayRecords.Sum(x => x.DurationSeconds),
                TodaySteps = EstimateSteps(todayRecords.Sum(x => x.DistanceKm)),
                RecentActivities = activityRecords
                    .Take(2)
                    .Select(x => new ProfileActivityCardViewModel
                    {
                        ImageUrl = NormalizeImageUrl(x.CoverImageUrl),
                        DistanceKm = x.DistanceKm,
                        DurationSeconds = x.DurationSeconds,
                        EstimatedSteps = EstimateSteps(x.DistanceKm),
                        RankText = globalRankIndex >= 0 ? ToOrdinal(globalRankIndex + 1) : "Unranked",
                        ActivityDate = x.ActivityDate ?? x.CreatedAtUtc
                    })
                    .ToList()
            };

            return View(model);
        }

        public async Task<IActionResult> UpdateProfile(CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(UpdateProfile), "AccountProfile") });
            }

            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId.Value, cancellationToken);
            if (user is null)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(UpdateProfile), "AccountProfile") });
            }

            var consumer = await _dbContext.Consumers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId.Value, cancellationToken);

            var username = !string.IsNullOrWhiteSpace(consumer?.Username)
                ? consumer.Username!.Trim()
                : BuildDisplayName(user.Email, consumer?.Username, consumer?.FullName, userId.Value);

            var model = new UpdateProfilePageViewModel
            {
                FullName = consumer?.FullName ?? string.Empty,
                Username = username,
                Motto = username,
                Gender = consumer?.Gender ?? string.Empty,
                Birthday = consumer?.Birthday,
                PhoneNumber = consumer?.PhoneNumber ?? string.Empty,
                Email = user.Email ?? string.Empty
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> UpdateProfileField([FromBody] UpdateProfileFieldRequest request, CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { success = false, message = "Login required." });
            }

            var user = await _dbContext.Users.FirstOrDefaultAsync(x => x.UserId == userId.Value, cancellationToken);
            if (user is null)
            {
                return Unauthorized(new { success = false, message = "Login required." });
            }

            var consumer = await _dbContext.Consumers.FirstOrDefaultAsync(x => x.UserId == userId.Value, cancellationToken);
            if (consumer is null)
            {
                consumer = new Consumer
                {
                    UserId = userId.Value,
                    CreatedAt = DateTime.Now
                };
                _dbContext.Consumers.Add(consumer);
            }

            var value = (request.Value ?? string.Empty).Trim();
            switch ((request.Field ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "fullname":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return BadRequest(new { success = false, message = "Full name is required." });
                    }

                    ApplyFullName(consumer, value);
                    value = consumer.FullName;
                    break;

                case "username":
                case "motto":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return BadRequest(new { success = false, message = "Username is required." });
                    }

                    consumer.Username = value;
                    break;

                case "phone":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return BadRequest(new { success = false, message = "Phone number is required." });
                    }

                    consumer.PhoneNumber = value;
                    break;

                case "gender":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        consumer.Gender = null;
                        value = string.Empty;
                        break;
                    }

                    consumer.Gender = value;
                    break;

                case "birthday":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        consumer.Birthday = null;
                        value = string.Empty;
                        break;
                    }

                    if (!DateTime.TryParse(value, out var birthday))
                    {
                        return BadRequest(new { success = false, message = "Enter a valid birthday." });
                    }

                    consumer.Birthday = birthday.Date;
                    value = birthday.ToString("yyyy-MM-dd");
                    break;

                case "email":
                    if (string.IsNullOrWhiteSpace(value))
                    {
                        return BadRequest(new { success = false, message = "Email is required." });
                    }

                    user.Email = value;
                    break;

                default:
                    return BadRequest(new { success = false, message = "This field is not connected to the database." });
            }

            await _dbContext.SaveChangesAsync(cancellationToken);
            return Ok(new { success = true, value });
        }

        public IActionResult UploadActivity()
        {
            return View();
        }

        public IActionResult MyActivity()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> UploadMemberActivity([FromForm] MemberActivityUploadRequest request, CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { success = false, message = "Login required." });
            }

            var consumerId = await ResolveCurrentConsumerIdAsync(userId.Value, cancellationToken);
            if (!consumerId.HasValue)
            {
                return Unauthorized(new { success = false, message = "Consumer profile not found for this account." });
            }

            var activityName = (request.ActivityName ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(activityName))
            {
                return BadRequest(new { success = false, message = "Select an activity." });
            }

            if (request.ActivityDate == default)
            {
                return BadRequest(new { success = false, message = "Select an activity date." });
            }

            if (request.DistanceKm <= 0)
            {
                return BadRequest(new { success = false, message = "Distance must be greater than zero." });
            }

            if (request.MovingTime <= 0)
            {
                return BadRequest(new { success = false, message = "Moving time must be greater than zero." });
            }

            if (request.Steps is < 1 or > 100000)
            {
                return BadRequest(new { success = false, message = "Steps must be between 1 and 100000." });
            }

            if (request.ProofFile is null || request.ProofFile.Length == 0)
            {
                return BadRequest(new { success = false, message = "Upload a screenshot proof image." });
            }

            string? proofUrl;
            try
            {
                proofUrl = await SaveActivityProofAsync(request.ProofFile, cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
            var athleteName = await ResolveAthleteNameAsync(userId.Value, cancellationToken);
            var roundedDistance = decimal.Round(request.DistanceKm, 2, MidpointRounding.AwayFromZero);
            var pace = roundedDistance > 0
                ? (int)Math.Round(request.MovingTime / (double)roundedDistance, MidpointRounding.AwayFromZero)
                : 0;
            var activityTitle = $"{activityName} - {request.ActivityDate:yyyy-MM-dd}";

            await using var connection = _dbContext.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    INSERT INTO dbo.MemberUploads
                        (UserId, Title, ActivityName, ActivityDate, ProofUrl, DistanceKm, MovingTimeSec, Steps, AvgPaceSecPerKm, CreatedAt)
                    OUTPUT INSERTED.UploadId
                    VALUES
                        (@UserId, @Title, @ActivityName, @ActivityDate, @ProofUrl, @DistanceKm, @MovingTimeSec, @Steps, @AvgPaceSecPerKm, SYSUTCDATETIME());
                    """;
                AddParameter(command, "@UserId", consumerId.Value, DbType.Int32);
                AddParameter(command, "@Title", activityTitle, DbType.String);
                AddParameter(command, "@ActivityName", activityName, DbType.String);
                AddParameter(command, "@ActivityDate", request.ActivityDate.Date, DbType.Date);
                AddParameter(command, "@ProofUrl", proofUrl, DbType.String);
                AddParameter(command, "@DistanceKm", roundedDistance, DbType.Decimal);
                AddParameter(command, "@MovingTimeSec", request.MovingTime, DbType.Int32);
                AddParameter(command, "@Steps", request.Steps, DbType.Int32);
                AddParameter(command, "@AvgPaceSecPerKm", pace, DbType.Int32);

                var uploadId = Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken), CultureInfo.InvariantCulture);

                await using var leaderboardCommand = connection.CreateCommand();
                leaderboardCommand.CommandText =
                    """
                    INSERT INTO dbo.leaderboard_records
                        (UploadId, UserId, AthleteName, AvatarUrl, CoverImageUrl, DistanceKm, DurationSeconds, Scope, CategoryLabel, RankChange, IsVerified, IsActive, CreatedAtUtc, ActivityDate)
                    VALUES
                        (@UploadId, @UserId, @AthleteName, NULL, @CoverImageUrl, @DistanceKm, @DurationSeconds, N'National', N'Current Season', 0, 1, 1, SYSUTCDATETIME(), @ActivityDate);
                    """;
                AddParameter(leaderboardCommand, "@UploadId", uploadId, DbType.Int32);
                AddParameter(leaderboardCommand, "@UserId", userId.Value, DbType.Int32);
                AddParameter(leaderboardCommand, "@AthleteName", athleteName, DbType.String);
                AddParameter(leaderboardCommand, "@CoverImageUrl", proofUrl, DbType.String);
                AddParameter(leaderboardCommand, "@DistanceKm", roundedDistance, DbType.Decimal);
                AddParameter(leaderboardCommand, "@DurationSeconds", request.MovingTime, DbType.Int32);
                AddParameter(leaderboardCommand, "@ActivityDate", request.ActivityDate.Date, DbType.DateTime);
                await leaderboardCommand.ExecuteNonQueryAsync(cancellationToken);

                return Ok(new { success = true, uploadId });
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }

        [HttpGet]
        public async Task<IActionResult> MemberActivities(string? sort, string? dateFilter, CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized(new { success = false, message = "Login required." });
            }

            var consumerId = await ResolveCurrentConsumerIdAsync(userId.Value, cancellationToken);
            if (!consumerId.HasValue)
            {
                return Unauthorized(new { success = false, message = "Consumer profile not found for this account." });
            }

            await using var connection = _dbContext.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT UploadId, Title, ActivityName, ActivityDate, ProofUrl, DistanceKm, MovingTimeSec, Steps, AvgPaceSecPerKm, CreatedAt
                    FROM dbo.MemberUploads
                    WHERE UserId = @UserId
                    ORDER BY CreatedAt DESC;
                    """;
                AddParameter(command, "@UserId", consumerId.Value, DbType.Int32);

                var activities = new List<MemberActivityDto>();
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var distance = GetDecimal(reader, "DistanceKm") ?? 0m;
                    var duration = GetInt32(reader, "MovingTimeSec") ?? 0;
                    activities.Add(new MemberActivityDto
                    {
                        UploadId = GetInt32(reader, "UploadId") ?? 0,
                        Title = GetString(reader, "Title", "Activity") ?? "Activity",
                        ActivityName = GetString(reader, "ActivityName", "Activity") ?? "Activity",
                        ActivityDate = GetDateTime(reader, "ActivityDate") ?? DateTime.Today,
                        DistanceKm = distance,
                        MovingTimeSec = duration,
                        Steps = GetInt32(reader, "Steps") ?? EstimateSteps(distance),
                        AvgPaceSecPerKm = GetInt32(reader, "AvgPaceSecPerKm") ?? CalculatePaceSeconds(distance, duration),
                        CreatedAt = GetDateTime(reader, "CreatedAt") ?? DateTime.UtcNow
                    });

                    activities[^1].ProofUrl = Url.Action(nameof(MemberActivityProof), "AccountProfile", new { uploadId = activities[^1].UploadId }) ?? string.Empty;
                }

                activities = ApplyActivityDateFilter(activities, dateFilter).ToList();
                activities = ApplyActivitySort(activities, sort).ToList();

                return Ok(new { success = true, activities });
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }

        [HttpGet]
        public async Task<IActionResult> MemberActivityProof(int uploadId, CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized();
            }

            var consumerId = await ResolveCurrentConsumerIdAsync(userId.Value, cancellationToken);
            if (!consumerId.HasValue)
            {
                return Unauthorized();
            }

            if (uploadId <= 0)
            {
                return NotFound();
            }

            await using var connection = _dbContext.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT TOP (1) ProofUrl
                    FROM dbo.MemberUploads
                    WHERE UploadId = @UploadId
                      AND UserId = @UserId;
                    """;
                AddParameter(command, "@UploadId", uploadId, DbType.Int32);
                AddParameter(command, "@UserId", consumerId.Value, DbType.Int32);

                var proofUrl = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken));
                if (string.IsNullOrWhiteSpace(proofUrl))
                {
                    return NotFound();
                }

                var localPath = _mediaPathService.ResolveLocalPath(proofUrl);
                if (!string.IsNullOrWhiteSpace(localPath))
                {
                    return PhysicalFile(localPath, _mediaPathService.GetContentType(localPath));
                }

                var normalizedUrl = NormalizeImageUrl(proofUrl);
                if (!string.IsNullOrWhiteSpace(normalizedUrl))
                {
                    return Redirect(normalizedUrl);
                }

                return NotFound();
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }

        public async Task<IActionResult> ShippingAddress(CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(ShippingAddress), "AccountProfile") });
            }

            var consumer = await _dbContext.Consumers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId.Value, cancellationToken);

            var model = new ShippingAddressPageViewModel();
            if (consumer is not null && !string.IsNullOrWhiteSpace(consumer.Address))
            {
                model.DefaultAddress = new CheckoutAddressOption
                {
                    Label = "Default Address",
                    FullName = consumer.FullName,
                    Phone = consumer.PhoneNumber ?? string.Empty,
                    Address = consumer.Address.Trim(),
                    IsDefault = true
                };
            }

            return View(model);
        }

        public IActionResult PaymentMethods()
        {
            return View();
        }

        public async Task<IActionResult> MyPurchases(CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return RedirectToAction("Login", "Account", new { returnUrl = Url.Action(nameof(MyPurchases), "AccountProfile") });
            }

            var consumerId = await _dbContext.Consumers
                .AsNoTracking()
                .Where(x => x.UserId == userId.Value)
                .Select(x => (int?)x.ConsumerId)
                .FirstOrDefaultAsync(cancellationToken);

            var orders = await _orderService.GetUserPurchasesAsync(userId, consumerId, cancellationToken);
            return View(orders);
        }

        public async Task<IActionResult> OrderItemImage(int orderItemId, int? variantId, CancellationToken cancellationToken)
        {
            var userId = GetCurrentUserId();
            if (!userId.HasValue)
            {
                return Unauthorized();
            }

            var consumerId = await _dbContext.Consumers
                .AsNoTracking()
                .Where(x => x.UserId == userId.Value)
                .Select(x => (int?)x.ConsumerId)
                .FirstOrDefaultAsync(cancellationToken);

            await using var connection = _dbContext.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText =
                    """
                    SELECT TOP (1)
                        oi.ProductImageData,
                        oi.ProductImageMimeType,
                        oi.ProductImage,
                        pv.ImageData AS VariantImageData,
                        pv.ImageMimeType AS VariantImageMimeType,
                        pv.ImagePath AS VariantImagePath,
                        p.ImagePath AS ProductImagePath
                    FROM dbo.OrderItems oi
                    INNER JOIN dbo.Orders o ON o.OrderID = oi.OrderID
                    LEFT JOIN dbo.ProductVariants pv ON
                        (
                            @VariantId IS NOT NULL
                            AND pv.VariantId = @VariantId
                        )
                        OR
                        (
                            @VariantId IS NULL
                            AND pv.ProductId = oi.ProductID
                            AND (oi.Size IS NULL OR LTRIM(RTRIM(CONVERT(NVARCHAR(100), pv.Size))) = LTRIM(RTRIM(CONVERT(NVARCHAR(100), oi.Size))))
                            AND (oi.Color IS NULL OR LTRIM(RTRIM(CONVERT(NVARCHAR(100), pv.Style))) = LTRIM(RTRIM(CONVERT(NVARCHAR(100), oi.Color))))
                        )
                    LEFT JOIN dbo.Products p ON p.ProductId = oi.ProductID
                    WHERE oi.OrderItemID = @OrderItemId
                      AND (
                          LTRIM(RTRIM(CONVERT(NVARCHAR(50), o.UserID))) = @UserIdText
                          OR (@ConsumerId IS NOT NULL AND o.ConsumerID = @ConsumerId)
                      )
                    ORDER BY pv.VariantId;
                    """;
                command.CommandType = CommandType.Text;
                AddParameter(command, "@OrderItemId", orderItemId, DbType.Int32);
                AddParameter(command, "@VariantId", variantId, DbType.Int32);
                AddParameter(command, "@UserIdText", userId.Value.ToString(), DbType.String);
                AddParameter(command, "@ConsumerId", consumerId, DbType.Int32);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    return NotFound();
                }

                var orderItemData = GetBytes(reader, "ProductImageData");
                if (orderItemData is { Length: > 0 })
                {
                    return File(orderItemData, GetString(reader, "ProductImageMimeType", "image/jpeg") ?? "image/jpeg");
                }

                var variantData = GetBytes(reader, "VariantImageData");
                if (variantData is { Length: > 0 })
                {
                    return File(variantData, GetString(reader, "VariantImageMimeType", "image/jpeg") ?? "image/jpeg");
                }

                var fallbackPath = NormalizeImageUrl(
                    GetString(reader, "ProductImage")
                    ?? GetString(reader, "VariantImagePath")
                    ?? GetString(reader, "ProductImagePath"));

                return string.IsNullOrWhiteSpace(fallbackPath)
                    ? Redirect("/images/placeholder.png")
                    : Redirect(fallbackPath);
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }

        private int? GetCurrentUserId()
        {
            var sessionUserId = HttpContext.Session.GetInt32("UserId");
            if (sessionUserId.HasValue)
            {
                return sessionUserId.Value;
            }

            if (int.TryParse(Request.Cookies[SharedUserIdCookie], out var cookieUserId))
            {
                return cookieUserId;
            }

            return null;
        }

        private async Task<int?> ResolveCurrentConsumerIdAsync(int userId, CancellationToken cancellationToken)
        {
            return await _dbContext.Consumers
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => (int?)x.ConsumerId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private static void AddParameter(System.Data.Common.DbCommand command, string name, object? value, DbType dbType)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.DbType = dbType;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        private static string? GetString(System.Data.Common.DbDataReader reader, string columnName, string? fallback = null)
        {
            var ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal))
            {
                return fallback;
            }

            var value = reader.GetValue(ordinal)?.ToString();
            return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        }

        private static byte[]? GetBytes(System.Data.Common.DbDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            return reader.GetValue(ordinal) switch
            {
                byte[] value when value.Length > 0 => value,
                _ => null
            };
        }

        private static int? GetInt32(System.Data.Common.DbDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            return reader.GetValue(ordinal) switch
            {
                int value => value,
                long value => Convert.ToInt32(value, CultureInfo.InvariantCulture),
                short value => value,
                byte value => value,
                decimal value => Convert.ToInt32(value, CultureInfo.InvariantCulture),
                string value when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => null
            };
        }

        private static decimal? GetDecimal(System.Data.Common.DbDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            return reader.GetValue(ordinal) switch
            {
                decimal value => value,
                double value => Convert.ToDecimal(value, CultureInfo.InvariantCulture),
                float value => Convert.ToDecimal(value, CultureInfo.InvariantCulture),
                int value => value,
                long value => value,
                string value when decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) => parsed,
                _ => null
            };
        }

        private static DateTime? GetDateTime(System.Data.Common.DbDataReader reader, string columnName)
        {
            var ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal))
            {
                return null;
            }

            return reader.GetValue(ordinal) switch
            {
                DateTime value => value,
                DateOnly value => value.ToDateTime(TimeOnly.MinValue),
                string value when DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed) => parsed,
                _ => null
            };
        }

        private static string BuildDisplayName(string? email, string? username, string? fullName, int userId)
        {
            if (!string.IsNullOrWhiteSpace(username))
            {
                return username.Trim();
            }

            if (!string.IsNullOrWhiteSpace(fullName))
            {
                return fullName.Trim();
            }

            if (!string.IsNullOrWhiteSpace(email))
            {
                var atIndex = email.IndexOf('@');
                return atIndex > 0 ? email[..atIndex] : email;
            }

            return $"User {userId}";
        }

        private static string? BuildProfileImageUrl(MyAspNetApp.Models.User user)
        {
            if (user.ProfilePicture is not { Length: > 0 })
            {
                return null;
            }

            var contentType = string.IsNullOrWhiteSpace(user.ProfilePictureContentType)
                ? "image/jpeg"
                : user.ProfilePictureContentType.Trim();

            return $"data:{contentType};base64,{Convert.ToBase64String(user.ProfilePicture)}";
        }

        private static void ApplyFullName(Consumer consumer, string fullName)
        {
            var parts = fullName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            consumer.FirstName = parts.Length > 0 ? parts[0] : null;
            consumer.MiddleName = parts.Length > 2 ? string.Join(" ", parts.Skip(1).Take(parts.Length - 2)) : null;
            consumer.LastName = parts.Length > 1 ? parts[^1] : null;
        }

        private static string? NormalizeImageUrl(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            if (trimmed.StartsWith("http://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("https://", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("data:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("/", StringComparison.Ordinal))
            {
                return trimmed;
            }

            return "/" + trimmed.Replace("\\", "/").TrimStart('/');
        }

        private static int EstimateSteps(decimal distanceKm)
        {
            if (distanceKm <= 0)
            {
                return 0;
            }

            return (int)Math.Round((double)(distanceKm * 1312m), MidpointRounding.AwayFromZero);
        }

        private static int CalculatePaceSeconds(decimal distanceKm, int movingTimeSec)
        {
            if (distanceKm <= 0 || movingTimeSec <= 0)
            {
                return 0;
            }

            return (int)Math.Round(movingTimeSec / (double)distanceKm, MidpointRounding.AwayFromZero);
        }

        private async Task<string?> SaveActivityProofAsync(IFormFile? file, CancellationToken cancellationToken)
        {
            if (file is null || file.Length == 0)
            {
                return null;
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (extension is not ".jpg" and not ".jpeg" and not ".png" and not ".webp")
            {
                throw new InvalidOperationException("Proof image must be JPG, PNG, or WEBP.");
            }

            if (file.Length > 5 * 1024 * 1024)
            {
                throw new InvalidOperationException("Proof image must be 5MB or smaller.");
            }

            var webRoot = string.IsNullOrWhiteSpace(_environment.WebRootPath)
                ? Path.Combine(_environment.ContentRootPath, "wwwroot")
                : _environment.WebRootPath;
            var uploadDirectory = Path.Combine(webRoot, "uploads", "proofs");
            Directory.CreateDirectory(uploadDirectory);

            var fileName = $"{Guid.NewGuid():N}{extension}";
            var filePath = Path.Combine(uploadDirectory, fileName);
            await using var stream = System.IO.File.Create(filePath);
            await file.CopyToAsync(stream, cancellationToken);

            return $"/uploads/proofs/{fileName}";
        }

        private async Task<string> ResolveAthleteNameAsync(int userId, CancellationToken cancellationToken)
        {
            var user = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
            var consumer = await _dbContext.Consumers
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

            return BuildDisplayName(user?.Email, consumer?.Username, consumer?.FullName, userId);
        }

        private static IEnumerable<MemberActivityDto> ApplyActivityDateFilter(IEnumerable<MemberActivityDto> activities, string? dateFilter)
        {
            var today = DateTime.Today;
            return (dateFilter ?? "all").Trim().ToLowerInvariant() switch
            {
                "today" => activities.Where(x => x.ActivityDate.Date == today),
                "thisweek" => activities.Where(x => x.ActivityDate.Date >= today.AddDays(-(int)today.DayOfWeek)),
                "thismonth" => activities.Where(x => x.ActivityDate.Year == today.Year && x.ActivityDate.Month == today.Month),
                "last7days" => activities.Where(x => x.ActivityDate.Date >= today.AddDays(-7)),
                "last30days" => activities.Where(x => x.ActivityDate.Date >= today.AddDays(-30)),
                _ => activities
            };
        }

        private static IEnumerable<MemberActivityDto> ApplyActivitySort(IEnumerable<MemberActivityDto> activities, string? sort)
        {
            return (sort ?? "createdAt_desc").Trim().ToLowerInvariant() switch
            {
                "activitydate_desc" => activities.OrderByDescending(x => x.ActivityDate),
                "longestdistance" => activities.OrderByDescending(x => x.DistanceKm),
                "bestpace" => activities.OrderBy(x => x.AvgPaceSecPerKm <= 0 ? int.MaxValue : x.AvgPaceSecPerKm),
                _ => activities.OrderByDescending(x => x.CreatedAt)
            };
        }

        private static string ToOrdinal(int number)
        {
            var abs = Math.Abs(number);
            var lastTwo = abs % 100;
            if (lastTwo is 11 or 12 or 13)
            {
                return $"{number}th";
            }

            return (abs % 10) switch
            {
                1 => $"{number}st",
                2 => $"{number}nd",
                3 => $"{number}rd",
                _ => $"{number}th"
            };
        }

        public sealed class UpdateProfileFieldRequest
        {
            public string? Field { get; set; }
            public string? Value { get; set; }
        }

        public sealed class MemberActivityUploadRequest
        {
            public string? ActivityName { get; set; }
            public DateTime ActivityDate { get; set; }
            public int MovingTime { get; set; }
            public decimal DistanceKm { get; set; }
            public int Steps { get; set; }
            public IFormFile? ProofFile { get; set; }
        }

        public sealed class MemberActivityDto
        {
            public int UploadId { get; set; }
            public string Title { get; set; } = string.Empty;
            public string ActivityName { get; set; } = string.Empty;
            public DateTime ActivityDate { get; set; }
            public string ProofUrl { get; set; } = string.Empty;
            public decimal DistanceKm { get; set; }
            public int MovingTimeSec { get; set; }
            public int Steps { get; set; }
            public int AvgPaceSecPerKm { get; set; }
            public DateTime CreatedAt { get; set; }
        }
    }
}
