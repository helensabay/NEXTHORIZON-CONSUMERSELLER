using NextHorizon.Models;

namespace NextHorizon.Services;

public interface ISellerContextService
{
    Task<SellerContextInfo> ResolveSellerByIdAsync(int sellerId, CancellationToken cancellationToken = default);
    Task<SellerContextInfo> ResolveSellerAsync(string? userIdentity, CancellationToken cancellationToken = default);
}
