using Microsoft.EntityFrameworkCore;
using MyAspNetApp.Data;
using MyAspNetApp.Models;
using Microsoft.Data.SqlClient;

namespace MyAspNetApp.Services
{
    public class ChallengeNotificationService
    {
        private readonly AppDbContext _dbContext;

        public ChallengeNotificationService(AppDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task<IReadOnlyList<DbChallengeNotification>> GetHeaderNotificationsAsync(HttpContext httpContext, CancellationToken cancellationToken = default)
        {
            if (!TryGetCurrentUserId(httpContext, out var userId))
            {
                return Array.Empty<DbChallengeNotification>();
            }

            var configuredConnectionString = _dbContext.Database.GetConnectionString();
            if (string.IsNullOrWhiteSpace(configuredConnectionString))
            {
                return Array.Empty<DbChallengeNotification>();
            }

            try
            {
                var approvedRegistrations = await _dbContext.ChallengeRegistrations
                    .Where(x => x.UserId == userId
                        && x.Status == "Approved"
                        && x.ApprovalNotifiedAt == null)
                    .ToListAsync(cancellationToken);

                if (approvedRegistrations.Count > 0)
                {
                    var registrationIds = approvedRegistrations
                        .Select(x => x.RegistrationId)
                        .ToList();

                    var challengeIds = approvedRegistrations
                        .Select(x => x.ChallengeId)
                        .Distinct()
                        .ToList();

                    var challengeTitles = await _dbContext.Challenges
                        .Where(x => challengeIds.Contains(x.ChallengeId))
                        .ToDictionaryAsync(x => x.ChallengeId, x => x.Title, cancellationToken);

                    var existingNotificationRegistrationIds = await _dbContext.ChallengeNotifications
                        .Where(x => x.RegistrationId.HasValue
                            && registrationIds.Contains(x.RegistrationId.Value)
                            && x.NotificationType == "challenge-approval")
                        .Select(x => x.RegistrationId!.Value)
                        .ToListAsync(cancellationToken);

                    var existingNotificationRegistrationIdSet = existingNotificationRegistrationIds.ToHashSet();
                    var now = DateTime.UtcNow;

                    foreach (var registration in approvedRegistrations)
                    {
                        if (!existingNotificationRegistrationIdSet.Contains(registration.RegistrationId))
                        {
                            var challengeTitle = challengeTitles.TryGetValue(registration.ChallengeId, out var title)
                                ? title
                                : "Challenge";

                            _dbContext.ChallengeNotifications.Add(new DbChallengeNotification
                            {
                                UserId = userId,
                                RegistrationId = registration.RegistrationId,
                                Title = "Challenge registration approved",
                                Message = $"Your registration approved, you can now join the challenge: {challengeTitle}.",
                                NotificationType = "challenge-approval",
                                IsRead = false,
                                CreatedAt = now
                            });
                        }

                        registration.ApprovalNotifiedAt = now;
                    }

                    await _dbContext.SaveChangesAsync(cancellationToken);
                }

                return await _dbContext.ChallengeNotifications
                    .AsNoTracking()
                    .Where(x => x.UserId == userId)
                    .OrderByDescending(x => x.CreatedAt)
                    .Take(10)
                    .ToListAsync(cancellationToken);
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("ConnectionString", StringComparison.OrdinalIgnoreCase))
            {
                return Array.Empty<DbChallengeNotification>();
            }
            catch (SqlException)
            {
                return Array.Empty<DbChallengeNotification>();
            }
        }

        private static bool TryGetCurrentUserId(HttpContext httpContext, out int userId)
        {
            userId = httpContext.Session.GetInt32("UserId") ?? 0;
            if (userId > 0)
            {
                return true;
            }

            return int.TryParse(httpContext.Request.Cookies["NextHorizon.SharedUserId"], out userId) && userId > 0;
        }
    }
}
