
using System.Security.Claims;

namespace NextHorizon.Security;
public interface IAuthenticatedUserContextService
    {
        Task<AuthenticatedUserContext?> GetByUserIdAsync(int userId, CancellationToken cancellationToken = default);
        Task<AuthenticatedUserContext> GetCurrentAsync(ClaimsPrincipal user, CancellationToken cancellationToken = default);
    }
