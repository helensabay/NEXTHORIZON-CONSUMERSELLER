using Microsoft.Data.SqlClient;
using NextHorizon.Models;

namespace NextHorizon.Services;

public sealed class SellerContextService : ISellerContextService
{
    private static readonly string[] IdCandidates = ["seller_id", "SellerId", "Id"];
    private static readonly string[] NameCandidates = ["business_name", "DisplayName", "FullName", "Name", "SellerName"];
    private static readonly string[] IdentityCandidates = ["business_email", "Email", "UserName", "Username", "LoginName", "user_id"];

    private readonly string _connectionString;
    private readonly ILogger<SellerContextService> _logger;

    public SellerContextService(IConfiguration configuration, ILogger<SellerContextService> logger)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection") ?? string.Empty;
        _logger = logger;
    }

    public async Task<SellerContextInfo> ResolveSellerByIdAsync(int sellerId, CancellationToken cancellationToken = default)
    {
        var fallback = new SellerContextInfo { SellerId = 0, SellerName = "Seller" };
        if (sellerId <= 0 || string.IsNullOrWhiteSpace(_connectionString))
        {
            return fallback;
        }

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand("SELECT TOP (1) * FROM dbo.Sellers WHERE seller_id = @SellerId", connection);
            command.Parameters.AddWithValue("@SellerId", sellerId);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return fallback;
            }

            var ordinals = BuildOrdinalMap(reader);
            var idOrdinal = FindOrdinal(ordinals, IdCandidates);
            if (idOrdinal is null)
            {
                return fallback;
            }

            var nameOrdinal = FindOrdinal(ordinals, NameCandidates);
            return ReadSeller(reader, idOrdinal.Value, nameOrdinal, fallback.SellerName) ?? fallback;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve seller {SellerId} from dbo.Sellers. Falling back to default seller context.", sellerId);
            return fallback;
        }
    }

    public async Task<SellerContextInfo> ResolveSellerAsync(string? userIdentity, CancellationToken cancellationToken = default)
    {
        var fallback = new SellerContextInfo { SellerId = 0, SellerName = "Seller" };
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return fallback;
        }

        try
        {
            await using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync(cancellationToken);

            await using var command = new SqlCommand("SELECT TOP (200) * FROM dbo.Sellers", connection);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);

            var ordinals = BuildOrdinalMap(reader);
            var idOrdinal = FindOrdinal(ordinals, IdCandidates);
            if (idOrdinal is null)
            {
                return fallback;
            }

            var nameOrdinal = FindOrdinal(ordinals, NameCandidates);
            var identityOrdinal = FindOrdinal(ordinals, IdentityCandidates);
            var normalizedIdentity = Normalize(userIdentity);

            SellerContextInfo? firstSeller = null;

            while (await reader.ReadAsync(cancellationToken))
            {
                var seller = ReadSeller(reader, idOrdinal.Value, nameOrdinal, fallback.SellerName);
                if (seller is null)
                {
                    continue;
                }

                firstSeller ??= seller;

                if (normalizedIdentity is null || identityOrdinal is null)
                {
                    continue;
                }

                var rowIdentity = Normalize(Convert.ToString(reader[identityOrdinal.Value]));
                if (!string.IsNullOrWhiteSpace(rowIdentity) && string.Equals(rowIdentity, normalizedIdentity, StringComparison.OrdinalIgnoreCase))
                {
                    return seller;
                }
            }

            return firstSeller ?? fallback;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to resolve seller from dbo.Sellers. Falling back to default seller context.");
            return fallback;
        }
    }

    private static SellerContextInfo? ReadSeller(SqlDataReader reader, int idOrdinal, int? nameOrdinal, string fallbackName)
    {
        var idRaw = reader[idOrdinal];
        if (idRaw is DBNull)
        {
            return null;
        }

        if (!int.TryParse(Convert.ToString(idRaw), out var sellerId) || sellerId <= 0)
        {
            return null;
        }

        var sellerName = fallbackName;
        if (nameOrdinal is not null && reader[nameOrdinal.Value] is not DBNull)
        {
            var candidate = Convert.ToString(reader[nameOrdinal.Value]);
            if (!string.IsNullOrWhiteSpace(candidate))
            {
                sellerName = candidate;
            }
        }

        return new SellerContextInfo
        {
            SellerId = sellerId,
            SellerName = sellerName
        };
    }

    private static Dictionary<string, int> BuildOrdinalMap(SqlDataReader reader)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < reader.FieldCount; i++)
        {
            map[reader.GetName(i)] = i;
        }

        return map;
    }

    private static int? FindOrdinal(IReadOnlyDictionary<string, int> ordinals, IEnumerable<string> candidates)
    {
        foreach (var candidate in candidates)
        {
            if (ordinals.TryGetValue(candidate, out var ordinal))
            {
                return ordinal;
            }
        }

        return null;
    }

    private static string? Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
