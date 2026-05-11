using System.Data;
using System.IO;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MyAspNetApp.Data;
using MyAspNetApp.Models.ViewModels;

namespace MyAspNetApp.Services
{
    public class LeaderboardService
    {
        private readonly AppDbContext _db;
        private readonly ILogger<LeaderboardService> _logger;
        private readonly IWebHostEnvironment _environment;

        public LeaderboardService(AppDbContext db, ILogger<LeaderboardService> logger, IWebHostEnvironment environment)
        {
            _db = db;
            _logger = logger;
            _environment = environment;
        }

        public async Task<LeaderboardPageViewModel> GetLeaderboardAsync(int take = 12, CancellationToken cancellationToken = default)
        {
            try
            {
                var entries = await ReadLeaderboardEntriesAsync(take, cancellationToken);
                return BuildPage(entries);
            }
            catch (SqlException ex) when (ex.Number == 208)
            {
                _logger.LogWarning(ex, "leaderboard_records table was not found. Returning an empty leaderboard.");
                return BuildPage(new List<LeaderboardEntryViewModel>());
            }
            catch (Exception ex) when (ex is SqlException || ex is TimeoutException)
            {
                _logger.LogWarning(ex, "Unable to load leaderboard due to database connectivity issues. Returning an empty leaderboard.");
                return BuildPage(new List<LeaderboardEntryViewModel>());
            }
        }

        private async Task<List<LeaderboardEntryViewModel>> ReadLeaderboardEntriesAsync(int take, CancellationToken cancellationToken)
        {
            var entries = new List<LeaderboardRow>();
            var connection = _db.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;

            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = @"
WITH ranked_records AS
(
    SELECT
                ROW_NUMBER() OVER
                (
                    ORDER BY
                        lr.DistanceKm DESC,
                        CASE WHEN lr.DurationSeconds <= 0 THEN 2147483647 ELSE lr.DurationSeconds END ASC,
                        COALESCE(NULLIF(LTRIM(RTRIM(lr.AthleteName)), ''), NULLIF(LTRIM(RTRIM(c.username)), ''), CONVERT(nvarchar(20), lr.UserId)) ASC,
                        lr.UploadId DESC
                ) AS Id,
        lr.UploadId,
        lr.UserId,
        COALESCE(
            NULLIF(LTRIM(RTRIM(c.username)), ''),
            NULLIF(
                LTRIM(RTRIM(
                    CONCAT(
                        COALESCE(c.first_name, ''),
                        CASE
                            WHEN NULLIF(LTRIM(RTRIM(c.last_name)), '') IS NULL THEN ''
                            ELSE CASE
                                WHEN NULLIF(LTRIM(RTRIM(c.first_name)), '') IS NULL THEN ''
                                ELSE ' '
                            END + LTRIM(RTRIM(c.last_name))
                        END
                    )
                )),
                ''
            ),
            NULLIF(LTRIM(RTRIM(lr.AthleteName)), ''),
            ''
        ) AS AthleteName,
        lr.AvatarUrl,
        COALESCE(NULLIF(LTRIM(RTRIM(lr.CoverImageUrl)), ''), mu.ProofUrl) AS CoverImageUrl,
        lr.DistanceKm,
        lr.DurationSeconds,
        lr.Scope,
        lr.CategoryLabel,
        lr.RankChange,
        lr.IsVerified,
        lr.IsActive,
        lr.CreatedAtUtc
    FROM dbo.leaderboard_records lr
    LEFT JOIN dbo.MemberUploads mu
        ON mu.UploadId = lr.UploadId
    LEFT JOIN dbo.Consumers c
        ON c.user_id = lr.UserId
    LEFT JOIN dbo.Users u
        ON u.user_id = lr.UserId
    WHERE lr.IsActive = 1
)
SELECT TOP (@take) *
FROM ranked_records
ORDER BY Id ASC;";
                command.CommandType = CommandType.Text;

                var takeParameter = command.CreateParameter();
                takeParameter.ParameterName = "@take";
                takeParameter.Value = take;
                takeParameter.DbType = DbType.Int32;
                command.Parameters.Add(takeParameter);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                var ordinalMap = BuildOrdinalMap(reader);

                while (await reader.ReadAsync(cancellationToken))
                {
                    var row = new LeaderboardRow
                    {
                        UploadId = GetInt(reader, ordinalMap, "upload_id", "uploadid"),
                        UserId = GetInt(reader, ordinalMap, "user_id", "userid"),
                        AthleteName = GetString(reader, ordinalMap, "athlete_name", "athletename", "name", "user_name", "username", "full_name", "fullname", "runner_name"),
                        AvatarUrl = GetString(reader, ordinalMap, "avatar_url", "avatar", "profile_image", "profile_img", "image_url", "user_avatar"),
                        CoverImageUrl = GetString(reader, ordinalMap, "cover_image_url", "coverimageurl", "cover_url", "cover_image", "image", "banner_url", "photo_url", "proof_url", "proofurl"),
                        CategoryLabel = GetString(reader, ordinalMap, "category_label", "categorylabel", "category", "season_label", "period_label", "week_label"),
                        Scope = GetString(reader, ordinalMap, "scope", "leaderboard_scope", "ranking_scope", "region", "group_name"),
                        DistanceKm = GetDecimal(reader, ordinalMap, "distance_km", "distancekm", "distance", "km", "total_km", "total_distance", "total_distance_km"),
                        DurationSeconds = GetInt(reader, ordinalMap, "duration_seconds", "durationseconds", "duration", "elapsed_seconds", "elapsed_time_seconds", "time_seconds", "seconds"),
                        RankChange = GetInt(reader, ordinalMap, "rank_change", "rankchange", "change", "position_change", "movement"),
                        IsVerified = GetBool(reader, ordinalMap, defaultValue: true, "is_verified", "isverified", "verified", "is_approved"),
                        IsActive = GetBool(reader, ordinalMap, defaultValue: true, "is_active", "isactive", "active", "enabled"),
                        CreatedAtUtc = GetDateTime(reader, ordinalMap, "created_at_utc", "createdatutc", "created_at", "createdon", "date_created", "timestamp")
                    };

                    if (row.IsActive)
                    {
                        entries.Add(row);
                    }
                }
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }

