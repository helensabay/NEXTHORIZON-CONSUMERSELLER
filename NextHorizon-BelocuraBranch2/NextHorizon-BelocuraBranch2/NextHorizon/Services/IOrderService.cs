using NextHorizon.Models;
using System.Threading;

namespace NextHorizon.Services
{
    public interface IOrderService
    {
        Task UpdateOrderAsync(Order order);
        Task<List<Order>> GetOrdersBySellerAsync(int sellerId, DateTime? startDate = null, DateTime? endDate = null);
        Task<AcceptOrderResult> AcceptOrderAsync(int orderId, int sellerId, int logisticsId);
        Task<List<Logistics>> GetCouriersAsync();
        Task<bool> DeclineOrderAsync(int orderId, int sellerId, string reason);
        Task<Order?> GetOrderByIdAsync(int orderId, int sellerId);
        Task ApplySellerFacingStatusesAsync(int sellerId, IList<Order> orders, CancellationToken cancellationToken = default);
        Task<int> CountOrdersBySellerFacingStatusAsync(int sellerId, string status, CancellationToken cancellationToken = default);
    }
}
