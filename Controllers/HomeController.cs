using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MyAspNetApp.Data;
using MyAspNetApp.Models;
using MyAspNetApp.Security;
using MyAspNetApp.Models.ViewModels;
using MyAspNetApp.Services;
using System.Text.Json;
using System.Data;
using System.Data.Common;
using Microsoft.Data.SqlClient;

namespace MyAspNetApp.Controllers
{
    public class HomeController : Controller
    {
        private const string SharedUserIdCookie = "NextHorizon.SharedUserId";
        private const string SharedUserEmailCookie = "NextHorizon.SharedUserEmail";
        private const string SharedUserTypeCookie = "NextHorizon.SharedUserType";
        private const string SharedDisplayNameCookie = "NextHorizon.SharedDisplayName";
        private const string LoginAppUrl = "/Account/Login";
        private const string LandingPageUrl = "/";
        private const string UploadActivityAppUrl = "/AccountProfile/UploadActivity";
        private static readonly string[] SessionCookiesToClear =
        {
            ".AspNetCore.Session",
            ".NextHorizon.Session",
            ".NextHorizon.Products.Session",
            ".NextHorizon.Login.Session"
        };
        private const string CheckoutAppUrl = "/Home/Checkout";
        private const string ProfileAppUrl = "/AccountProfile/ProfileView";
        private const string MyPurchasesAppUrl = "/AccountProfile/MyPurchases";
        private readonly AppDbContext _dbContext;
        private readonly IWebHostEnvironment _environment;
        private readonly LeaderboardService _leaderboardService;
        private readonly MediaPathService _mediaPathService;
        private readonly ChallengeNotificationService _challengeNotificationService;
        private readonly IAuthenticatedUserContextService _userContextService;
        private readonly ILogger<HomeController> _logger;

        public HomeController(AppDbContext dbContext, IWebHostEnvironment environment, LeaderboardService leaderboardService, MediaPathService mediaPathService, ChallengeNotificationService challengeNotificationService, IAuthenticatedUserContextService userContextService, ILogger<HomeController> logger)
        {
            _dbContext = dbContext;
            _environment = environment;
            _leaderboardService = leaderboardService;
            _mediaPathService = mediaPathService;
            _challengeNotificationService = challengeNotificationService;
            _userContextService = userContextService;
            _logger = logger;
        }

        // GET: /
        public async Task<IActionResult> Index(CancellationToken cancellationToken)
        {
            var products = new List<Product>();

            try
            {
                var dbProducts = await _dbContext.Products
                    .AsNoTracking()
                    .Where(p => p.Status == null || p.Status.ToLower() == "active")
                    .OrderBy(p => p.ProductId)
                    .Take(44)
                    .ToListAsync(cancellationToken);

                var productIds = dbProducts.Select(p => p.ProductId).ToList();

                var variants = productIds.Count == 0
                    ? new List<DbProductVariant>()
                    : await _dbContext.ProductVariants
                        .AsNoTracking()
                        .Where(v => productIds.Contains(v.ProductId))
                        .ToListAsync(cancellationToken);

                products = dbProducts.Select(p =>
                {
                    var myVariants = variants.Where(v => v.ProductId == p.ProductId).ToList();

                    var imagePath = myVariants
                        .Where(v => !string.IsNullOrEmpty(v.ImagePath))
                        .Select(v => v.ImagePath!)
                        .FirstOrDefault() ?? string.Empty;

                    var totalStock = myVariants.Sum(v => v.Quantity);

                    var variantPrice = myVariants
                        .Where(v => v.Price.HasValue)
                        .Select(v => v.Price!.Value)
                        .FirstOrDefault();

                    return new Product
                    {
                        Id = p.ProductId,
                        Name = p.ProductName,
                        Description = p.Details ?? string.Empty,
                        Price = variantPrice > 0 ? variantPrice : 0m,
                        Image = imagePath,
                        Images = myVariants
                            .Where(v => !string.IsNullOrWhiteSpace(v.ImagePath))
                            .Select(v => v.ImagePath!)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList(),
                        Sizes = myVariants
                            .Where(v => !string.IsNullOrWhiteSpace(v.Size))
                            .Select(v => v.Size)
                            .Distinct(StringComparer.OrdinalIgnoreCase)
                            .ToList(),
                        Stock = totalStock,
                        Brand = p.Brand ?? string.Empty,
                        Category = p.Category ?? string.Empty,
                        Gender = p.Gender ?? "Unisex"
                    };
                }).ToList();
            }
            catch (Exception ex) when (IsDatabaseConnectionException(ex))
            {
                _logger.LogWarning(ex, "Unable to load landing page products from the database. Rendering the landing page without products.");
            }

            if (products.Count == 0)
            {
                products = ProductData.Products.Take(44).ToList();
            }

            var viewModel = new LandingPageViewModel
            {
                ShowLoginWall = HttpContext.Session.GetInt32("UserId") == null,
                Products = products
            };

            return View(viewModel);
        }

        private static bool IsDatabaseConnectionException(Exception ex)
        {
            if (ex is SqlException or TimeoutException)
            {
                return true;
            }

            return ex is InvalidOperationException
                && ex.Message.Contains("Timeout expired", StringComparison.OrdinalIgnoreCase);
        }

        public async Task<IActionResult> Storefront(CancellationToken cancellationToken)
        {
            if (IsSeller())
            {
                return RedirectToAction("Index", "Seller");
            }

            try
            {
                var model = await _leaderboardService.GetLeaderboardAsync(6, cancellationToken);
                return View(model);
            }
            catch (Exception ex) when (ex is SqlException || ex is TimeoutException)
            {
                _logger.LogWarning(ex, "Unable to load leaderboard for storefront due to database connectivity issues. Rendering storefront without leaderboard.");
                return View(new Models.ViewModels.LeaderboardPageViewModel());
            }
        }

        [HttpGet]
        public async Task<IActionResult> GetPromotions()
        {
            try
            {
                var promos = await _dbContext.Promotions
                    .Where(p => p.Status == "Approved")
                    .ToListAsync();

                return Json(promos);
            }
            catch (Exception ex) when (IsDatabaseConnectionException(ex))
            {
                _logger.LogWarning(ex, "Unable to load promotions due to database connectivity issues. Returning empty list.");
                return Json(new List<object>());
            }
        }

        public IActionResult SearchPage(string? query, string? tag)
        {
            ViewData["Query"] = query ?? string.Empty;
            ViewData["Tag"] = tag ?? string.Empty;
            return View();
        }

        [HttpGet]
        public async Task<IActionResult> GetLeaderboard(
            string scope = "National",
            int take = 1000)
        {
            try
            {
                var query = _dbContext.LeaderboardRecords
                    .Where(r => r.IsActive);

                if (!string.Equals(scope, "All", StringComparison.OrdinalIgnoreCase))
                {
                    query = query.Where(r => r.Scope == scope);
                }

                var results = await query
                    .OrderByDescending(r => r.DistanceKm)
                    .ThenBy(r => r.DurationSeconds)
                    .Take(Math.Clamp(take, 1, 1000))
                    .Select(r => new
                    {
                        r.Id,
                        r.AthleteName,
                        r.AvatarUrl,
                        r.DistanceKm,
                        r.DurationSeconds,
                        r.Scope,
                        r.CategoryLabel,
                        r.ActivityDate,
                        r.RankChange
                    })
                    .ToListAsync();

                if (results.Count == 0)
                {
                    var uploadResults = await GetLeaderboardFromMemberUploadsAsync(Math.Clamp(take, 1, 1000));
                    if (uploadResults.Count > 0)
                    {
                        return Json(uploadResults);
                    }
                }

                return Json(results);
            }
            catch (Exception ex) when (IsDatabaseConnectionException(ex))
            {
                _logger.LogWarning(ex, "Unable to load leaderboard due to database connectivity issues. Returning empty list.");
                return Json(new List<object>());
            }
        }

        private async Task<List<object>> GetLeaderboardFromMemberUploadsAsync(int take)
        {
            var results = new List<object>();
            var connection = _dbContext.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;

            if (shouldClose)
            {
                await connection.OpenAsync();
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = @"
SELECT TOP (@take)
    mu.UploadId,
    mu.UserId,
    COALESCE(
        NULLIF(LTRIM(RTRIM(c.username)), ''),
        NULLIF(LTRIM(RTRIM(CONCAT(COALESCE(c.first_name, ''), ' ', COALESCE(c.last_name, '')))), ''),
        CONCAT(N'Runner ', mu.UserId)
    ) AS AthleteName,
    CAST(NULL AS nvarchar(500)) AS AvatarUrl,
    mu.DistanceKm,
    mu.MovingTimeSec AS DurationSeconds,
    N'All' AS Scope,
    N'Uploaded Activities' AS CategoryLabel,
    COALESCE(mu.ActivityDate, mu.CreatedAt) AS ActivityDate,
    0 AS RankChange
FROM dbo.MemberUploads mu
LEFT JOIN dbo.Consumers c
    ON c.user_id = mu.UserId
WHERE mu.DistanceKm > 0
ORDER BY mu.DistanceKm DESC,
    CASE WHEN mu.MovingTimeSec <= 0 THEN 2147483647 ELSE mu.MovingTimeSec END ASC,
    COALESCE(mu.ActivityDate, mu.CreatedAt) DESC,
    mu.UploadId DESC;";

                var takeParameter = command.CreateParameter();
                takeParameter.ParameterName = "@take";
                takeParameter.Value = take;
                takeParameter.DbType = DbType.Int32;
                command.Parameters.Add(takeParameter);

                await using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    results.Add(new
                    {
                        UploadId = reader.GetInt32(0),
                        UserId = reader.GetInt32(1),
                        AthleteName = reader.IsDBNull(2) ? $"Runner {reader.GetInt32(1)}" : reader.GetString(2),
                        AvatarUrl = reader.IsDBNull(3) ? null : reader.GetString(3),
                        DistanceKm = reader.GetDecimal(4),
                        DurationSeconds = reader.GetInt32(5),
                        Scope = reader.GetString(6),
                        CategoryLabel = reader.GetString(7),
                        ActivityDate = reader.GetDateTime(8),
                        RankChange = reader.GetInt32(9)
                    });
                }
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }

            return results;
        }

        [HttpGet]
        public async Task<IActionResult> SearchSuggestions(string? query)
        {
            if (string.IsNullOrWhiteSpace(query) || query.Length < 1)
            {
                return Json(new List<object>());
            }

            var q = query.Trim().ToLower();

            var matches = await _dbContext.Products
                .Where(p =>
                    p.Status == "active" &&
                    (p.ProductName.ToLower().Contains(q) ||
                     (p.Category != null && p.Category.ToLower().Contains(q)) ||
                     (p.Brand != null && p.Brand.ToLower().Contains(q)) ||
                     (p.Gender != null && p.Gender.ToLower().Contains(q))))
                .Select(p => new { p.ProductName, p.Category, p.Gender })
                .Distinct()
                .Take(50)
                .ToListAsync();

            var suggestions = new List<string>();

            foreach (var item in matches)
            {
                if (!string.IsNullOrEmpty(item.Gender) && item.Gender != "Unisex")
                {
                    suggestions.Add($"{item.ProductName} for {item.Gender}");
                }
                else
                {
                    suggestions.Add(item.ProductName);
                }

                if (!string.IsNullOrEmpty(item.Category) && item.Category.ToLower().Contains(q))
                {
                    var catSuggestion = string.IsNullOrEmpty(item.Gender) || item.Gender == "Unisex"
                        ? item.Category
                        : $"{item.Category} for {item.Gender}";
                    suggestions.Add(catSuggestion);
                }
            }

            var result = suggestions
                .Where(s => s.ToLower().Contains(q))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .Select(s => new { suggestion = s })
                .ToList();

            return Json(result);
        }

