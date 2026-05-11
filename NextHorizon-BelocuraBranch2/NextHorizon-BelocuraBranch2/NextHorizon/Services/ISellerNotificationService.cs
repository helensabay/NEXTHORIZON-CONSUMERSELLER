using NextHorizon.Models;

namespace NextHorizon.Services;

public interface ISellerNotificationService
{
    Task<SellerNotification> CreateAsync(SellerNotification notification, CancellationToken cancellationToken = default);
    Task<SellerNotification> CreateIfMissingAsync(SellerNotification notification, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SellerNotification>> ListAsync(int sellerId, int take = 20, bool unreadOnly = false, CancellationToken cancellationToken = default);
    Task<int> GetUnreadCountAsync(int sellerId, CancellationToken cancellationToken = default);
    Task<bool> MarkReadAsync(int sellerId, int notificationId, CancellationToken cancellationToken = default);
    Task<int> MarkAllReadAsync(int sellerId, CancellationToken cancellationToken = default);
}