            var sorted = entries
                .OrderByDescending(x => x.DistanceKm)
                .ThenBy(x => x.DurationSeconds == 0 ? int.MaxValue : x.DurationSeconds)
                .ThenBy(x => x.AthleteName)
                .Take(take)
                .ToList();

            var userIds = sorted
                .Where(item => item.UserId > 0)
                .Select(item => item.UserId)
                .Distinct()
                .ToList();

            var userProfileMap = await _db.Users
                .AsNoTracking()
                .Where(item => userIds.Contains(item.UserId))
                .Select(item => new
                {
                    item.UserId,
                    HasProfilePicture = item.ProfilePicture != null && item.ProfilePicture.Length > 0,
                    Version = item.UpdatedAt ?? item.CreatedAt
                })
                .ToDictionaryAsync(item => item.UserId, cancellationToken);

            var bestDistance = sorted.Count == 0 ? 0m : sorted.Max(x => x.DistanceKm);
            return sorted.Select((row, index) => new LeaderboardEntryViewModel
            {
                Rank = index + 1,
                UploadId = row.UploadId,
                UserId = row.UserId,
                AthleteName = row.AthleteName,
                AvatarUrl = userProfileMap.TryGetValue(row.UserId, out var userProfile) && userProfile.HasProfilePicture
                    ? $"/Leaderboard/ProfileImage/{row.UserId}?v={(userProfile.Version?.Ticks ?? 0)}"
                    : NormalizeImageUrl(row.AvatarUrl, string.Empty),
                CoverImageUrl = row.UploadId > 0
                    ? $"/Leaderboard/CoverImage/{row.UploadId}"
                    : NormalizeImageUrl(row.CoverImageUrl, string.Empty),
                DistanceDisplay = $"{row.DistanceKm:0.##} km",
                DurationDisplay = FormatDuration(row.DurationSeconds),
                PaceDisplay = FormatPace(row.DistanceKm, row.DurationSeconds),
                Tag = $"{(string.IsNullOrWhiteSpace(row.Scope) ? "National" : row.Scope)} - {(string.IsNullOrWhiteSpace(row.CategoryLabel) ? "Current Season" : row.CategoryLabel)}",
                ChangeDisplay = FormatRankChange(row.RankChange),
                ChangeCssClass = GetRankChangeCss(row.RankChange),
                ProgressPercent = bestDistance <= 0 ? 0 : Math.Round((row.DistanceKm / bestDistance) * 100m, 0)
            }).ToList();
        }

