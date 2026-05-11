using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using MyAspNetApp.Data;

namespace MyAspNetApp.Security;

public sealed class AuthenticatedUserContextService : IAuthenticatedUserContextService
{
    private readonly AppDbContext _dbContext;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public AuthenticatedUserContextService(AppDbContext dbContext, IHttpContextAccessor httpContextAccessor)
    {
        _dbContext = dbContext;
        _httpContextAccessor = httpContextAccessor;
    }

    public Task<AuthenticatedUserContext?> GetCurrentAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        var userIdClaim = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (int.TryParse(userIdClaim, out var claimedUserId) && claimedUserId > 0)
        {
            return GetByUserIdAsync(claimedUserId, cancellationToken);
        }

        return GetCurrentAsync(cancellationToken);
    }

    public Task<AuthenticatedUserContext?> GetCurrentAsync(CancellationToken cancellationToken)
    {
        var httpContext = _httpContextAccessor.HttpContext;
        var userId = httpContext?.Session.GetInt32("UserId");
        return !userId.HasValue
            ? Task.FromResult<AuthenticatedUserContext?>(null)
            : GetByUserIdAsync(userId.Value, cancellationToken);
    }

    public async Task<AuthenticatedUserContext?> GetByUserIdAsync(int userId, CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            return null;
        }

        var user = await _dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);
        if (user is null)
        {
            return null;
        }

        var consumer = await _dbContext.Consumers
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.UserId == userId, cancellationToken);

        var sessionSellerId = _httpContextAccessor.HttpContext?.Session.GetInt32("SellerId");
        var sellerId = sessionSellerId.HasValue && sessionSellerId.Value > 0
            ? sessionSellerId.Value
            : await _dbContext.Sellers
                .AsNoTracking()
                .Where(x => x.UserId == userId)
                .Select(x => (int?)x.SellerId)
                .FirstOrDefaultAsync(cancellationToken);

        var sellerExists = sellerId.HasValue && await _dbContext.Products
            .AsNoTracking()
            .AnyAsync(x => x.SellerId == sellerId.Value, cancellationToken);

        return new AuthenticatedUserContext(
            userId,
            consumer?.ConsumerId,
            consumer?.UserId,
            sellerExists || sellerId.HasValue ? sellerId : null,
            sellerExists || sellerId.HasValue ? userId : null);
    }
}
