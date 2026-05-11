using Microsoft.EntityFrameworkCore;
using NextHorizon.Data;
using NextHorizon.Security;
using System.Security.Claims;

namespace NextHorizon.Services
{
    public class AuthenticatedUserContextService : IAuthenticatedUserContextService
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly AppDbContext _dbContext;

        public AuthenticatedUserContextService(
            IHttpContextAccessor httpContextAccessor,
            AppDbContext dbContext)
        {
            _httpContextAccessor = httpContextAccessor;
            _dbContext = dbContext;
        }

        public async Task<AuthenticatedUserContext?> GetByUserIdAsync(int userId, CancellationToken cancellationToken)
        {
            var dbUser = await _dbContext.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId, cancellationToken);

            if (dbUser == null)
            {
                return null;
            }

            int? sellerId = null;
            int? sellerAccountUserId = null;
            int? consumerId = null;
            int? consumerAccountUserId = null;

            // If user is a seller, get the seller account
            if (dbUser.UserType?.Equals("Seller", StringComparison.OrdinalIgnoreCase) == true)
            {
                var sellerAccount = await _dbContext.SellerAccounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(s => s.UserId == dbUser.UserId, cancellationToken);

                if (sellerAccount != null)
                {
                    sellerId = sellerAccount.SellerId;
                    sellerAccountUserId = dbUser.UserId;
                }
            }

            // TODO: Add consumer logic here if needed
            // If user is a consumer, get the consumer account
            if (dbUser.UserType?.Equals("Consumer", StringComparison.OrdinalIgnoreCase) == true)
            {
                var consumer = await _dbContext.Consumers
                    .AsNoTracking()
                    .FirstOrDefaultAsync(c => c.UserId == dbUser.UserId, cancellationToken);

                if (consumer != null)
                {
                    consumerId = consumer.ConsumerId;
                    consumerAccountUserId = dbUser.UserId;
                }
            }

            return new AuthenticatedUserContext(
                UserId: dbUser.UserId,
                Email: dbUser.Email ?? string.Empty,
                UserType: dbUser.UserType ?? string.Empty,
                ConsumerId: consumerId,
                ConsumerAccountUserId: consumerAccountUserId,
                SellerId: sellerId,
                SellerAccountUserId: sellerAccountUserId
            );
        }

        public async Task<AuthenticatedUserContext> GetCurrentAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default)
        {
            var userIdClaim = user.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userIdClaim) || !int.TryParse(userIdClaim, out var userId))
            {
                return new AuthenticatedUserContext(
                    UserId: 0,
                    Email: string.Empty,
                    UserType: string.Empty,
                    ConsumerId: null,
                    ConsumerAccountUserId: null,
                    SellerId: null,
                    SellerAccountUserId: null
                );
            }

            var result = await GetByUserIdAsync(userId, cancellationToken);
            
            if (result == null)
            {
                return new AuthenticatedUserContext(
                    UserId: 0,
                    Email: string.Empty,
                    UserType: string.Empty,
                    ConsumerId: null,
                    ConsumerAccountUserId: null,
                    SellerId: null,
                    SellerAccountUserId: null
                );
            }

            return result;
        }
    }
}