        public async Task<LeaderboardImageResult?> GetProfileImageAsync(int userId, CancellationToken cancellationToken = default)
        {
            if (userId <= 0)
            {
                return null;
            }

            var user = await _db.Users
                .AsNoTracking()
                .Where(item => item.UserId == userId)
                .Select(item => new
                {
                    item.ProfilePicture,
                    item.ProfilePictureContentType
                })
                .FirstOrDefaultAsync(cancellationToken);

            if (user?.ProfilePicture == null || user.ProfilePicture.Length == 0)
            {
                return null;
            }

            return new LeaderboardImageResult(
                user.ProfilePicture,
                string.IsNullOrWhiteSpace(user.ProfilePictureContentType) ? DetectImageContentType(user.ProfilePicture) : user.ProfilePictureContentType,
                null);
        }

        public async Task<LeaderboardImageResult?> GetCoverImageAsync(int uploadId, CancellationToken cancellationToken = default)
        {
            if (uploadId <= 0)
            {
                return null;
            }

            var fromUpload = await TryReadImageRowAsync("MemberUploads", "UploadId", uploadId, cancellationToken);
            if (fromUpload?.Bytes?.Length > 0)
            {
                return fromUpload;
            }

            var fromLeaderboard = await TryReadImageRowAsync("leaderboard_records", "UploadId", uploadId, cancellationToken);
            if (fromLeaderboard?.Bytes?.Length > 0)
            {
                return fromLeaderboard;
            }

            var leaderboardUrl = await TryReadStringColumnAsync("leaderboard_records", "CoverImageUrl", "UploadId", uploadId, cancellationToken);
            var memberProofUrl = await TryReadStringColumnAsync("MemberUploads", "ProofUrl", "UploadId", uploadId, cancellationToken);
            var redirectUrl = NormalizeImageUrl(leaderboardUrl, string.Empty);

            if (string.IsNullOrWhiteSpace(redirectUrl))
            {
                redirectUrl = NormalizeImageUrl(memberProofUrl, string.Empty);
            }

            var localFilePath = ResolveWorkspaceImagePath(memberProofUrl) ?? ResolveWorkspaceImagePath(leaderboardUrl);
            if (!string.IsNullOrWhiteSpace(localFilePath))
            {
                return new LeaderboardImageResult(null, GetContentTypeFromPath(localFilePath), null, localFilePath);
            }

            var nearbyUploadProof = await TryFindExistingMemberUploadProofAsync(uploadId, cancellationToken);
            if (nearbyUploadProof is not null)
            {
                return nearbyUploadProof;
            }

            return string.IsNullOrWhiteSpace(redirectUrl)
                ? null
                : new LeaderboardImageResult(null, null, redirectUrl, null);
        }

