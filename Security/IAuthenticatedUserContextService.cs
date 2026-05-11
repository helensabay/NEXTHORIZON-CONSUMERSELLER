using System.Security.Claims;

namespace MyAspNetApp.Security;

public interface IAuthenticatedUserContextService
{
    Task<AuthenticatedUserContext?> GetCurrentAsync(ClaimsPrincipal principal, CancellationToken cancellationToken);

    Task<AuthenticatedUserContext?> GetCurrentAsync(CancellationToken cancellationToken);

    Task<AuthenticatedUserContext?> GetByUserIdAsync(int userId, CancellationToken cancellationToken);
}