        // GET: /Shop
        public async Task<IActionResult> Shop()
        {
            var q = Request.Query["q"].ToString();
            var label = Request.Query["label"].ToString();
            if (string.Equals(q, "challenge", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(label, "challenges", StringComparison.OrdinalIgnoreCase))
            {
                ViewData["Title"] = "Challenges";
                return View("Challenges", await BuildChallengesPageViewModelAsync());
            }

            return View();
        }

        // GET: /Home/Challenges
        public async Task<IActionResult> Challenges()
        {
            return View(await BuildChallengesPageViewModelAsync());
        }

        [HttpGet]
        public async Task<IActionResult> ChallengeActivityImage(int id, CancellationToken cancellationToken)
        {
            var activity = await _dbContext.ChallengeActivities
                .AsNoTracking()
                .Where(x => x.ActivityId == id)
                .Select(x => new { x.ImageProof })
                .FirstOrDefaultAsync(cancellationToken);

            if (activity?.ImageProof == null || activity.ImageProof.Length == 0)
            {
                return NotFound();
            }

            return File(activity.ImageProof, ResolveImageContentType(activity.ImageProof));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [RequestSizeLimit(20_000_000)]
        public async Task<IActionResult> UploadChallengeActivity(UploadChallengeActivityViewModel model, CancellationToken cancellationToken)
        {
            if (!IsLoggedIn())
            {
                return Redirect(BuildLoginRedirectUrl(Url.Action(nameof(Challenges), "Home", new { tab = "activities" }, Request.Scheme) ?? "/Home/Challenges?tab=activities"));
            }

            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                TempData["ChallengeActivityError"] = "You must be logged in to upload an activity.";
                return RedirectToAction(nameof(Challenges), new { tab = "activities" });
            }

            var participant = await _dbContext.ChallengeParticipants
                .FirstOrDefaultAsync(x => x.ParticipantId == model.ParticipantId && x.UserId == currentUserId.Value, cancellationToken);

            if (participant == null || participant.ChallengeId != model.ChallengeId)
            {
                TempData["ChallengeActivityError"] = "Challenge participation record not found.";
                return RedirectToAction(nameof(Challenges), new { tab = "activities" });
            }

            if (!CanUploadChallengeActivity(participant.Status))
            {
                TempData["ChallengeActivityError"] = "Your challenge participation is still pending approval.";
                return RedirectToAction(nameof(Challenges), new { tab = "activities" });
            }

            var challenge = await _dbContext.Challenges
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ChallengeId == participant.ChallengeId, cancellationToken);

            if (challenge == null)
            {
                TempData["ChallengeActivityError"] = "Challenge record not found.";
                return RedirectToAction(nameof(Challenges), new { tab = "activities" });
            }

            var today = DateTime.Now.Date;
            if (!IsChallengeOpenForActivityUpload(challenge.StartDate, challenge.EndDate, today))
            {
                TempData["ChallengeActivityError"] = challenge.EndDate.Date < today
                    ? "This challenge has already ended. Activity uploads are closed."
                    : "This challenge has not started yet. Activity uploads are not open.";
                return RedirectToAction(nameof(Challenges), new { tab = "activities" });
            }

            var totalSeconds = (model.Hours * 3600) + (model.Minutes * 60) + model.Seconds;
            if (model.ActivityDate == default || model.DistanceKm <= 0 || totalSeconds <= 0)
            {
                TempData["ChallengeActivityError"] = "Complete the activity date, distance, and moving time before submitting.";
                return RedirectToAction(nameof(Challenges), new { tab = "activities" });
            }

            if (model.ActivityDate.Date < challenge.StartDate.Date || model.ActivityDate.Date > challenge.EndDate.Date)
            {
                TempData["ChallengeActivityError"] = $"Activity date must be between {challenge.StartDate:MMM d, yyyy} and {challenge.EndDate:MMM d, yyyy}.";
                return RedirectToAction(nameof(Challenges), new { tab = "activities" });
            }

            byte[]? proofBytes = null;
            if (model.ProofImage != null && model.ProofImage.Length > 0)
            {
                if (!IsAllowedChallengeActivityImage(model.ProofImage))
                {
                    TempData["ChallengeActivityError"] = "Proof image must be JPG, PNG, or WEBP and 5MB or smaller.";
                    return RedirectToAction(nameof(Challenges), new { tab = "activities" });
                }

                await using var memoryStream = new MemoryStream();
                await model.ProofImage.CopyToAsync(memoryStream, cancellationToken);
                proofBytes = memoryStream.ToArray();
            }

            var roundedDistanceKm = decimal.Round(model.DistanceKm, 2, MidpointRounding.AwayFromZero);
            var averagePace = CalculateAverageSpeedKmPerHour(roundedDistanceKm, totalSeconds);

            var activity = new DbChallengeActivity
            {
                ParticipantId = participant.ParticipantId,
                ChallengeId = participant.ChallengeId,
                UserId = currentUserId.Value,
                ActivityDate = model.ActivityDate.Date,
                DistanceKm = roundedDistanceKm,
                DurationSeconds = totalSeconds,
                AveragePace = averagePace,
                ActivityType = string.IsNullOrWhiteSpace(model.ActivityType) ? "Run" : model.ActivityType.Trim(),
                IsVerified = false,
                Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim(),
                CreatedAt = DateTime.UtcNow,
                ImageProof = proofBytes
            };

            _dbContext.ChallengeActivities.Add(activity);

            participant.TotalActivities = (participant.TotalActivities ?? 0) + 1;
            participant.TotalDistanceKm = decimal.Round((participant.TotalDistanceKm ?? 0m) + roundedDistanceKm, 2, MidpointRounding.AwayFromZero);
            participant.TotalTimeSeconds = (participant.TotalTimeSeconds ?? 0) + totalSeconds;
            participant.LastActivityDate = model.ActivityDate.Date;
            participant.AveragePace = CalculateAverageSpeedKmPerHour(participant.TotalDistanceKm, participant.TotalTimeSeconds ?? 0);

            await _dbContext.SaveChangesAsync(cancellationToken);

            TempData["ChallengeActivitySuccess"] = "Activity uploaded successfully.";
            return RedirectToAction(nameof(Challenges), new { tab = "activities" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> JoinChallenge(JoinChallengeViewModel model, CancellationToken cancellationToken)
        {
            if (!IsLoggedIn())
            {
                var targetUrl = Url.Action(nameof(Challenges), "Home", null, Request.Scheme) ?? "/Home/Challenges";
                return Unauthorized(new
                {
                    success = false,
                    loginUrl = BuildLoginRedirectUrl(targetUrl)
                });
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .ToDictionary(
                        x => x.Key,
                        x => x.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

                return BadRequest(new { success = false, errors });
            }

            await EnsureChallengesTableAsync(cancellationToken);

            var challenge = await _dbContext.Challenges
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ChallengeId == model.ChallengeId, cancellationToken);

            if (challenge == null)
            {
                return NotFound(new { success = false, message = "Challenge not found." });
            }

            var userContext = await _userContextService.GetCurrentAsync(cancellationToken);
            var userId = userContext?.UserId;

            if (!userId.HasValue)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Consumer profile missing—please complete your account before joining."
                });
            }

            var userIdValue = userId.Value;
            var consumerIdValue = await EnsureConsumerRecordAsync(userIdValue, model, cancellationToken);
            var normalizedEmail = model.Email.Trim().ToLowerInvariant();

            var duplicateExists = await _dbContext.ChallengeRegistrations.AnyAsync(
                x => x.ChallengeId == model.ChallengeId &&
                    (
                        x.UserId == userIdValue ||
                        x.Email.ToLower() == normalizedEmail
                    ) &&
                x.Status != "Rejected",
                cancellationToken);

            var duplicateParticipant = await _dbContext.ChallengeParticipants.AnyAsync(
                x => x.ChallengeId == model.ChallengeId &&
                    (x.UserId == userIdValue || x.ConsumerId == consumerIdValue),
                cancellationToken);

            if (duplicateParticipant)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "You already submitted a registration for this challenge."
                });
            }

            if (duplicateExists)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "You already submitted a registration for this challenge."
                });
            }

            try
            {
                var registration = new DbChallengeRegistration
                {
                    ChallengeId = model.ChallengeId,
                    UserId = userId,
                    FullName = model.FullName.Trim(),
                    Email = model.Email.Trim(),
                    PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber.Trim(),
                    City = string.IsNullOrWhiteSpace(model.City) ? null : model.City.Trim(),
                    Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim(),
                    Status = "Pending",
                    SubmittedAt = DateTime.UtcNow
                };

                await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

                _dbContext.ChallengeRegistrations.Add(registration);
                await _dbContext.SaveChangesAsync(cancellationToken);

                var participantId = await InsertChallengeParticipantAsync(
                    model.ChallengeId,
                    userIdValue,
                    consumerIdValue,
                    cancellationToken);

                if (!participantId.HasValue)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return StatusCode(StatusCodes.Status500InternalServerError, new
                    {
                        success = false,
                        message = "Registration was submitted but the participant row was not stored."
                    });
                }

                await transaction.CommitAsync(cancellationToken);

                var rowExists = await ChallengeParticipantExistsAsync(participantId.Value, cancellationToken);
                if (!rowExists)
                {
                    return StatusCode(StatusCodes.Status500InternalServerError, new
                    {
                        success = false,
                        message = "Participant insert did not persist to challenge_participants."
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    success = false,
                    message = $"Unable to save challenge participant: {ex.Message}"
                });
            }

            return Json(new
            {
                success = true,
                message = "Successfully submitted and wait for the approval."
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> JoinChallengeSubmit(JoinChallengeViewModel model, CancellationToken cancellationToken)
        {
            var isAjaxRequest = string.Equals(Request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase);

            IActionResult ErrorResult(string message, int statusCode = StatusCodes.Status400BadRequest, object? errors = null)
            {
                if (isAjaxRequest)
                {
                    if (errors is not null)
                    {
                        return StatusCode(statusCode, new { success = false, errors });
                    }

                    return StatusCode(statusCode, new { success = false, message });
                }

                TempData["ChallengeJoinError"] = message;
                return RedirectToAction(nameof(Challenges));
            }

            IActionResult SuccessResult(string message)
            {
                if (isAjaxRequest)
                {
                    return Json(new { success = true, message });
                }

                TempData["ChallengeJoinSuccess"] = message;
                return RedirectToAction(nameof(Challenges));
            }

            if (!IsLoggedIn())
            {
                var targetUrl = Url.Action(nameof(Challenges), "Home", null, Request.Scheme) ?? "/Home/Challenges";
                var loginUrl = BuildLoginRedirectUrl(targetUrl);
                return isAjaxRequest
                    ? Unauthorized(new { success = false, loginUrl })
                    : Redirect(loginUrl);
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .ToDictionary(
                        x => x.Key,
                        x => x.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

                var firstError = errors.Values.SelectMany(x => x).FirstOrDefault() ?? "Please complete the form correctly.";
                return ErrorResult(firstError, StatusCodes.Status400BadRequest, errors);
            }

            await EnsureChallengesTableAsync(cancellationToken);

            var challenge = await _dbContext.Challenges
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ChallengeId == model.ChallengeId, cancellationToken);

            if (challenge == null)
            {
                return ErrorResult("Challenge not found.", StatusCodes.Status404NotFound);
            }

            var currentUserId = GetCurrentUserId();
            if (!currentUserId.HasValue)
            {
                return ErrorResult("You must be logged in before joining a challenge.");
            }

            var userContext = await _userContextService.GetByUserIdAsync(currentUserId.Value, cancellationToken);
            if (userContext?.UserId is not int userIdValue)
            {
                return ErrorResult("Consumer profile missing. Please complete your account before joining.");
            }

            var consumerIdValue = await EnsureConsumerRecordAsync(userIdValue, model, cancellationToken);
            var duplicateParticipant = await _dbContext.ChallengeParticipants.AnyAsync(
                x => x.ChallengeId == model.ChallengeId &&
                    (x.UserId == userIdValue || x.ConsumerId == consumerIdValue),
                cancellationToken);

            if (duplicateParticipant)
            {
                return ErrorResult("You already joined or submitted this challenge.");
            }

            try
            {
                var participantId = await InsertChallengeParticipantAsync(
                    model.ChallengeId,
                    userIdValue,
                    consumerIdValue,
                    cancellationToken);

                if (!participantId.HasValue)
                {
                    return ErrorResult("challenge_participants insert failed.", StatusCodes.Status500InternalServerError);
                }

                var rowExists = await ChallengeParticipantExistsAsync(participantId.Value, cancellationToken);
                if (!rowExists)
                {
                    return ErrorResult("challenge_participants insert did not persist.", StatusCodes.Status500InternalServerError);
                }
            }
            catch (Exception ex)
            {
                return ErrorResult($"Unable to save challenge participant: {ex.Message}", StatusCodes.Status500InternalServerError);
            }

            return SuccessResult("Successfully saved to challenge_participants with Pending status.");
        }

        private async Task<int?> InsertChallengeParticipantAsync(
            int challengeId,
            int userId,
            int consumerId,
            CancellationToken cancellationToken)
        {
            var connection = _dbContext.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;

            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.Transaction = _dbContext.Database.CurrentTransaction?.GetDbTransaction();
                command.CommandText = """
                    INSERT INTO dbo.challenge_participants
                    (
                        challenge_id,
                        user_id,
                        consumer_id,
                        total_distance_km,
                        total_activities,
                        total_time_seconds,
                        average_pace,
                        [rank],
                        joined_at,
                        last_activity_date,
                        is_completed,
                        completed_at,
                        status
                    )
                    VALUES
                    (
                        @challenge_id,
                        @user_id,
                        @consumer_id,
                        @total_distance_km,
                        @total_activities,
                        @total_time_seconds,
                        @average_pace,
                        @rank,
                        @joined_at,
                        @last_activity_date,
                        @is_completed,
                        @completed_at,
                        @status
                    );

                    SELECT CAST(SCOPE_IDENTITY() AS INT);
                    """;

                AddParameter(command, "@challenge_id", challengeId, DbType.Int32);
                AddParameter(command, "@user_id", userId, DbType.Int32);
                AddParameter(command, "@consumer_id", consumerId, DbType.Int32);
                AddParameter(command, "@total_distance_km", 0m, DbType.Decimal);
                AddParameter(command, "@total_activities", 0, DbType.Int32);
                AddParameter(command, "@total_time_seconds", 0, DbType.Int32);
                AddParameter(command, "@average_pace", null, DbType.Decimal);
                AddParameter(command, "@rank", null, DbType.Int32);
                AddParameter(command, "@joined_at", DateTime.UtcNow, DbType.DateTime2);
                AddParameter(command, "@last_activity_date", null, DbType.DateTime2);
                AddParameter(command, "@is_completed", false, DbType.Boolean);
                AddParameter(command, "@completed_at", null, DbType.DateTime2);
                AddParameter(command, "@status", "Pending", DbType.String);

                var result = await command.ExecuteScalarAsync(cancellationToken);
                return result is int intResult
                    ? intResult
                    : result is decimal decimalResult
                        ? Convert.ToInt32(decimalResult)
                        : result is long longResult
                            ? Convert.ToInt32(longResult)
                            : null;
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }

        private async Task<bool> ChallengeParticipantExistsAsync(int participantId, CancellationToken cancellationToken)
        {
            return await _dbContext.ChallengeParticipants
                .AsNoTracking()
                .AnyAsync(x => x.ParticipantId == participantId, cancellationToken);
        }

        private static void AddParameter(DbCommand command, string name, object? value, DbType dbType)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.DbType = dbType;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> LaunchChallenge(LaunchChallengeViewModel model, CancellationToken cancellationToken)
        {
            if (model.StartDate.HasValue && model.EndDate.HasValue && model.EndDate.Value < model.StartDate.Value)
            {
                ModelState.AddModelError(nameof(model.EndDate), "End date must be on or after the start date.");
            }

            if (model.ChallengeCoverImage is not null && model.ChallengeCoverImage.Length > 0)
            {
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
                var extension = Path.GetExtension(model.ChallengeCoverImage.FileName);
                if (string.IsNullOrWhiteSpace(extension) || !allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(nameof(model.ChallengeCoverImage), "Cover image must be a JPG, PNG, or WEBP file.");
                }
            }

            if (!ModelState.IsValid)
            {
                var errors = ModelState
                    .Where(x => x.Value?.Errors.Count > 0)
                    .ToDictionary(
                        x => x.Key,
                        x => x.Value!.Errors.Select(e => e.ErrorMessage).ToArray());

                return BadRequest(new { success = false, errors });
            }

            await EnsureChallengesTableAsync(cancellationToken);

            var uploadsRoot = Path.Combine(_environment.WebRootPath, "uploads", "challenges");
            Directory.CreateDirectory(uploadsRoot);

            var fileExtension = Path.GetExtension(model.ChallengeCoverImage!.FileName);
            var fileName = $"{Guid.NewGuid():N}{fileExtension}";
            var filePath = Path.Combine(uploadsRoot, fileName);

            await using (var stream = new FileStream(filePath, FileMode.Create))
            {
                await model.ChallengeCoverImage.CopyToAsync(stream, cancellationToken);
            }

            byte[]? bannerBytes = null;
            string? bannerImageName = null;
            string? bannerImageContentType = null;

            if (model.ChallengeCoverImage is not null && model.ChallengeCoverImage.Length > 0)
            {
                await using var memoryStream = new MemoryStream();
                await model.ChallengeCoverImage.CopyToAsync(memoryStream, cancellationToken);
                bannerBytes = memoryStream.ToArray();
                bannerImageName = model.ChallengeCoverImage.FileName;
                bannerImageContentType = model.ChallengeCoverImage.ContentType;
            }

            var challenge = new DbChallenge
            {
                Title = model.ChallengeName.Trim(),
                StartDate = model.StartDate!.Value,
                EndDate = model.EndDate!.Value,
                ActivityType = model.ActivityType.Trim(),
                GoalKm = model.GoalKm!.Value,
                Description = model.Description.Trim(),
                Rules = model.Rules.Trim(),
                Prizes = model.Prizes.Trim(),
                Status = model.StartDate.Value.Date > DateTime.Now.Date ? "upcoming" : "active",
                BannerImage = bannerBytes,
                BannerImageName = bannerImageName,
                BannerImageContentType = bannerImageContentType,
                CreatedBy = HttpContext.Session.GetInt32("UserId") ?? 0,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _dbContext.Challenges.Add(challenge);
            await _dbContext.SaveChangesAsync(cancellationToken);

            return Json(new { success = true });
        }

        private async Task<int> EnsureConsumerRecordAsync(int userId, JoinChallengeViewModel model, CancellationToken cancellationToken)
        {
            var consumer = await _dbContext.Consumers
                .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

            if (consumer != null)
            {
                return consumer.ConsumerId;
            }

            var splits = model.FullName
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            consumer = new Consumer
            {
                UserId = userId,
                FirstName = splits.FirstOrDefault() ?? string.Empty,
                LastName = splits.Count > 1 ? splits[^1] : string.Empty,
                Address = model.City,
                PhoneNumber = string.IsNullOrWhiteSpace(model.PhoneNumber) ? null : model.PhoneNumber?.Trim(),
                Username = model.Email,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Consumers.Add(consumer);
            await _dbContext.SaveChangesAsync(cancellationToken);
            return consumer.ConsumerId;
        }

        // GET: /Home/ChallengeBanner/5
        [HttpGet]
        public async Task<IActionResult> ChallengeBanner(int id)
        {
            var challenge = await _dbContext.Challenges
                .AsNoTracking()
                .Where(c => c.ChallengeId == id)
                .Select(c => new
                {
                    c.BannerImage,
                    c.BannerImageContentType
                })
                .FirstOrDefaultAsync();

            if (challenge?.BannerImage == null || challenge.BannerImage.Length == 0)
            {
                return NotFound();
            }

            var contentType = string.IsNullOrWhiteSpace(challenge.BannerImageContentType)
                ? "application/octet-stream"
                : challenge.BannerImageContentType;

            return File(challenge.BannerImage, contentType);
        }

        [HttpGet]
        public async Task<IActionResult> HeaderNotifications(CancellationToken cancellationToken)
        {
            var currentUser = await ResolveNotificationUserContextAsync(cancellationToken);
            if (!currentUser.UserId.HasValue && !currentUser.ConsumerId.HasValue)
            {
                return Json(Array.Empty<object>());
            }

            var notifications = new List<HeaderNotificationEntry>();

            notifications.AddRange((await GetOrderHeaderNotificationsAsync(currentUser.UserId, currentUser.ConsumerId, cancellationToken))
                .Select(notification => new HeaderNotificationEntry(
                    notification.NotificationId,
                    notification.Message,
                    notification.IsRead,
                    notification.CreatedAt,
                    BuildNotificationTargetUrl(notification.Category, notification.OrderId))));

            try
            {
                notifications.AddRange((await _challengeNotificationService.GetHeaderNotificationsAsync(HttpContext, cancellationToken))
                    .Select(notification => new HeaderNotificationEntry(
                        notification.NotificationId,
                        notification.Message,
                        notification.IsRead,
                        notification.CreatedAt,
                        null)));
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("ConnectionString", StringComparison.OrdinalIgnoreCase))
            {
            }

            var result = notifications
                .OrderByDescending(notification => notification.CreatedAt)
                .Take(10)
                .Select(notification =>
            {
                var age = DateTime.Now - notification.CreatedAt;
                var timeAgo = age.TotalMinutes < 1
                    ? "Just now"
                    : age.TotalHours < 1
                        ? $"{Math.Max(1, (int)Math.Floor(age.TotalMinutes))} min ago"
                        : age.TotalDays < 1
                            ? $"{Math.Max(1, (int)Math.Floor(age.TotalHours))} hours ago"
                            : $"{Math.Max(1, (int)Math.Floor(age.TotalDays))} days ago";

                return new
                {
                    notificationId = notification.NotificationId,
                    message = notification.Message,
                    isRead = notification.IsRead,
                    timeAgo,
                    targetUrl = notification.TargetUrl
                };
            });

            return Json(result);
        }

        [HttpPost]
        public async Task<IActionResult> MarkHeaderNotificationRead([FromBody] MarkHeaderNotificationReadRequest? request, CancellationToken cancellationToken)
        {
            if (request?.NotificationId is not > 0)
            {
                return BadRequest(new { success = false });
            }

            var currentUser = await ResolveNotificationUserContextAsync(cancellationToken);
            if (!currentUser.UserId.HasValue && !currentUser.ConsumerId.HasValue)
            {
                return Unauthorized(new { success = false });
            }

            await EnsureNotificationsTableAsync(cancellationToken);
            await MarkOrderHeaderNotificationReadAsync(request.NotificationId, currentUser.UserId, currentUser.ConsumerId, cancellationToken);

            return Json(new { success = true });
        }

        // GET: /Home/About
        public IActionResult About()
        {
            return View();
        }

        // GET: /Home/Product?id=5
        public IActionResult Product(int id)
        {
            ViewData["ProductId"] = id;
            ViewData["ShowConsumerMessengerLink"] = true;
            ViewData["ConsumerMessengerHref"] = "/consumer/messenger/";
            return View();
        }

        // GET: /Home/Contact
        public IActionResult Contact()
        {
            return View();
        }

        // GET: /Home/Reviews?id=5
        public IActionResult Reviews(int id)
        {
            ViewData["ProductId"] = id;
            return View();
        }

        // GET: /Home/WriteReview?id=5
        public IActionResult WriteReview(int id)
        {
            ViewData["ProductId"] = id;
            return View();
        }

        // GET: /Home/Cart
        public IActionResult Cart()
        {
            return View();
        }

        // GET: /Home/Checkout
        public async Task<IActionResult> Checkout(CancellationToken cancellationToken)
        {
            if (IsSeller())
            {
                return RedirectToAction("Index", "Seller");
            }

            if (!IsLoggedIn())
            {
                return Redirect(BuildLoginRedirectUrl(CheckoutAppUrl));
            }

            return View(await BuildCheckoutVmAsync(cancellationToken: cancellationToken));
        }

        public IActionResult BuyNow(int productId, string? size, string? color, int quantity = 1, int? sellerId = null, int? variantId = null)
        {
            if (IsSeller())
            {
                return RedirectToAction("Index", "Seller");
            }

            if (!IsLoggedIn())
            {
                var buyNowUrl = Url.Action(
                    nameof(BuyNow),
                    "Home",
                new { productId, size, color, quantity, sellerId, variantId },
                    Request.Scheme);

                return Redirect(BuildLoginRedirectUrl(buyNowUrl ?? CheckoutAppUrl));
            }

            if (productId <= 0 || string.IsNullOrWhiteSpace(size))
            {
                return RedirectToAction(nameof(Product), new { id = productId });
            }

            ProductData.ReplaceCart(new[]
            {
                new CartItem
                {
                    ProductId = productId,
                    VariantId = variantId,
                    Color = color?.Trim() ?? string.Empty,
                    Size = size.Trim(),
                    Quantity = Math.Max(1, quantity),
                    SellerId = sellerId ?? 0,
                    UnitPrice = ResolveBuyNowUnitPrice(productId, variantId, size, color)
                }
            });

            SyncSharedCartCookie();

            return RedirectToAction(nameof(Checkout));
        }

        public IActionResult MyProfile()
        {
            if (!IsLoggedIn())
            {
                return Redirect(BuildLoginRedirectUrl(ProfileAppUrl));
            }

            return RedirectToAction("ProfileView", "AccountProfile");
        }

        public IActionResult UploadActivity()
        {
            if (!IsLoggedIn())
            {
                return Redirect(BuildLoginRedirectUrl(UploadActivityAppUrl));
            }

            return RedirectToAction("UploadActivity", "AccountProfile");
        }

        public IActionResult Logout()
        {
            HttpContext.Session.Clear();
            Response.Cookies.Delete(SharedUserIdCookie, new CookieOptions { Path = "/" });
            Response.Cookies.Delete(SharedUserEmailCookie, new CookieOptions { Path = "/" });
            Response.Cookies.Delete(SharedUserTypeCookie, new CookieOptions { Path = "/" });
            Response.Cookies.Delete(SharedDisplayNameCookie, new CookieOptions { Path = "/" });
            foreach (var cookieName in SessionCookiesToClear)
            {
                Response.Cookies.Delete(cookieName, new CookieOptions { Path = "/" });
            }
            return Redirect(LandingPageUrl);
        }

        // POST: /Home/PlaceOrder
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PlaceOrder(CheckoutViewModel vm)
        {
            // Always re-populate cart items from server data
            vm.CartItems = GetCheckoutItems();
            vm.Subtotal = vm.CartItems.Sum(i => i.Price * i.Quantity);
            vm.ShippingFee = vm.DeliveryOption == "Express" ? 300m : 150m;

            try
            {
                vm.AvailableVouchers = await LoadApplicableVouchersAsync(vm.CartItems, vm.Subtotal, HttpContext.RequestAborted);
            }
            catch (Exception ex) when (IsOptionalCheckoutDataException(ex))
            {
                vm.AvailableVouchers = new List<CheckoutVoucherOption>();
                vm.SelectedVoucherId = null;
            }

            ApplySelectedVoucher(vm);

            ModelState.Remove("CartItems");
            ModelState.Remove("Subtotal");
            ModelState.Remove("ShippingFee");
            ModelState.Remove("CardNumber");
            ModelState.Remove("CardExpiry");
            ModelState.Remove("CardCvv");
            ModelState.Remove("AvailableVouchers");
            ModelState.Remove("AppliedVoucherName");
            ModelState.Remove("VoucherDiscount");

            if (!ModelState.IsValid)
                return View("Checkout", vm);

            foreach (var item in vm.CartItems)
            {
                ProductData.PurchaseRecords.Add(new PurchaseRecord
                {
                    ProductId = item.ProductId,
                    PurchaseDate = DateTime.Now,
                    DeliveryDate = DateTime.Now.AddDays(vm.DeliveryOption == "Express" ? 2 : 5)
                });
            }

            var createdAt = DateTime.Now;
            var currentUserId = GetCurrentUserId();
            int? consumerId = null;

            if (currentUserId.HasValue)
            {
                try
                {
                    consumerId = await TryGetConsumerIdAsync(currentUserId.Value, HttpContext.RequestAborted);
                }
                catch (Exception ex) when (IsOptionalCheckoutDataException(ex))
                {
                    consumerId = null;
                }
            }

            var insertedOrderId = await SaveOrderAsync(
                vm,
                createdAt,
                null,
                currentUserId,
                consumerId,
                HttpContext.RequestAborted);

            try
            {
                await EnsureNotificationsTableAsync(HttpContext.RequestAborted);
                await EnsureOrderNotificationsBackfilledAsync(currentUserId, consumerId, HttpContext.RequestAborted);
            }
            catch (Exception ex) when (IsOptionalCheckoutDataException(ex))
            {
                // Notifications are best-effort; they must not block order persistence.
            }

            var confirmVm = new OrderConfirmationViewModel
            {
                OrderId = insertedOrderId?.ToString() ?? string.Empty,
                OrderStatus = "Placed",
                FullName = vm.FullName,
                Email = vm.Email,
                Phone = vm.Phone,
                Address = vm.Address,
                City = vm.City,
                PostalCode = vm.PostalCode,
                DeliveryOption = vm.DeliveryOption,
                PaymentMethod = vm.PaymentMethod,
                CreatedAt = createdAt,
                EstimatedDeliveryDate = createdAt.AddDays(vm.DeliveryOption == "Express" ? 2 : 5),
                OrderItems = vm.CartItems,
                Subtotal = vm.Subtotal,
                ShippingFee = vm.ShippingFee,
                AppliedVoucherName = vm.AppliedVoucherName,
                VoucherDiscount = vm.VoucherDiscount,
            };

            ProductData.Orders.Insert(0, confirmVm);
            ProductData.ReplaceCart(Array.Empty<CartItem>());
            SyncSharedCartCookie();

            TempData["OrderConfirmation"] = JsonSerializer.Serialize(confirmVm);
            return RedirectToAction("OrderConfirmation");
        }

        // GET: /Home/OrderConfirmation
        public IActionResult OrderConfirmation()
        {
            if (TempData["OrderConfirmation"] is not string json)
                return RedirectToAction("Cart");

            var vm = JsonSerializer.Deserialize<OrderConfirmationViewModel>(json);
            if (vm == null)
                return RedirectToAction("Cart");

            // Keep so OrderDetail can also read it
            TempData.Keep("OrderConfirmation");
            return View(vm);
        }

        // GET: /Home/MyOrders
        public IActionResult MyOrders()
        {
            return View(ProductData.Orders);
        }

        // GET: /Home/OrderDetail?id=123
        public IActionResult OrderDetail(string? id)
        {
            OrderConfirmationViewModel? vm = null;

            if (!string.IsNullOrEmpty(id))
                vm = ProductData.Orders.FirstOrDefault(o => o.OrderId == id);

            if (vm == null)
            {
                if (TempData["OrderConfirmation"] is string json)
                    vm = System.Text.Json.JsonSerializer.Deserialize<OrderConfirmationViewModel>(json);
            }

            if (vm == null)
                return RedirectToAction("MyOrders");

            return View(vm);
        }

        private List<CheckoutItem> GetCheckoutItems()
        {
            Dictionary<int, DbProduct> dbProductsById;
            Dictionary<int, List<DbProductVariant>> variantsByProductId;

            try
            {
                dbProductsById = _dbContext.Products
                    .AsNoTracking()
                    .ToList()
                    .ToDictionary(product => product.ProductId);

                variantsByProductId = _dbContext.ProductVariants
                    .AsNoTracking()
                    .ToList()
                    .GroupBy(variant => variant.ProductId)
                    .ToDictionary(group => group.Key, group => group.OrderBy(variant => variant.Id).ToList());
            }
            catch (Exception ex) when (IsOptionalCheckoutDataException(ex))
            {
                dbProductsById = new Dictionary<int, DbProduct>();
                variantsByProductId = new Dictionary<int, List<DbProductVariant>>();
            }

            var cartSnapshot = ProductData.GetCartSnapshot();

            return cartSnapshot.Select(ci =>
            {
                var product = ProductData.Products.FirstOrDefault(p => p.Id == ci.ProductId);
                dbProductsById.TryGetValue(ci.ProductId, out var dbProduct);
                variantsByProductId.TryGetValue(ci.ProductId, out var productVariants);
                var matchingVariant = ResolveCheckoutVariant(ci, productVariants);

                return new CheckoutItem
                {
                    ProductId = ci.ProductId,
                    Name = product?.Name ?? dbProduct?.ProductName ?? "Unknown",
                    Image = BuildCheckoutItemImage(matchingVariant, product?.Image, dbProduct?.ImagePath),
                    SellerId = product?.SellerId ?? dbProduct?.SellerId ?? ci.SellerId,
                    Size = ci.Size,
                    Price = matchingVariant?.Price ?? product?.Price ?? ci.UnitPrice,
                    Quantity = ci.Quantity
                };
            }).Where(i => i.Price > 0).ToList();
        }

        private decimal ResolveBuyNowUnitPrice(int productId, int? variantId, string? size, string? color)
        {
            List<DbProductVariant> variants;

            try
            {
                variants = _dbContext.ProductVariants
                    .AsNoTracking()
                    .Where(variant => variant.ProductId == productId)
                    .OrderBy(variant => variant.Id)
                    .ToList();
            }
            catch (Exception ex) when (IsOptionalCheckoutDataException(ex))
            {
                variants = new List<DbProductVariant>();
            }

            var cartItem = new CartItem
            {
                ProductId = productId,
                VariantId = variantId,
                Size = size?.Trim() ?? string.Empty,
                Color = color?.Trim() ?? string.Empty
            };

            var matchingVariant = ResolveCheckoutVariant(cartItem, variants);
            if (matchingVariant?.Price is decimal variantPrice && variantPrice > 0)
            {
                return variantPrice;
            }

            return ProductData.Products.FirstOrDefault(product => product.Id == productId)?.Price ?? 0m;
        }

        private static DbProductVariant? ResolveCheckoutVariant(CartItem cartItem, IReadOnlyCollection<DbProductVariant>? productVariants)
        {
            if (productVariants == null || productVariants.Count == 0)
            {
                return null;
            }

            if (cartItem.VariantId.HasValue)
            {
                var exactVariant = productVariants.FirstOrDefault(variant => variant.Id == cartItem.VariantId.Value);
                if (exactVariant != null)
                {
                    return exactVariant;
                }
            }

            return productVariants.FirstOrDefault(variant =>
                       string.Equals(variant.Size, cartItem.Size, StringComparison.OrdinalIgnoreCase) &&
                       (string.IsNullOrWhiteSpace(cartItem.Color) ||
                        string.Equals(variant.Style, cartItem.Color, StringComparison.OrdinalIgnoreCase)))
                   ?? productVariants.FirstOrDefault(variant =>
                       string.Equals(variant.Size, cartItem.Size, StringComparison.OrdinalIgnoreCase))
                   ?? productVariants.FirstOrDefault(variant =>
                       string.Equals(variant.Style, cartItem.Color, StringComparison.OrdinalIgnoreCase))
                   ?? productVariants.FirstOrDefault();
        }

        private string BuildCheckoutItemImage(DbProductVariant? variant, string? productImage, string? productImagePath)
        {
            if (variant != null)
            {
                if (variant.ImageData != null && variant.ImageData.Length > 0)
                {
                    return Url.Action("GetVariantImage", "Products", new
                    {
                        variantId = variant.Id,
                        v = variant.ImageData.Length
                    }) ?? $"/api/products/variant-image/{variant.Id}";
                }

                var variantImagePath = _mediaPathService.NormalizePublicPath(variant.ImagePath);
                if (!string.IsNullOrWhiteSpace(variantImagePath))
                {
                    return variantImagePath;
                }
            }

            if (!string.IsNullOrWhiteSpace(productImage))
            {
                return productImage;
            }

            var normalizedProductImagePath = _mediaPathService.NormalizePublicPath(productImagePath);
            if (!string.IsNullOrWhiteSpace(normalizedProductImagePath))
            {
                return normalizedProductImagePath;
            }

            return "/images/placeholder.png";
        }

        private async Task<CheckoutViewModel> BuildCheckoutVmAsync(
            CheckoutViewModel? seed = null,
            CancellationToken cancellationToken = default)
        {
            var items = GetCheckoutItems();
            var model = seed ?? new CheckoutViewModel();
            model.CartItems = items;
            model.Subtotal = items.Sum(i => i.Price * i.Quantity);
            model.ShippingFee = string.Equals(model.DeliveryOption, "Express", StringComparison.OrdinalIgnoreCase) ? 300m : 150m;

            var currentUserId = GetCurrentUserId();
            User? currentUser = null;
            Consumer? currentConsumer = null;
            LatestCheckoutProfile? latestOrderProfile = null;

            try
            {
                currentUser = currentUserId.HasValue
                    ? await _dbContext.Users.AsNoTracking().FirstOrDefaultAsync(user => user.UserId == currentUserId.Value, cancellationToken)
                    : null;
                currentConsumer = currentUserId.HasValue
                    ? await _dbContext.Consumers.AsNoTracking().FirstOrDefaultAsync(consumer => consumer.UserId == currentUserId.Value, cancellationToken)
                    : null;
            }
            catch (Exception ex) when (IsOptionalCheckoutDataException(ex))
            {
                currentUser = null;
                currentConsumer = null;
            }

            try
            {
                latestOrderProfile = await LoadLatestCheckoutProfileAsync(currentUserId, currentConsumer?.ConsumerId, cancellationToken);
            }
            catch (Exception ex) when (IsOptionalCheckoutDataException(ex))
            {
                latestOrderProfile = null;
            }

            if (string.IsNullOrWhiteSpace(model.FullName))
            {
                model.FullName = FirstNonEmpty(
                    latestOrderProfile?.FullName,
                    currentConsumer?.FullName,
                    GetCurrentDisplayName(),
                    currentUser?.Email);
            }

            if (string.IsNullOrWhiteSpace(model.Email))
            {
                model.Email = FirstNonEmpty(
                    latestOrderProfile?.Email,
                    currentUser?.Email,
                    HttpContext.Session.GetString("UserEmail"),
                    Request.Cookies[SharedUserEmailCookie]);
            }

            if (string.IsNullOrWhiteSpace(model.Phone))
            {
                model.Phone = FirstNonEmpty(latestOrderProfile?.Phone, currentConsumer?.PhoneNumber);
            }

            if (seed == null || string.IsNullOrWhiteSpace(model.PaymentMethod) || string.Equals(model.PaymentMethod, "Card", StringComparison.OrdinalIgnoreCase))
            {
                model.PaymentMethod = FirstNonEmpty(latestOrderProfile?.PaymentMethod, "Card");
            }

            var fallbackAddress = DecomposeAddress(currentConsumer?.Address);
            model.Address = string.IsNullOrWhiteSpace(model.Address)
                ? FirstNonEmpty(fallbackAddress.Address, latestOrderProfile?.Address)
                : model.Address;
            model.City = string.IsNullOrWhiteSpace(model.City)
                ? FirstNonEmpty(fallbackAddress.City, latestOrderProfile?.City)
                : model.City;
            model.PostalCode = string.IsNullOrWhiteSpace(model.PostalCode)
                ? FirstNonEmpty(fallbackAddress.PostalCode, latestOrderProfile?.PostalCode)
                : model.PostalCode;

            model.SavedAddresses = BuildCheckoutAddressOptions(currentConsumer, latestOrderProfile, model);

            try
            {
                model.AvailableVouchers = await LoadApplicableVouchersAsync(model.CartItems, model.Subtotal, cancellationToken);
            }
            catch (Exception ex) when (IsOptionalCheckoutDataException(ex))
            {
                model.AvailableVouchers = new List<CheckoutVoucherOption>();
                model.SelectedVoucherId = null;
            }

            ApplySelectedVoucher(model);
            return model;
        }

        private async Task<LatestCheckoutProfile?> LoadLatestCheckoutProfileAsync(
            int? userId,
            int? consumerId,
            CancellationToken cancellationToken)
        {
            if (!userId.HasValue && !consumerId.HasValue)
            {
                return null;
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
                    SELECT TOP (1)
                        FullName,
                        Email,
                        PhoneNumber,
                        StreetAddress,
                        City,
                        PostalCode,
                        PaymentMethod
                    FROM dbo.Orders
                    WHERE
                        (@UserIdText IS NOT NULL AND LTRIM(RTRIM(CONVERT(NVARCHAR(50), UserId))) = @UserIdText)
                        OR (@ConsumerId IS NOT NULL AND TRY_CONVERT(INT, ConsumerId) = @ConsumerId)
                    ORDER BY COALESCE(OrderDate, EstimatedDeliveryDate, GETDATE()) DESC, OrderId DESC;
                    """;

                AddDbParameter(command, "@UserIdText", userId?.ToString(), DbType.String);
                AddDbParameter(command, "@ConsumerId", consumerId, DbType.Int32);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    return null;
                }

                return new LatestCheckoutProfile(
                    reader.IsDBNull(0) ? string.Empty : reader.GetString(0),
                    reader.IsDBNull(1) ? string.Empty : reader.GetString(1),
                    reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                    reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
                    reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                    reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    reader.IsDBNull(6) ? string.Empty : reader.GetString(6));
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }

        private async Task<List<CheckoutVoucherOption>> LoadApplicableVouchersAsync(
            IReadOnlyCollection<CheckoutItem> items,
            decimal subtotal,
            CancellationToken cancellationToken)
        {
            if (items.Count == 0)
            {
                return new List<CheckoutVoucherOption>();
            }

            var productIds = items.Select(item => item.ProductId).ToHashSet();
            var promotions = await _dbContext.Promotions
                .AsNoTracking()
                .Where(promotion =>
                    promotion.Status != null &&
                    (promotion.Status == "Approved" || promotion.Status == "active"))
                .OrderByDescending(promotion => promotion.UpdatedAt ?? promotion.CreatedAt)
                .ToListAsync(cancellationToken);

            var vouchers = new List<CheckoutVoucherOption>();
            foreach (var promotion in promotions)
            {
                var selectedProductIds = PromotionSerialization.Deserialize(promotion.SelectedProductIdsJson)
                    .Select(value => int.TryParse(value, out var parsedValue) ? parsedValue : 0)
                    .Where(value => value > 0)
                    .ToHashSet();

                if (selectedProductIds.Count > 0 && !selectedProductIds.Overlaps(productIds))
                {
                    continue;
                }

                if (promotion.MinimumPurchaseAmount.HasValue &&
                    promotion.MinimumPurchaseAmount.Value > 0 &&
                    subtotal < promotion.MinimumPurchaseAmount.Value)
                {
                    continue;
                }

                var discountAmount = ComputePromotionDiscount(promotion, subtotal);
                if (discountAmount <= 0)
                {
                    continue;
                }

                vouchers.Add(new CheckoutVoucherOption
                {
                    Id = promotion.Id,
                    Name = promotion.Name,
                    Description = BuildPromotionDescription(promotion, discountAmount),
                    DiscountAmount = discountAmount
                });
            }

            return vouchers
                .OrderByDescending(voucher => voucher.DiscountAmount)
                .ThenBy(voucher => voucher.Name)
                .ToList();
        }

        private static void ApplySelectedVoucher(CheckoutViewModel model)
        {
            model.AppliedVoucherName = string.Empty;
            model.VoucherDiscount = 0m;

            if (!model.SelectedVoucherId.HasValue)
            {
                return;
            }

            var selectedVoucher = model.AvailableVouchers
                .FirstOrDefault(voucher => voucher.Id == model.SelectedVoucherId.Value);

            if (selectedVoucher == null)
            {
                model.SelectedVoucherId = null;
                return;
            }

            model.AppliedVoucherName = selectedVoucher.Name;
            model.VoucherDiscount = Math.Min(selectedVoucher.DiscountAmount, model.Subtotal + model.ShippingFee);
        }

        private static decimal ComputePromotionDiscount(DbPromotion promotion, decimal subtotal)
        {
            if (subtotal <= 0)
            {
                return 0m;
            }

            if (promotion.TotalDiscountPercent.HasValue && promotion.TotalDiscountPercent.Value > 0)
            {
                return Math.Round(subtotal * (promotion.TotalDiscountPercent.Value / 100m), 2, MidpointRounding.AwayFromZero);
            }

            if (promotion.TotalDiscountFix.HasValue && promotion.TotalDiscountFix.Value > 0)
            {
                return Math.Min(subtotal, promotion.TotalDiscountFix.Value);
            }

            return 0m;
        }

        private static string BuildPromotionDescription(DbPromotion promotion, decimal discountAmount)
        {
            var baseDescription = promotion.TotalDiscountPercent.HasValue && promotion.TotalDiscountPercent.Value > 0
                ? $"{promotion.TotalDiscountPercent.Value:N0}% off"
                : $"PHP {discountAmount:N2} off";

            if (promotion.MinimumPurchaseAmount.HasValue && promotion.MinimumPurchaseAmount.Value > 0)
            {
                return $"{baseDescription} on orders over PHP {promotion.MinimumPurchaseAmount.Value:N2}";
            }

            return baseDescription;
        }

        private static (string Address, string City, string PostalCode) DecomposeAddress(string? rawAddress)
        {
            if (string.IsNullOrWhiteSpace(rawAddress))
            {
                return (string.Empty, string.Empty, string.Empty);
            }

            var segments = rawAddress
                .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .ToList();

            if (segments.Count >= 3)
            {
                return (
                    string.Join(", ", segments.Take(segments.Count - 2)),
                    segments[^2],
                    segments[^1]);
            }

            if (segments.Count == 2)
            {
                return (segments[0], segments[1], string.Empty);
            }

            return (segments[0], string.Empty, string.Empty);
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
        }

        private static List<CheckoutAddressOption> BuildCheckoutAddressOptions(
            Consumer? currentConsumer,
            LatestCheckoutProfile? latestOrderProfile,
            CheckoutViewModel model)
        {
            var options = new List<CheckoutAddressOption>();

            void AddOption(string label, string? fullName, string? phone, string? address, string? city, string? postalCode, bool isDefault)
            {
                var normalizedAddress = FirstNonEmpty(address);
                var normalizedCity = FirstNonEmpty(city);
                var normalizedPostalCode = FirstNonEmpty(postalCode);

                if (string.IsNullOrWhiteSpace(normalizedAddress) &&
                    string.IsNullOrWhiteSpace(normalizedCity) &&
                    string.IsNullOrWhiteSpace(normalizedPostalCode))
                {
                    return;
                }

                var duplicate = options.Any(option =>
                    string.Equals(option.Address, normalizedAddress, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(option.City, normalizedCity, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(option.PostalCode, normalizedPostalCode, StringComparison.OrdinalIgnoreCase));

                if (duplicate)
                {
                    return;
                }

                options.Add(new CheckoutAddressOption
                {
                    Label = label,
                    FullName = FirstNonEmpty(fullName, model.FullName),
                    Phone = FirstNonEmpty(phone, model.Phone),
                    Address = normalizedAddress,
                    City = normalizedCity,
                    PostalCode = normalizedPostalCode,
                    IsDefault = isDefault
                });
            }

            var consumerAddress = DecomposeAddress(currentConsumer?.Address);
            AddOption("Default Address", currentConsumer?.FullName, currentConsumer?.PhoneNumber, consumerAddress.Address, consumerAddress.City, consumerAddress.PostalCode, true);
            AddOption("Latest Order Address", latestOrderProfile?.FullName, latestOrderProfile?.Phone, latestOrderProfile?.Address, latestOrderProfile?.City, latestOrderProfile?.PostalCode, false);
            AddOption("Current Checkout Address", model.FullName, model.Phone, model.Address, model.City, model.PostalCode, false);

            return options;
        }

        private static bool IsOptionalCheckoutDataException(Exception ex)
        {
            if (ex is InvalidOperationException invalidOperationException &&
                invalidOperationException.Message.Contains("ConnectionString property has not been initialized", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            if (ex is SqlException || ex is DbException)
            {
                return true;
            }

            return ex.InnerException is not null && IsOptionalCheckoutDataException(ex.InnerException);
        }

        // GET: /Home/Wishlist
        public IActionResult Wishlist()
        {
            return View();
        }

        // GET: /Home/SellerShop?id=1
        public IActionResult SellerShop(int id)
        {
            ViewData["SellerId"] = id;
            ViewData["ShowConsumerMessengerLink"] = true;
            ViewData["ConsumerMessengerHref"] = $"/consumer/messenger/?sellerId={id}";
            return View();
        }

        [HttpGet("/consumer/messenger/")]
        public async Task<IActionResult> ConsumerMessenger(int? sellerId, int? conversationId, int? orderId, string? embed)
        {
            var isEmbeddedChat = string.Equals(embed, "chat", StringComparison.OrdinalIgnoreCase);
            var target = sellerId.HasValue
                ? $"/consumer/messenger/?sellerId={sellerId.Value}" + (isEmbeddedChat ? "&embed=chat" : string.Empty)
                : "/consumer/messenger/";
            var requiresLogin = !IsLoggedIn();
            string sellerName = string.Empty;

            if (sellerId.HasValue && sellerId.Value > 0)
            {
                var sellerProfile = await _dbContext.Sellers
                    .AsNoTracking()
                    .Where(s => s.SellerId == sellerId.Value || s.UserId == sellerId.Value)
                    .OrderByDescending(s => s.SellerId == sellerId.Value)
                    .ThenBy(s => s.SellerId)
                    .FirstOrDefaultAsync();

                var resolvedUserId = sellerProfile?.UserId ?? sellerId.Value;
                var user = await _dbContext.Users
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserId == resolvedUserId);
                var consumer = await _dbContext.Consumers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.UserId == resolvedUserId);

                sellerName = !string.IsNullOrWhiteSpace(sellerProfile?.BusinessName)
                    ? sellerProfile.BusinessName.Trim()
                    : !string.IsNullOrWhiteSpace(consumer?.Username)
                        ? consumer.Username.Trim()
                        : !string.IsNullOrWhiteSpace(consumer?.FullName)
                            ? consumer.FullName.Trim()
                            : !string.IsNullOrWhiteSpace(user?.Email)
                                ? (user.Email.Contains('@') ? user.Email[..user.Email.IndexOf('@')] : user.Email)
                                : $"Seller {sellerId.Value}";
            }

            ViewData["SellerId"] = sellerId;
            ViewData["SellerName"] = sellerName;
            ViewData["ConversationId"] = conversationId;
            ViewData["OrderId"] = orderId;
            ViewData["IsEmbeddedChat"] = isEmbeddedChat;
            ViewData["CurrentUserId"] = GetCurrentUserId()?.ToString() ?? string.Empty;
            ViewData["RequiresLogin"] = requiresLogin;
            ViewData["LoginUrl"] = BuildLoginRedirectUrl(target);
            ViewData["BodyClass"] = isEmbeddedChat
                ? "consumer-messenger-layout consumer-messenger-embed"
                : "consumer-messenger-layout";
            ViewData["ShowConsumerMessengerLink"] = !isEmbeddedChat;
            ViewData["ConsumerMessengerHref"] = sellerId.HasValue
                ? $"/consumer/messenger/?sellerId={sellerId.Value}"
                : "/consumer/messenger/";
            return View();
        }

        // GET: /Home/Error
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            var feature = HttpContext.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
            return View(feature?.Error);
        }

        private bool IsSeller()
        {
            return string.Equals(HttpContext.Session.GetString("UserType"), "Seller", StringComparison.OrdinalIgnoreCase);
        }

        private bool IsLoggedIn()
        {
            return GetCurrentUserId().HasValue;
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

        private static async Task<HashSet<string>> LoadColumnLookupAsync(
            DbConnection connection,
            DbTransaction? transaction,
            string tableName,
            CancellationToken cancellationToken)
        {
            var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText =
                """
                SELECT COLUMN_NAME
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @TableName;
                """;

            var parameter = command.CreateParameter();
            parameter.ParameterName = "@TableName";
            parameter.DbType = DbType.String;
            parameter.Value = tableName;
            command.Parameters.Add(parameter);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (!reader.IsDBNull(0))
                {
                    columns.Add(reader.GetString(0));
                }
            }

            return columns;
        }

        private static string? FindColumn(IReadOnlySet<string> columns, params string[] candidates)
        {
            foreach (var candidate in candidates)
            {
                if (columns.Contains(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        private async Task<int?> TryGetConsumerIdAsync(int userId, CancellationToken cancellationToken)
        {
            return await _dbContext.Consumers
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => (int?)x.ConsumerId)
                .FirstOrDefaultAsync(cancellationToken);
        }

        private async Task<NotificationUserContext> ResolveNotificationUserContextAsync(CancellationToken cancellationToken)
        {
            var currentUser = await _userContextService.GetCurrentAsync(cancellationToken);
            if (currentUser is null)
            {
                return new NotificationUserContext(null, null, null);
            }

            var email = await _dbContext.Users
                .AsNoTracking()
                .Where(user => user.UserId == currentUser.UserId)
                .Select(user => user.Email)
                .FirstOrDefaultAsync(cancellationToken);

            return new NotificationUserContext(currentUser.UserId, currentUser.ConsumerId, email);
        }

        private async Task<IReadOnlyList<NotificationRow>> GetOrderHeaderNotificationsAsync(
            int? currentUserId,
            int? consumerId,
            CancellationToken cancellationToken)
        {
            if (!currentUserId.HasValue && !consumerId.HasValue)
            {
                return Array.Empty<NotificationRow>();
            }

            await EnsureNotificationsTableAsync(cancellationToken);
            await EnsureOrderNotificationsBackfilledAsync(currentUserId, consumerId, cancellationToken);

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
                    SELECT TOP (10)
                        n.NotificationId,
                        n.OrderId,
                        n.Message,
                        n.IsRead,
                        n.CreatedAt,
                        n.Category
                    FROM dbo.Notifications AS n
                    LEFT JOIN dbo.Orders AS o
                        ON n.OrderId = o.OrderID
                    WHERE n.RecipientType IN (@RecipientType, @LegacyRecipientType)
                      AND (
                            (@RecipientUserId IS NOT NULL AND n.RecipientId = @RecipientUserId)
                         OR (@RecipientConsumerId IS NOT NULL AND n.RecipientId = @RecipientConsumerId)
                      )
                      AND (
                            n.OrderId IS NULL
                         OR (
                                o.OrderID IS NOT NULL
                            AND (
                                    (@RecipientUserId IS NOT NULL AND LTRIM(RTRIM(ISNULL(o.UserID, N''))) = @RecipientUserIdText)
                                 OR (@RecipientConsumerId IS NOT NULL AND o.ConsumerID = @RecipientConsumerId)
                                )
                            )
                      )
                    ORDER BY n.CreatedAt DESC, n.NotificationId DESC;
                    """;

                AddDbParameter(command, "@RecipientType", "User", DbType.String);
                AddDbParameter(command, "@LegacyRecipientType", "Consumer", DbType.String);
                AddDbParameter(command, "@RecipientUserId", currentUserId, DbType.Int32);
                AddDbParameter(command, "@RecipientUserIdText", currentUserId?.ToString(), DbType.String);
                AddDbParameter(command, "@RecipientConsumerId", consumerId, DbType.Int32);

                var results = new List<NotificationRow>();
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    results.Add(new NotificationRow(
                        reader.GetInt32(0),
                        reader.IsDBNull(1) ? null : reader.GetInt32(1),
                        reader.GetString(2),
                        reader.GetBoolean(3),
                        reader.GetDateTime(4),
                        reader.IsDBNull(5) ? null : reader.GetString(5)));
                }

                return results;
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }

        private static string BuildOrderReference(int orderSequence, DateTime createdAt)
        {
            return $"ORD-{createdAt:yyyyMMdd}-{orderSequence:D4}";
        }

        private static string BuildOrderProductName(IReadOnlyCollection<CheckoutItem> items)
        {
            if (items.Count == 0)
            {
                return "Checkout Order";
            }

            if (items.Count == 1)
            {
                return items.First().Name;
            }

            var firstName = items.First().Name;
            var extraCount = items.Count - 1;
            var summary = $"{firstName} + {extraCount} more item{(extraCount == 1 ? string.Empty : "s")}";
            return summary.Length <= 255 ? summary : summary[..255];
        }

        private async Task<int?> SaveOrderAsync(
            CheckoutViewModel vm,
            DateTime createdAt,
            string orderReference,
            int? userId,
            int? consumerId,
            CancellationToken cancellationToken)
        {
            await using var connection = _dbContext.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                var orderColumns = await LoadColumnLookupAsync(connection, transaction, "Orders", cancellationToken);
                var orderItemsColumns = await LoadColumnLookupAsync(connection, transaction, "OrderItems", cancellationToken);

                var orderIdColumn = FindColumn(orderColumns, "OrderId", "OrderID");
                var orderNumberColumn = FindColumn(orderColumns, "OrderNumber", "OrderNo");
                var userIdColumn = FindColumn(orderColumns, "UserId", "user_id", "UserID");
                var consumerIdColumn = FindColumn(orderColumns, "ConsumerId", "consumer_id", "ConsumerID");
                var sellerIdColumn = FindColumn(orderColumns, "SellerId", "seller_id", "SellerID");
                var fullNameColumn = FindColumn(orderColumns, "FullName", "full_name");
                var emailColumn = FindColumn(orderColumns, "Email", "email");
                var phoneColumn = FindColumn(orderColumns, "PhoneNumber", "phone_number");
                var streetColumn = FindColumn(orderColumns, "StreetAddress", "street_address", "Address", "address");
                var cityColumn = FindColumn(orderColumns, "City", "city");
                var postalCodeColumn = FindColumn(orderColumns, "PostalCode", "postal_code");
                var deliveryOptionColumn = FindColumn(orderColumns, "DeliveryOption", "delivery_option");
                var paymentMethodColumn = FindColumn(orderColumns, "PaymentMethod", "payment_method");
                var statusColumn = FindColumn(orderColumns, "Status", "status");
                var subtotalColumn = FindColumn(orderColumns, "Subtotal", "subtotal");
                var shippingFeeColumn = FindColumn(orderColumns, "ShippingFee", "shipping_fee");
                var totalAmountColumn = FindColumn(orderColumns, "TotalAmount", "total_amount");
                var orderDateColumn = FindColumn(orderColumns, "OrderDate", "order_date", "CreatedAt", "created_at");
                var estimatedDeliveryColumn = FindColumn(orderColumns, "EstimatedDeliveryDate", "estimated_delivery_date");
                var cancellationReasonColumn = FindColumn(orderColumns, "CancellationReason", "cancellation_reason");
                var quantityColumn = FindColumn(orderColumns, "Quantity", "quantity");
                var productNameColumn = FindColumn(orderColumns, "ProductName", "product_name");

                if (orderIdColumn is null)
                {
                    throw new InvalidOperationException("Orders table is missing its primary key column.");
                }

                await using var orderCommand = connection.CreateCommand();
                orderCommand.Transaction = transaction;
                var singleSellerId = vm.CartItems
                    .Select(item => item.SellerId)
                    .Distinct()
                    .Take(2)
                    .ToArray();

                var orderInsertColumns = new List<string>();
                var orderInsertValues = new List<string>();

                void AddOrderField(string? columnName, string parameterName, object? value, DbType dbType)
                {
                    if (string.IsNullOrWhiteSpace(columnName))
                    {
                        return;
                    }

                    orderInsertColumns.Add($"[{columnName}]");
                    orderInsertValues.Add(parameterName);
                    AddDbParameter(orderCommand, parameterName, value, dbType);
                }

                AddOrderField(orderNumberColumn, "@OrderNumber", orderReference, DbType.String);
                AddOrderField(userIdColumn, "@UserId", userId?.ToString(), DbType.String);
                AddOrderField(consumerIdColumn, "@ConsumerId", consumerId, DbType.Int32);
                AddOrderField(sellerIdColumn, "@SellerId", singleSellerId.Length == 1 && singleSellerId[0] > 0 ? singleSellerId[0] : null, DbType.Int32);
                AddOrderField(fullNameColumn, "@FullName", vm.FullName, DbType.String);
                AddOrderField(emailColumn, "@Email", vm.Email, DbType.String);
                AddOrderField(phoneColumn, "@PhoneNumber", vm.Phone, DbType.String);
                AddOrderField(streetColumn, "@StreetAddress", vm.Address, DbType.String);
                AddOrderField(cityColumn, "@City", vm.City, DbType.String);
                AddOrderField(postalCodeColumn, "@PostalCode", vm.PostalCode, DbType.String);
                AddOrderField(deliveryOptionColumn, "@DeliveryOption", vm.DeliveryOption, DbType.String);
                AddOrderField(paymentMethodColumn, "@PaymentMethod", vm.PaymentMethod, DbType.String);
                AddOrderField(statusColumn, "@Status", "Placed", DbType.String);
                AddOrderField(subtotalColumn, "@Subtotal", vm.Subtotal, DbType.Decimal);
                AddOrderField(shippingFeeColumn, "@ShippingFee", vm.ShippingFee, DbType.Decimal);
                AddOrderField(totalAmountColumn, "@TotalAmount", vm.Total, DbType.Decimal);
                AddOrderField(orderDateColumn, "@OrderDate", createdAt, DbType.DateTime2);
                AddOrderField(estimatedDeliveryColumn, "@EstimatedDeliveryDate", createdAt.AddDays(vm.DeliveryOption == "Express" ? 2 : 5), DbType.DateTime2);
                AddOrderField(cancellationReasonColumn, "@CancellationReason", null, DbType.String);
                AddOrderField(quantityColumn, "@Quantity", vm.CartItems.Sum(item => item.Quantity), DbType.Int32);
                AddOrderField(productNameColumn, "@ProductName", BuildOrderProductName(vm.CartItems), DbType.String);

                orderCommand.CommandText =
                    $"""
                    INSERT INTO dbo.Orders
                    (
                        {string.Join(", ", orderInsertColumns)}
                    )
                    VALUES
                    (
                        {string.Join(", ", orderInsertValues)}
                    );

                    SELECT CAST(SCOPE_IDENTITY() AS INT);
                    """;

                var insertedOrderIdValue = await orderCommand.ExecuteScalarAsync(cancellationToken);
                var insertedOrderId = insertedOrderIdValue switch
                {
                    int value => value,
                    decimal value => Convert.ToInt32(value),
                    long value => Convert.ToInt32(value),
                    _ => throw new InvalidOperationException("Failed to create order record.")
                };

                await using var itemCommand = connection.CreateCommand();
                itemCommand.Transaction = transaction;

                var orderItemOrderIdColumn = FindColumn(orderItemsColumns, "OrderId", "OrderID");
                var orderItemProductIdColumn = FindColumn(orderItemsColumns, "ProductId", "ProductID");
                var orderItemSellerIdColumn = FindColumn(orderItemsColumns, "SellerId", "SellerID", "seller_id");
                var orderItemSizeColumn = FindColumn(orderItemsColumns, "Size", "size");
                var orderItemColorColumn = FindColumn(orderItemsColumns, "Color", "color");
                var orderItemQuantityColumn = FindColumn(orderItemsColumns, "Quantity", "quantity");
                var orderItemUnitPriceColumn = FindColumn(orderItemsColumns, "UnitPrice", "unit_price", "Price");

                if (orderItemOrderIdColumn is null || orderItemProductIdColumn is null)
                {
                    throw new InvalidOperationException("OrderItems table is missing required columns.");
                }

                foreach (var item in vm.CartItems)
                {
                    itemCommand.Parameters.Clear();

                    var orderItemInsertColumns = new List<string>();
                    var orderItemInsertValues = new List<string>();

                    void AddOrderItemField(string? columnName, string parameterName, object? value, DbType dbType)
                    {
                        if (string.IsNullOrWhiteSpace(columnName))
                        {
                            return;
                        }

                        orderItemInsertColumns.Add($"[{columnName}]");
                        orderItemInsertValues.Add(parameterName);
                        AddDbParameter(itemCommand, parameterName, value, dbType);
                    }

                    AddOrderItemField(orderItemOrderIdColumn, "@OrderId", insertedOrderId, DbType.Int32);
                    AddOrderItemField(orderItemProductIdColumn, "@ProductId", item.ProductId, DbType.Int32);
                    AddOrderItemField(orderItemSellerIdColumn, "@SellerId", item.SellerId > 0 ? item.SellerId : null, DbType.Int32);
                    AddOrderItemField(orderItemSizeColumn, "@Size", item.Size, DbType.String);
                    AddOrderItemField(orderItemColorColumn, "@Color", null, DbType.String);
                    AddOrderItemField(orderItemQuantityColumn, "@Quantity", item.Quantity, DbType.Int32);
                    AddOrderItemField(orderItemUnitPriceColumn, "@UnitPrice", item.Price, DbType.Decimal);

                    itemCommand.CommandText =
                        $"""
                        INSERT INTO dbo.OrderItems
                        (
                            {string.Join(", ", orderItemInsertColumns)}
                        )
                        VALUES
                        (
                            {string.Join(", ", orderItemInsertValues)}
                        );
                        """;
                    await itemCommand.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
                return insertedOrderId;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }

        private Task EnsureNotificationsTableAsync(CancellationToken cancellationToken)
        {
            return _dbContext.Database.ExecuteSqlRawAsync(
                """
                IF OBJECT_ID(N'dbo.Notifications', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.Notifications
                    (
                        NotificationId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        RecipientType NVARCHAR(20) NOT NULL,
                        RecipientId INT NOT NULL,
                        OrderId INT NULL,
                        Message NVARCHAR(500) NOT NULL,
                        Category NVARCHAR(100) NULL,
                        IsRead BIT NOT NULL CONSTRAINT DF_Notifications_IsRead DEFAULT(0),
                        CreatedAt DATETIME NOT NULL CONSTRAINT DF_Notifications_CreatedAt DEFAULT(GETDATE())
                    );
                END

                IF COL_LENGTH(N'dbo.Notifications', N'Category') IS NULL
                BEGIN
                    ALTER TABLE dbo.Notifications
                    ADD Category NVARCHAR(100) NULL;
                END
                """,
                cancellationToken);
        }

        private Task EnsureOrderNotificationsBackfilledAsync(
            int? currentUserId,
            int? consumerId,
            CancellationToken cancellationToken)
        {
            if (!currentUserId.HasValue && !consumerId.HasValue)
            {
                return Task.CompletedTask;
            }

            var recipientId = consumerId ?? currentUserId;
            var currentUserIdText = currentUserId?.ToString();

            return _dbContext.Database.ExecuteSqlRawAsync(
                """
                INSERT INTO dbo.Notifications
                (
                    RecipientType,
                    RecipientId,
                    OrderId,
                    Message,
                    IsRead,
                    CreatedAt,
                    Category
                )
                SELECT
                    N'User',
                    @recipientId,
                    o.OrderID,
                    CONCAT(N'Order successfully placed. Order number: ', CONVERT(NVARCHAR(20), o.OrderID)),
                    0,
                    COALESCE(o.OrderDate, GETDATE()),
                    N'order'
                FROM dbo.Orders o
                WHERE
                    (
                        (@currentUserId IS NOT NULL AND LTRIM(RTRIM(ISNULL(o.UserID, N''))) = @currentUserIdText)
                        OR (@consumerId IS NOT NULL AND o.ConsumerID = @consumerId)
                    )
                    AND NOT EXISTS
                    (
                        SELECT 1
                        FROM dbo.Notifications n
                        WHERE n.RecipientType = N'User'
                          AND n.RecipientId = @recipientId
                          AND n.OrderId = o.OrderID
                          AND n.Category = N'order'
                    );
                """,
                new object[]
                {
                    new Microsoft.Data.SqlClient.SqlParameter("@recipientId", recipientId ?? (object)DBNull.Value),
                    new Microsoft.Data.SqlClient.SqlParameter("@currentUserId", currentUserId ?? (object)DBNull.Value),
                    new Microsoft.Data.SqlClient.SqlParameter("@currentUserIdText", currentUserIdText ?? (object)DBNull.Value),
                    new Microsoft.Data.SqlClient.SqlParameter("@consumerId", consumerId ?? (object)DBNull.Value)
                },
                cancellationToken);
        }

        private static string? BuildNotificationTargetUrl(string? category, int? orderId)
        {
            if (!string.Equals(category, "order", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(category, "OrderPlaced", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return orderId.HasValue
                ? $"{MyPurchasesAppUrl}?orderId={orderId.Value}"
                : MyPurchasesAppUrl;
        }

        private async Task MarkOrderHeaderNotificationReadAsync(
            int notificationId,
            int? currentUserId,
            int? consumerId,
            CancellationToken cancellationToken)
        {
            if (!currentUserId.HasValue && !consumerId.HasValue)
            {
                return;
            }

            var recipientId = consumerId ?? currentUserId;

            await _dbContext.Database.ExecuteSqlRawAsync(
                """
                UPDATE dbo.Notifications
                SET IsRead = 1
                WHERE NotificationId = @notificationId
                  AND RecipientType IN (N'User', N'Consumer')
                  AND (
                        (@recipientUserId IS NOT NULL AND RecipientId = @recipientUserId)
                     OR (@recipientConsumerId IS NOT NULL AND RecipientId = @recipientConsumerId)
                     OR (@recipientId IS NOT NULL AND RecipientId = @recipientId)
                  )
                  AND (
                        OrderId IS NULL
                     OR EXISTS
                        (
                            SELECT 1
                            FROM dbo.Orders o
                            WHERE o.OrderID = dbo.Notifications.OrderId
                              AND (
                                    (@recipientUserId IS NOT NULL AND LTRIM(RTRIM(ISNULL(o.UserID, N''))) = @recipientUserIdText)
                                 OR (@recipientConsumerId IS NOT NULL AND o.ConsumerID = @recipientConsumerId)
                                  )
                        )
                  );
                """,
                new object[]
                {
                    new Microsoft.Data.SqlClient.SqlParameter("@notificationId", notificationId),
                    new Microsoft.Data.SqlClient.SqlParameter("@recipientUserId", currentUserId ?? (object)DBNull.Value),
                    new Microsoft.Data.SqlClient.SqlParameter("@recipientUserIdText", currentUserId?.ToString() ?? (object)DBNull.Value),
                    new Microsoft.Data.SqlClient.SqlParameter("@recipientConsumerId", consumerId ?? (object)DBNull.Value),
                    new Microsoft.Data.SqlClient.SqlParameter("@recipientId", recipientId ?? (object)DBNull.Value)
                },
                cancellationToken);
        }

        private static void AddDbParameter(DbCommand command, string name, object? value, DbType dbType)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.DbType = dbType;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }

        private sealed record NotificationRow(
            int NotificationId,
            int? OrderId,
            string Message,
            bool IsRead,
            DateTime CreatedAt,
            string? Category);

        private sealed record HeaderNotificationEntry(
            int NotificationId,
            string Message,
            bool IsRead,
            DateTime CreatedAt,
            string? TargetUrl);

        private sealed record NotificationUserContext(
            int? UserId,
            int? ConsumerId,
            string? Email);

        public sealed class MarkHeaderNotificationReadRequest
        {
            public int NotificationId { get; set; }
        }

        private string GetCurrentUserEmail()
        {
            return HttpContext.Session.GetString("UserEmail")
                ?? Request.Cookies[SharedUserEmailCookie]
                ?? string.Empty;
        }

        private string GetCurrentDisplayName()
        {
            return HttpContext.Session.GetString("DisplayName")
                ?? Request.Cookies[SharedDisplayNameCookie]
                ?? string.Empty;
        }

        private static string BuildLoginRedirectUrl(string targetUrl)
        {
            return $"{LoginAppUrl}?returnUrl={Uri.EscapeDataString(targetUrl)}";
        }

        private void SyncSharedCartCookie()
        {
            Response.Cookies.Append(
                "NextHorizon.SharedCart",
                JsonSerializer.Serialize(ProductData.GetCartSnapshot()),
                new CookieOptions
                {
                    HttpOnly = true,
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Path = "/",
                    Expires = DateTimeOffset.UtcNow.AddHours(8)
                });
        }

        private async Task EnsureChallengesTableAsync(CancellationToken cancellationToken)
        {
            const string sql = """
                IF OBJECT_ID(N'dbo.challenges', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.challenges (
                        challenge_id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                        title NVARCHAR(255) NOT NULL,
                        description NVARCHAR(MAX) NOT NULL,
                        rules NVARCHAR(MAX) NOT NULL,
                        prizes NVARCHAR(MAX) NOT NULL,
                        goal_km DECIMAL(10,2) NOT NULL,
                        activity_type NVARCHAR(50) NOT NULL,
                        start_date DATETIME2 NOT NULL,
                        end_date DATETIME2 NOT NULL,
                        status NVARCHAR(20) NULL,
                        banner_image VARBINARY(MAX) NULL,
                        banner_image_name NVARCHAR(255) NULL,
                        banner_image_content_type NVARCHAR(100) NULL,
                        created_by INT NOT NULL CONSTRAINT DF_challenges_created_by DEFAULT 0,
                        created_at DATETIME2 NULL,
                        updated_at DATETIME2 NULL,
                        updated_by INT NULL,
                        total_participants INT NULL,
                        total_completed INT NULL
                    );
                END
                """;

            await _dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }

        private async Task<ChallengesPageViewModel> BuildChallengesPageViewModelAsync()
        {
            var now = DateTime.Now;
            var currentUserId = GetCurrentUserId();
            var rawChallenges = await _dbContext.Challenges
                .AsNoTracking()
                .OrderBy(c => c.StartDate)
                .ToListAsync();

            var allCards = rawChallenges.Select(ToChallengeCard).ToList();
            var myParticipants = currentUserId.HasValue
                ? await _dbContext.ChallengeParticipants
                    .AsNoTracking()
                    .Where(x => x.UserId == currentUserId.Value)
                    .OrderByDescending(x => x.JoinedAt ?? DateTime.MinValue)
                    .ThenByDescending(x => x.ParticipantId)
                    .ToListAsync()
                : new List<DbChallengeParticipant>();
            var myActivityRows = currentUserId.HasValue && myParticipants.Count > 0
                ? await _dbContext.ChallengeActivities
                    .AsNoTracking()
                    .Where(x => x.UserId == currentUserId.Value && myParticipants.Select(p => p.ParticipantId).Contains(x.ParticipantId))
                    .ToListAsync()
                : new List<DbChallengeActivity>();
            var active = allCards
                .Where(c => IsChallengeOpenForActivityUpload(c.StartDate, c.EndDate, now.Date))
                .OrderBy(c => c.EndDate)
                .ToList();
            var upcoming = allCards
                .Where(c => c.StartDate > now || string.Equals(c.Status, "upcoming", StringComparison.OrdinalIgnoreCase))
                .OrderBy(c => c.StartDate)
                .ToList();
            var completed = allCards
                .Where(c =>
                    string.Equals(c.Status, "completed", StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(c.Status, "finished", StringComparison.OrdinalIgnoreCase) ||
                    c.EndDate < now)
                .Where(c => !active.Any(a => a.ChallengeId == c.ChallengeId))
                .Where(c => !upcoming.Any(u => u.ChallengeId == c.ChallengeId))
                .OrderByDescending(c => c.EndDate)
                .ToList();
            var featured = active.FirstOrDefault() ?? upcoming.FirstOrDefault();
            var nextDropDate = upcoming.FirstOrDefault()?.StartDate;
            var availableChallenges = allCards
                .Where(c => !completed.Any(done => done.ChallengeId == c.ChallengeId))
                .Where(c => featured == null || c.ChallengeId != featured.ChallengeId)
                .OrderBy(c => c.StartDate)
                .ToList();
            var activitySummaryByParticipantId = myActivityRows
                .GroupBy(x => x.ParticipantId)
                .ToDictionary(
                    group => group.Key,
                    group => new ChallengeActivitySummary
                    {
                        TotalActivities = group.Count(),
                        TotalDistanceKm = group.Sum(x => x.DistanceKm),
                        TotalTimeSeconds = group.Sum(x => x.DurationSeconds),
                        LastActivityDate = group.Max(x => x.ActivityDate)
                    });
            var challengeById = allCards.ToDictionary(x => x.ChallengeId);
            var myActivityCards = myParticipants
                .Where(x => challengeById.ContainsKey(x.ChallengeId))
                .Select(x => ToParticipationCard(x, challengeById[x.ChallengeId], activitySummaryByParticipantId, now.Date))
                .ToList();
            var uploadedActivities = myActivityRows
                .Where(x => challengeById.ContainsKey(x.ChallengeId))
                .OrderByDescending(x => x.ActivityDate)
                .ThenByDescending(x => x.ActivityId)
                .Select(x => ToUploadedActivityCard(x, challengeById[x.ChallengeId]))
                .ToList();
            await PopulateChallengeLeaderboardsAsync(allCards);

            return new ChallengesPageViewModel
            {
                SeasonLabel = $"{now.Year} Season",
                ActiveCount = active.Count,
                TotalParticipants = active.Sum(c => c.TotalParticipants),
                TotalGoalKm = active.Sum(c => c.GoalKm),
                DaysUntilNextDrop = nextDropDate.HasValue ? Math.Max(0, (nextDropDate.Value.Date - now.Date).Days) : 0,
                FeaturedChallenge = featured,
                MyActivities = myActivityCards,
                UploadedActivities = uploadedActivities,
                MyActivitiesCount = myActivityCards.Count,
                ApprovedActivitiesCount = myActivityCards.Count(x => x.CanUpload),
                PendingActivitiesCount = myActivityCards.Count(x => !x.CanUpload),
                UpcomingChallenges = availableChallenges,
                CompletedChallenges = completed,
                TopChallenges = allCards
                    .OrderByDescending(c => c.CompletionPercent)
                    .ThenByDescending(c => c.TotalParticipants)
                    .Take(5)
                    .ToList(),
                JoinChallengePrefill = new JoinChallengeViewModel
                {
                    FullName = GetCurrentDisplayName(),
                    Email = GetCurrentUserEmail()
                },
                UploadActivityPrefill = new UploadChallengeActivityViewModel
                {
                    ActivityDate = now.Date,
                    ActivityType = "Run",
                    Hours = 0,
                    Minutes = 0,
                    Seconds = 0
                }
            };
        }

        private ChallengeUploadedActivityViewModel ToUploadedActivityCard(
            DbChallengeActivity activity,
            ChallengeCardViewModel challenge)
        {
            var isVerified = activity.IsVerified.GetValueOrDefault();

            return new ChallengeUploadedActivityViewModel
            {
                ActivityId = activity.ActivityId,
                ChallengeTitle = challenge.Title,
                ActivityType = string.IsNullOrWhiteSpace(activity.ActivityType) ? challenge.ActivityType : activity.ActivityType,
                VerificationLabel = isVerified ? "Verified" : "Pending",
                VerificationCssClass = isVerified ? "ok" : "pending",
                ActivityDateDisplay = activity.ActivityDate.ToString("MMM d, yyyy"),
                DistanceDisplay = $"{activity.DistanceKm:N2} km",
                DurationDisplay = FormatDuration(activity.DurationSeconds),
                AveragePaceDisplay = FormatPace(CalculateAverageSpeedKmPerHour(activity.DistanceKm, activity.DurationSeconds)),
                ProofImageUrl = activity.ImageProof != null && activity.ImageProof.Length > 0
                    ? Url.Action(nameof(ChallengeActivityImage), "Home", new { id = activity.ActivityId }) ?? string.Empty
                    : string.Empty,
                HasProofImage = activity.ImageProof != null && activity.ImageProof.Length > 0,
                Notes = string.IsNullOrWhiteSpace(activity.Notes) ? "No notes added." : activity.Notes.Trim()
            };
        }

        private static ChallengeParticipationCardViewModel ToParticipationCard(
            DbChallengeParticipant participant,
            ChallengeCardViewModel challenge,
            IReadOnlyDictionary<int, ChallengeActivitySummary> activitySummaryByParticipantId,
            DateTime today)
        {
            activitySummaryByParticipantId.TryGetValue(participant.ParticipantId, out var summary);
            var totalActivities = summary is null ? participant.TotalActivities ?? 0 : summary.TotalActivities;
            var totalDistance = summary is null ? participant.TotalDistanceKm ?? 0m : summary.TotalDistanceKm;
            var totalTime = summary is null ? participant.TotalTimeSeconds ?? 0 : summary.TotalTimeSeconds;
            var lastActivityDate = summary is null ? participant.LastActivityDate : summary.LastActivityDate;
            var normalizedStatus = NormalizeParticipationStatus(participant.Status);
            var isOpenForUpload = IsChallengeOpenForActivityUpload(challenge.StartDate, challenge.EndDate, today);
            var isExpired = challenge.EndDate.Date < today.Date;
            var canUpload = CanUploadChallengeActivity(participant.Status) && isOpenForUpload;
            var statusLabel = canUpload
                ? "Approved"
                : isExpired
                    ? "Expired"
                    : CanUploadChallengeActivity(participant.Status)
                        ? "Not Open"
                        : "Pending";

            return new ChallengeParticipationCardViewModel
            {
                ParticipantId = participant.ParticipantId,
                ChallengeId = challenge.ChallengeId,
                ChallengeTitle = challenge.Title,
                ChallengeDescription = challenge.Description,
                ActivityType = challenge.ActivityType,
                Status = normalizedStatus,
                StatusLabel = statusLabel,
                CanUpload = canUpload,
                IsExpired = isExpired,
                GoalKm = challenge.GoalKm,
                GoalDisplay = $"{challenge.GoalKm:N1} km goal",
                TotalActivities = totalActivities,
                TotalDistanceKm = totalDistance,
                TotalDistanceDisplay = $"{totalDistance:N2} km",
                TotalTimeSeconds = totalTime,
                TotalTimeDisplay = FormatDuration(totalTime),
                LastActivityDisplay = lastActivityDate.HasValue ? lastActivityDate.Value.ToString("MMM d, yyyy") : "No activity yet",
                Notes = canUpload
                    ? "Approved. You can now submit your member-style challenge activity form."
                    : isExpired
                        ? $"Challenge ended on {challenge.EndDate:MMM d, yyyy}. Activity uploads are closed."
                        : CanUploadChallengeActivity(participant.Status)
                            ? $"Challenge opens for activity uploads on {challenge.StartDate:MMM d, yyyy}."
                            : "Pending approval. Activity upload will be enabled once the challenge admin approves your participation."
            };
        }

        private static string FormatDuration(int totalSeconds)
        {
            if (totalSeconds <= 0)
            {
                return "-";
            }

            var hours = totalSeconds / 3600;
            var minutes = (totalSeconds % 3600) / 60;
            var seconds = totalSeconds % 60;

            if (hours > 0)
            {
                return $"{hours}h {minutes}m {seconds}s";
            }

            return $"{minutes}m {seconds}s";
        }

        private static decimal? CalculateAverageSpeedKmPerHour(decimal? distanceKm, int durationSeconds)
        {
            if (!distanceKm.HasValue || distanceKm.Value <= 0 || durationSeconds <= 0)
            {
                return null;
            }

            return decimal.Round(distanceKm.Value / ((decimal)durationSeconds / 3600m), 2, MidpointRounding.AwayFromZero);
        }

        private static string FormatPace(decimal? averagePace)
        {
            if (!averagePace.HasValue || averagePace.Value <= 0)
            {
                return "-";
            }

            return $"{averagePace.Value:N2} km / hr";
        }

        private static string ResolveImageContentType(byte[] bytes)
        {
            if (bytes.Length >= 8 &&
                bytes[0] == 0x89 &&
                bytes[1] == 0x50 &&
                bytes[2] == 0x4E &&
                bytes[3] == 0x47)
            {
                return "image/png";
            }

            if (bytes.Length >= 3 &&
                bytes[0] == 0xFF &&
                bytes[1] == 0xD8 &&
                bytes[2] == 0xFF)
            {
                return "image/jpeg";
            }

            if (bytes.Length >= 12 &&
                bytes[0] == 0x52 &&
                bytes[1] == 0x49 &&
                bytes[2] == 0x46 &&
                bytes[3] == 0x46 &&
                bytes[8] == 0x57 &&
                bytes[9] == 0x45 &&
                bytes[10] == 0x42 &&
                bytes[11] == 0x50)
            {
                return "image/webp";
            }

            return "application/octet-stream";
        }

        private static string NormalizeParticipationStatus(string? status)
        {
            return string.IsNullOrWhiteSpace(status) ? "pending" : status.Trim().ToLowerInvariant();
        }

        private static bool CanUploadChallengeActivity(string? status)
        {
            var normalized = NormalizeParticipationStatus(status);
            return normalized is "approved" or "active";
        }

        private static bool IsChallengeOpenForActivityUpload(DateTime startDate, DateTime endDate, DateTime today)
        {
            return startDate.Date <= today.Date && endDate.Date >= today.Date;
        }

        private static bool IsAllowedChallengeActivityImage(IFormFile file)
        {
            if (file.Length <= 0 || file.Length > 5 * 1024 * 1024)
            {
                return false;
            }

            var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".webp" };
            var extension = Path.GetExtension(file.FileName);
            return !string.IsNullOrWhiteSpace(extension) &&
                   allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
        }

        private sealed class ChallengeActivitySummary
        {
            public int TotalActivities { get; init; }
            public decimal TotalDistanceKm { get; init; }
            public int TotalTimeSeconds { get; init; }
            public DateTime? LastActivityDate { get; init; }
        }

        private static ChallengeCardViewModel ToChallengeCard(DbChallenge challenge)
        {
            var totalParticipants = challenge.TotalParticipants ?? 0;
            var totalCompleted = challenge.TotalCompleted ?? 0;
            var completionPercent = totalParticipants > 0
                ? Math.Round((double)totalCompleted / totalParticipants * 100, 0)
                : 0;
            var durationDays = Math.Max(1, (challenge.EndDate.Date - challenge.StartDate.Date).Days + 1);
            var (difficultyLabel, difficultyCssClass) = challenge.GoalKm switch
            {
                <= 10m => ("Easy", "diff-easy"),
                <= 30m => ("Medium", "diff-med"),
                _ => ("Hard", "diff-hard")
            };

            return new ChallengeCardViewModel
            {
                ChallengeId = challenge.ChallengeId,
                Title = challenge.Title,
                Description = challenge.Description,
                Rules = challenge.Rules,
                Prizes = challenge.Prizes,
                GoalKm = challenge.GoalKm,
                ActivityType = challenge.ActivityType,
                StartDate = challenge.StartDate,
                EndDate = challenge.EndDate,
                Status = challenge.Status ?? string.Empty,
                TotalParticipants = totalParticipants,
                TotalCompleted = totalCompleted,
                CompletionPercent = completionPercent,
                DurationDays = durationDays,
                DifficultyLabel = difficultyLabel,
                DifficultyCssClass = difficultyCssClass,
                HasBannerImage = challenge.BannerImage != null && challenge.BannerImage.Length > 0
            };
        }

        private async Task PopulateChallengeLeaderboardsAsync(List<ChallengeCardViewModel> challenges, CancellationToken cancellationToken = default)
        {
            if (challenges.Count == 0)
            {
                return;
            }

            var challengeIds = challenges.Select(x => x.ChallengeId).ToList();
            var participantRows = await (
                from participant in _dbContext.ChallengeParticipants.AsNoTracking()
                join consumer in _dbContext.Consumers.AsNoTracking()
                    on participant.UserId equals consumer.UserId into consumerGroup
                from consumer in consumerGroup.DefaultIfEmpty()
                join user in _dbContext.Users.AsNoTracking()
                    on participant.UserId equals user.UserId into userGroup
                from user in userGroup.DefaultIfEmpty()
                where challengeIds.Contains(participant.ChallengeId) &&
                      (participant.Status == "Approved" || participant.Status == "Active")
                select new
                {
                    participant.ChallengeId,
                    participant.ParticipantId,
                    participant.UserId,
                    participant.TotalDistanceKm,
                    participant.TotalTimeSeconds,
                    ConsumerUsername = consumer != null ? consumer.Username : null,
                    ConsumerFirstName = consumer != null ? consumer.FirstName : null,
                    ConsumerLastName = consumer != null ? consumer.LastName : null,
                    HasProfilePicture = user != null && user.ProfilePicture != null && user.ProfilePicture.Length > 0,
                    ProfileImageVersion = user != null ? (user.UpdatedAt ?? user.CreatedAt) : null
                })
                .ToListAsync(cancellationToken);

            var entriesByChallengeId = participantRows
                .GroupBy(x => x.ChallengeId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderByDescending(x => x.TotalDistanceKm ?? 0m)
                        .ThenBy(x => x.TotalTimeSeconds ?? int.MaxValue)
                        .ThenBy(x => BuildParticipantName(x.ConsumerUsername, x.ConsumerFirstName, x.ConsumerLastName))
                        .Take(3)
                        .Select((x, index) => new ChallengeLeaderboardEntryViewModel
                        {
                            Rank = index + 1,
                            UserId = x.UserId,
                            AthleteName = BuildParticipantName(x.ConsumerUsername, x.ConsumerFirstName, x.ConsumerLastName),
                            AvatarUrl = x.HasProfilePicture
                                ? $"/Leaderboard/ProfileImage/{x.UserId}?v={(x.ProfileImageVersion?.Ticks ?? 0)}"
                                : $"https://i.pravatar.cc/150?u=challenge-{x.ParticipantId}",
                            DistanceDisplay = $"{(x.TotalDistanceKm ?? 0m):N2} km",
                            DurationDisplay = FormatDuration(x.TotalTimeSeconds ?? 0)
                        })
                        .ToList());

            foreach (var challenge in challenges)
            {
                if (entriesByChallengeId.TryGetValue(challenge.ChallengeId, out var entries))
                {
                    challenge.LeaderboardEntries = entries;
                }
            }
        }

        private static string BuildParticipantName(string? username, string? firstName, string? lastName)
        {
            if (!string.IsNullOrWhiteSpace(username))
            {
                return username.Trim();
            }

            var fullName = string.Join(" ", new[] { firstName, lastName }.Where(x => !string.IsNullOrWhiteSpace(x)).Select(x => x!.Trim()));
            return string.IsNullOrWhiteSpace(fullName) ? "Challenge Runner" : fullName;
        }

        private sealed record LatestCheckoutProfile(
            string FullName,
            string Email,
            string Phone,
            string Address,
            string City,
            string PostalCode,
            string PaymentMethod);
    }
}