        private async Task<LeaderboardImageResult?> TryFindExistingMemberUploadProofAsync(int uploadId, CancellationToken cancellationToken)
        {
            var connection = _db.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;

            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = @"
DECLARE @UserId int;

SELECT @UserId = UserId
FROM dbo.MemberUploads
WHERE UploadId = @uploadId;

SELECT TOP (20) ProofUrl
FROM dbo.MemberUploads
WHERE @UserId IS NOT NULL
  AND UserId = @UserId
  AND NULLIF(LTRIM(RTRIM(ProofUrl)), '') IS NOT NULL
ORDER BY
    CASE WHEN UploadId = @uploadId THEN 0 ELSE 1 END,
    CreatedAt DESC,
    UploadId DESC;";
                command.CommandType = CommandType.Text;

                var parameter = command.CreateParameter();
                parameter.ParameterName = "@uploadId";
                parameter.Value = uploadId;
                parameter.DbType = DbType.Int32;
                command.Parameters.Add(parameter);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var proofUrl = reader.IsDBNull(0) ? null : Convert.ToString(reader.GetValue(0));
                    var localFilePath = ResolveWorkspaceImagePath(proofUrl);
                    if (!string.IsNullOrWhiteSpace(localFilePath))
                    {
                        return new LeaderboardImageResult(null, GetContentTypeFromPath(localFilePath), null, localFilePath);
                    }
                }
            }
            catch (SqlException)
            {
                return null;
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }

            return null;
        }

        private async Task<LeaderboardImageResult?> TryFindAnyExistingMemberUploadProofAsync(CancellationToken cancellationToken)
        {
            var connection = _db.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;

            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = @"
SELECT TOP (50) ProofUrl
FROM dbo.MemberUploads
WHERE NULLIF(LTRIM(RTRIM(ProofUrl)), '') IS NOT NULL
ORDER BY CreatedAt DESC, UploadId DESC;";
                command.CommandType = CommandType.Text;

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    var proofUrl = reader.IsDBNull(0) ? null : Convert.ToString(reader.GetValue(0));
                    var localFilePath = ResolveWorkspaceImagePath(proofUrl);
                    if (!string.IsNullOrWhiteSpace(localFilePath))
                    {
                        return new LeaderboardImageResult(null, GetContentTypeFromPath(localFilePath), null, localFilePath);
                    }
                }
            }
            catch (SqlException)
            {
                return null;
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }

            return null;
        }

        private static Dictionary<string, int> BuildOrdinalMap(IDataReader reader)
        {
            var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
            {
                map[Normalize(reader.GetName(i))] = i;
            }

            return map;
        }

        private static string Normalize(string value)
        {
            return value.Replace("_", string.Empty).Replace(" ", string.Empty).ToLowerInvariant();
        }

        private static int? FindOrdinal(Dictionary<string, int> ordinalMap, params string[] aliases)
        {
            foreach (var alias in aliases)
            {
                if (ordinalMap.TryGetValue(Normalize(alias), out var ordinal))
                {
                    return ordinal;
                }
            }

            return null;
        }

        private static string GetString(IDataRecord record, Dictionary<string, int> ordinalMap, params string[] aliases)
        {
            var ordinal = FindOrdinal(ordinalMap, aliases);
            if (ordinal == null || record.IsDBNull(ordinal.Value))
            {
                return string.Empty;
            }

            return Convert.ToString(record.GetValue(ordinal.Value)) ?? string.Empty;
        }

        private static int GetInt(IDataRecord record, Dictionary<string, int> ordinalMap, params string[] aliases)
        {
            var ordinal = FindOrdinal(ordinalMap, aliases);
            if (ordinal == null || record.IsDBNull(ordinal.Value))
            {
                return 0;
            }

            var value = record.GetValue(ordinal.Value);
            return value switch
            {
                int i => i,
                long l => (int)l,
                short s => s,
                byte b => b,
                decimal d => (int)d,
                double db => (int)db,
                float f => (int)f,
                _ when int.TryParse(Convert.ToString(value), out var parsed) => parsed,
                _ => 0
            };
        }

        private static decimal GetDecimal(IDataRecord record, Dictionary<string, int> ordinalMap, params string[] aliases)
        {
            var ordinal = FindOrdinal(ordinalMap, aliases);
            if (ordinal == null || record.IsDBNull(ordinal.Value))
            {
                return 0m;
            }

            var value = record.GetValue(ordinal.Value);
            return value switch
            {
                decimal d => d,
                double db => (decimal)db,
                float f => (decimal)f,
                int i => i,
                long l => l,
                _ when decimal.TryParse(Convert.ToString(value), out var parsed) => parsed,
                _ => 0m
            };
        }

        private static bool GetBool(IDataRecord record, Dictionary<string, int> ordinalMap, bool defaultValue = true, params string[] aliases)
        {
            var ordinal = FindOrdinal(ordinalMap, aliases);
            if (ordinal == null || record.IsDBNull(ordinal.Value))
            {
                return defaultValue;
            }

            var value = record.GetValue(ordinal.Value);
            return value switch
            {
                bool b => b,
                int i => i != 0,
                long l => l != 0,
                short s => s != 0,
                byte bt => bt != 0,
                string str when bool.TryParse(str, out var parsedBool) => parsedBool,
                string str when int.TryParse(str, out var parsedInt) => parsedInt != 0,
                _ => defaultValue
            };
        }

        private static DateTime GetDateTime(IDataRecord record, Dictionary<string, int> ordinalMap, params string[] aliases)
        {
            var ordinal = FindOrdinal(ordinalMap, aliases);
            if (ordinal == null || record.IsDBNull(ordinal.Value))
            {
                return DateTime.UtcNow;
            }

            var value = record.GetValue(ordinal.Value);
            return value switch
            {
                DateTime dt => dt,
                _ when DateTime.TryParse(Convert.ToString(value), out var parsed) => parsed,
                _ => DateTime.UtcNow
            };
        }

        private static LeaderboardPageViewModel BuildPage(List<LeaderboardEntryViewModel> entries)
        {
            return new LeaderboardPageViewModel
            {
                SeasonLabel = $"Season {DateTime.UtcNow.Year}",
                Subtitle = "National Rankings",
                Entries = entries
            };
        }

        private static string FormatDuration(int durationSeconds)
        {
            var duration = TimeSpan.FromSeconds(Math.Max(durationSeconds, 0));
            return duration.TotalHours >= 1
                ? $"{(int)duration.TotalHours}:{duration.Minutes:00}:{duration.Seconds:00}"
                : $"{duration.Minutes:00}:{duration.Seconds:00}";
        }

        private static string FormatPace(decimal distanceKm, int durationSeconds)
        {
            if (distanceKm <= 0 || durationSeconds <= 0)
            {
                return "--/KM";
            }

            var secondsPerKm = durationSeconds / (double)distanceKm;
            var pace = TimeSpan.FromSeconds(secondsPerKm);
            return $"{Math.Max((int)pace.TotalMinutes, 0)}:{pace.Seconds:00}/KM";
        }

        private static string NormalizeImageUrl(string? rawUrl, string fallbackUrl)
        {
            if (string.IsNullOrWhiteSpace(rawUrl))
            {
                return fallbackUrl;
            }

            var url = rawUrl.Trim().Replace('\\', '/');

            if (Uri.TryCreate(url, UriKind.Absolute, out var absoluteUri) &&
                (absoluteUri.Scheme == Uri.UriSchemeHttp || absoluteUri.Scheme == Uri.UriSchemeHttps))
            {
                return absoluteUri.ToString();
            }

            if (url.StartsWith("~/", StringComparison.Ordinal))
            {
                return "/" + url[2..];
            }

            if (url.StartsWith("/", StringComparison.Ordinal))
            {
                return url;
            }

            var wwwrootIndex = url.IndexOf("/wwwroot/", StringComparison.OrdinalIgnoreCase);
            if (wwwrootIndex >= 0)
            {
                return url[(wwwrootIndex + "/wwwroot".Length)..];
            }

            var uploadsIndex = url.IndexOf("/uploads/", StringComparison.OrdinalIgnoreCase);
            if (uploadsIndex >= 0)
            {
                return url[uploadsIndex..];
            }

            if (url.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase))
            {
                return "/" + url;
            }

            var imagesIndex = url.IndexOf("/images/", StringComparison.OrdinalIgnoreCase);
            if (imagesIndex >= 0)
            {
                return url[imagesIndex..];
            }

            if (url.StartsWith("images/", StringComparison.OrdinalIgnoreCase))
            {
                return "/" + url;
            }

            if (HasImageFileName(url))
            {
                return "/uploads/" + Path.GetFileName(url);
            }

            return fallbackUrl;
        }

        private static bool IsLocalUploadUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
            {
                return false;
            }

            var normalized = url.Trim().Replace('\\', '/');
            return normalized.StartsWith("/uploads/", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("~/uploads/", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("uploads/", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<LeaderboardImageResult?> TryReadImageRowAsync(string tableName, string keyColumn, int keyValue, CancellationToken cancellationToken)
        {
            var connection = _db.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;

            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = $"SELECT TOP (1) * FROM dbo.{tableName} WHERE {keyColumn} = @keyValue";
                command.CommandType = CommandType.Text;

                var parameter = command.CreateParameter();
                parameter.ParameterName = "@keyValue";
                parameter.Value = keyValue;
                parameter.DbType = DbType.Int32;
                command.Parameters.Add(parameter);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    return null;
                }

                var imageBytes = TryExtractImageBytes(reader, out var contentType);
                if (imageBytes == null || imageBytes.Length == 0)
                {
                    return null;
                }

                return new LeaderboardImageResult(imageBytes, contentType ?? DetectImageContentType(imageBytes), null);
            }
            catch (SqlException)
            {
                return null;
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }

        private async Task<string?> TryReadStringColumnAsync(string tableName, string columnName, string keyColumn, int keyValue, CancellationToken cancellationToken)
        {
            var connection = _db.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;

            if (shouldClose)
            {
                await connection.OpenAsync(cancellationToken);
            }

            try
            {
                await using var command = connection.CreateCommand();
                command.CommandText = $"SELECT TOP (1) {columnName} FROM dbo.{tableName} WHERE {keyColumn} = @keyValue";
                command.CommandType = CommandType.Text;

                var parameter = command.CreateParameter();
                parameter.ParameterName = "@keyValue";
                parameter.Value = keyValue;
                parameter.DbType = DbType.Int32;
                command.Parameters.Add(parameter);

                var result = await command.ExecuteScalarAsync(cancellationToken);
                return result == null || result == DBNull.Value ? null : Convert.ToString(result);
            }
            catch (SqlException)
            {
                return null;
            }
            finally
            {
                if (shouldClose)
                {
                    await connection.CloseAsync();
                }
            }
        }

        private static byte[]? TryExtractImageBytes(IDataRecord record, out string? contentType)
        {
            contentType = null;
            byte[]? bestMatch = null;
            var bestScore = int.MinValue;

            for (var i = 0; i < record.FieldCount; i++)
            {
                if (record.IsDBNull(i))
                {
                    continue;
                }

                var value = record.GetValue(i);
                if (value is not byte[] bytes || bytes.Length < 32)
                {
                    continue;
                }

                var fieldName = record.GetName(i);
                var score = bytes.Length;
                if (LooksLikeImageColumn(fieldName))
                {
                    score += 1_000_000;
                }

                if (score <= bestScore)
                {
                    continue;
                }

                bestScore = score;
                bestMatch = bytes;
            }

            if (bestMatch == null)
            {
                return null;
            }

            contentType = TryExtractContentType(record) ?? DetectImageContentType(bestMatch);
            return bestMatch;
        }

        private static bool LooksLikeImageColumn(string fieldName)
        {
            var name = Normalize(fieldName);
            return name.Contains("image", StringComparison.Ordinal)
                || name.Contains("photo", StringComparison.Ordinal)
                || name.Contains("proof", StringComparison.Ordinal)
                || name.Contains("cover", StringComparison.Ordinal)
                || name.Contains("banner", StringComparison.Ordinal)
                || name.Contains("avatar", StringComparison.Ordinal)
                || name.Contains("picture", StringComparison.Ordinal);
        }

        private static string? TryExtractContentType(IDataRecord record)
        {
            for (var i = 0; i < record.FieldCount; i++)
            {
                if (record.IsDBNull(i))
                {
                    continue;
                }

                if (record.GetValue(i) is not string value || string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                var fieldName = Normalize(record.GetName(i));
                if (fieldName.Contains("contenttype", StringComparison.Ordinal)
                    || fieldName.Contains("mimetype", StringComparison.Ordinal)
                    || fieldName.Contains("mime", StringComparison.Ordinal))
                {
                    return value;
                }
            }

            return null;
        }

        private static string DetectImageContentType(byte[] bytes)
        {
            if (bytes.Length >= 8 &&
                bytes[0] == 0x89 && bytes[1] == 0x50 && bytes[2] == 0x4E && bytes[3] == 0x47)
            {
                return "image/png";
            }

            if (bytes.Length >= 3 &&
                bytes[0] == 0xFF && bytes[1] == 0xD8 && bytes[2] == 0xFF)
            {
                return "image/jpeg";
            }

            if (bytes.Length >= 6 &&
                bytes[0] == 0x47 && bytes[1] == 0x49 && bytes[2] == 0x46)
            {
                return "image/gif";
            }

            if (bytes.Length >= 12 &&
                bytes[0] == 0x52 && bytes[1] == 0x49 && bytes[2] == 0x46 && bytes[3] == 0x46 &&
                bytes[8] == 0x57 && bytes[9] == 0x45 && bytes[10] == 0x42 && bytes[11] == 0x50)
            {
                return "image/webp";
            }

            if (bytes.Length >= 12 &&
                bytes[4] == 0x66 && bytes[5] == 0x74 && bytes[6] == 0x79 && bytes[7] == 0x70)
            {
                return "image/avif";
            }

            if (bytes.Length >= 2 &&
                bytes[0] == 0x42 && bytes[1] == 0x4D)
            {
                return "image/bmp";
            }

            return "application/octet-stream";
        }

        private string? ResolveWorkspaceImagePath(string? rawPath)
        {
            if (string.IsNullOrWhiteSpace(rawPath))
            {
                return null;
            }

            var normalized = rawPath.Trim().Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var currentAppCandidate = Path.Combine(_environment.WebRootPath ?? string.Empty, normalized.TrimStart(Path.DirectorySeparatorChar));
            if (File.Exists(currentAppCandidate))
            {
                return currentAppCandidate;
            }

            var fileName = Path.GetFileName(normalized);
            if (string.IsNullOrWhiteSpace(fileName))
            {
                return null;
            }

            foreach (var searchRoot in GetSearchRoots())
            {
                if (string.IsNullOrWhiteSpace(searchRoot) || !Directory.Exists(searchRoot))
                {
                    continue;
                }

                try
                {
                    foreach (var file in Directory.EnumerateFiles(searchRoot, fileName, SearchOption.AllDirectories))
                    {
                        var candidate = file.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                        if (IsWorkspaceUploadCandidate(candidate))
                        {
                            return candidate;
                        }
                    }
                }
                catch (IOException)
                {
                    continue;
                }
                catch (UnauthorizedAccessException)
                {
                    continue;
                }
            }

            return null;
        }

        private IEnumerable<string> GetSearchRoots()
        {
            var currentRoot = _environment.ContentRootPath;
            if (!string.IsNullOrWhiteSpace(currentRoot))
            {
                yield return currentRoot;
            }

            var parentRoot = Directory.GetParent(_environment.ContentRootPath)?.FullName;
            if (!string.IsNullOrWhiteSpace(parentRoot))
            {
                yield return parentRoot;
                yield return Path.Combine(parentRoot, "Tracker", "NextHorizon-AndradeBranch", "NextHorizon");
            }
        }

        private static string GetContentTypeFromPath(string path)
        {
            var extension = Path.GetExtension(path);
            return extension.ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".jpg" => "image/jpeg",
                ".jpeg" => "image/jpeg",
                ".gif" => "image/gif",
                ".webp" => "image/webp",
                ".avif" => "image/avif",
                ".bmp" => "image/bmp",
                _ => "application/octet-stream"
            };
        }

        private static bool IsWorkspaceUploadCandidate(string path)
        {
            var uploadsRoot = $"{Path.DirectorySeparatorChar}wwwroot{Path.DirectorySeparatorChar}uploads{Path.DirectorySeparatorChar}";
            if (!path.Contains(uploadsRoot, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            var normalized = path.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
            var activityUploads = $"{uploadsRoot}activity{Path.DirectorySeparatorChar}";
            var proofUploads = $"{uploadsRoot}proofs{Path.DirectorySeparatorChar}";
            return normalized.Contains(activityUploads, StringComparison.OrdinalIgnoreCase)
                || normalized.Contains(proofUploads, StringComparison.OrdinalIgnoreCase);
        }

        private static bool HasImageFileName(string path)
        {
            var extension = Path.GetExtension(path);
            return extension.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".png", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".webp", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".gif", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
                || extension.Equals(".avif", StringComparison.OrdinalIgnoreCase);
        }

        private static string FormatRankChange(int rankChange)
        {
            if (rankChange > 0)
            {
                return $"up {rankChange}";
            }

            if (rankChange < 0)
            {
                return $"down {Math.Abs(rankChange)}";
            }

            return "same 0";
        }

        private static string GetRankChangeCss(int rankChange)
        {
            if (rankChange > 0)
            {
                return "up";
            }

            if (rankChange < 0)
            {
                return "down";
            }

            return "same";
        }

        private sealed class LeaderboardRow
        {
            public int UploadId { get; set; }
            public int UserId { get; set; }
            public string AthleteName { get; set; } = string.Empty;
            public string AvatarUrl { get; set; } = string.Empty;
            public string CoverImageUrl { get; set; } = string.Empty;
            public string CategoryLabel { get; set; } = string.Empty;
            public string Scope { get; set; } = string.Empty;
            public decimal DistanceKm { get; set; }
            public int DurationSeconds { get; set; }
            public int RankChange { get; set; }
            public bool IsVerified { get; set; } = true;
            public bool IsActive { get; set; } = true;
            public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
        }

        public sealed class LeaderboardImageResult
        {
            public LeaderboardImageResult(byte[]? bytes, string? contentType, string? redirectUrl, string? localFilePath = null)
            {
                Bytes = bytes;
                ContentType = contentType;
                RedirectUrl = redirectUrl;
                LocalFilePath = localFilePath;
            }

            public byte[]? Bytes { get; }
            public string? ContentType { get; }
            public string? RedirectUrl { get; }
            public string? LocalFilePath { get; }
        }
    }
}
