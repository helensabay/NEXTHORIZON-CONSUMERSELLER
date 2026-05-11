using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NextHorizon.Data;
using NextHorizon.Models;
using NextHorizon.Security;
using NextHorizon.Services;

namespace NextHorizon.Controllers;

[ApiController]
[Authorize]
[Route("api/seller/notifications")]
public sealed class SellerNotificationsController : ControllerBase
{
    private readonly IAuthenticatedUserContextService _authenticatedUserContextService;
    private readonly ISellerNotificationService _sellerNotificationService;
    private readonly AppDbContext _context;
    private readonly IOrderService _orderService;

    public SellerNotificationsController(
        IAuthenticatedUserContextService authenticatedUserContextService,
        ISellerNotificationService sellerNotificationService,
        AppDbContext context,
        IOrderService orderService)
    {
        _authenticatedUserContextService = authenticatedUserContextService;
        _sellerNotificationService = sellerNotificationService;
        _context = context;
        _orderService = orderService;
    }

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] int take = 20, [FromQuery] bool unreadOnly = false, CancellationToken cancellationToken = default)
    {
        var currentUser = await _authenticatedUserContextService.GetCurrentAsync(User, cancellationToken);
        if (currentUser.SellerId is not int sellerId || sellerId <= 0)
        {
            return Forbid();
        }

        await SyncComputedNotificationsAsync(sellerId, cancellationToken);
        var notifications = await _sellerNotificationService.ListAsync(sellerId, take, unreadOnly, cancellationToken);
        return Ok(notifications);
    }

    [HttpGet("unread-count")]
    public async Task<IActionResult> GetUnreadCount(CancellationToken cancellationToken = default)
    {
        var currentUser = await _authenticatedUserContextService.GetCurrentAsync(User, cancellationToken);
        if (currentUser.SellerId is not int sellerId || sellerId <= 0)
        {
            return Forbid();
        }

        await SyncComputedNotificationsAsync(sellerId, cancellationToken);
        var count = await _sellerNotificationService.GetUnreadCountAsync(sellerId, cancellationToken);
        return Ok(new { unreadCount = count });
    }

    [HttpPost("{notificationId:int}/read")]
    public async Task<IActionResult> MarkRead(int notificationId, CancellationToken cancellationToken = default)
    {
        var currentUser = await _authenticatedUserContextService.GetCurrentAsync(User, cancellationToken);
        if (currentUser.SellerId is not int sellerId || sellerId <= 0)
        {
            return Forbid();
        }

        var updated = await _sellerNotificationService.MarkReadAsync(sellerId, notificationId, cancellationToken);
        return updated ? Ok(new { success = true }) : NotFound(new { success = false });
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead(CancellationToken cancellationToken = default)
    {
        var currentUser = await _authenticatedUserContextService.GetCurrentAsync(User, cancellationToken);
        if (currentUser.SellerId is not int sellerId || sellerId <= 0)
        {
            return Forbid();
        }

        var affected = await _sellerNotificationService.MarkAllReadAsync(sellerId, cancellationToken);
        return Ok(new { success = true, affected });
    }

    private async Task SyncComputedNotificationsAsync(int sellerId, CancellationToken cancellationToken)
    {
        var notifications = await BuildComputedNotificationsAsync(sellerId, cancellationToken);
        foreach (var notification in notifications)
        {
            await _sellerNotificationService.CreateIfMissingAsync(notification, cancellationToken);
        }
    }

    private async Task<List<SellerNotification>> BuildComputedNotificationsAsync(int sellerId, CancellationToken cancellationToken)
    {
        var notifications = new List<SellerNotification>();
        if (sellerId <= 0)
        {
            return notifications;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var now = DateTime.UtcNow;

        void AddNotification(
            string category,
            string title,
            string message,
            DateTime createdAt,
            int? orderId = null)
        {
            var key = string.Join("|", category, orderId?.ToString() ?? "none", message, createdAt.ToString("O"));
            if (!seen.Add(key))
            {
                return;
            }

            notifications.Add(new SellerNotification
            {
                RecipientType = "seller",
                RecipientId = sellerId.ToString(),
                OrderId = orderId,
                Category = category,
                Title = title,
                Message = message,
                IsRead = false,
                CreatedAt = createdAt
            });
        }

        var recentOrders = await _context.Orders
            .AsNoTracking()
            .Where(order => order.seller_id == sellerId)
            .OrderByDescending(order => order.OrderDate)
            .Take(20)
            .ToListAsync(cancellationToken);

        await _orderService.ApplySellerFacingStatusesAsync(sellerId, recentOrders, cancellationToken);

        foreach (var order in recentOrders)
        {
            var effectiveStatus = string.IsNullOrWhiteSpace(order.EffectiveStatus)
                ? order.Status ?? string.Empty
                : order.EffectiveStatus;

            if (effectiveStatus.Equals("Pending", StringComparison.OrdinalIgnoreCase))
            {
                AddNotification(
                    "order",
                    "Order Needs Your Action",
                    $"Order #{order.OrderID} is waiting for your confirmation or fulfillment update.",
                    order.OrderDate,
                    order.OrderID);
                continue;
            }

            if (order.OrderDate >= now.AddDays(-1))
            {
                AddNotification(
                    "order",
                    "New Order Received",
                    $"You received a new order #{order.OrderID}. Review and confirm fulfillment details.",
                    order.OrderDate,
                    order.OrderID);
            }

            if (effectiveStatus.Equals("Accepted", StringComparison.OrdinalIgnoreCase)
                || effectiveStatus.Equals("Processing", StringComparison.OrdinalIgnoreCase))
            {
                AddNotification(
                    "order",
                    "Order Ready to Ship",
                    $"Order #{order.OrderID} is ready for shipment. Print the label and dispatch it.",
                    order.OrderDate,
                    order.OrderID);
            }

            if (effectiveStatus.Equals("Cancelled", StringComparison.OrdinalIgnoreCase))
            {
                var cancelledByBuyer = !string.IsNullOrWhiteSpace(order.CancellationReason);
                AddNotification(
                    "cancellation",
                    cancelledByBuyer ? "Order Canceled by Buyer" : "Order Canceled by Platform",
                    cancelledByBuyer
                        ? $"Buyer canceled order #{order.OrderID}."
                        : $"Order #{order.OrderID} was canceled by the platform or an administrator.",
                    order.OrderDate,
                    order.OrderID);
            }

            if (effectiveStatus.Equals("Failed Delivery", StringComparison.OrdinalIgnoreCase))
            {
                AddNotification(
                    "order",
                    "Order Processing Issue",
                    $"Order #{order.OrderID} could not be completed because delivery failed. Review the issue and take action.",
                    order.OrderDate,
                    order.OrderID);
            }

            if (effectiveStatus.Equals("Shipped", StringComparison.OrdinalIgnoreCase)
                || effectiveStatus.Equals("Delivered", StringComparison.OrdinalIgnoreCase))
            {
                AddNotification(
                    "order",
                    "Order Status Updated",
                    $"Order #{order.OrderID} moved to {effectiveStatus}.",
                    order.DateShipped ?? order.OrderDate,
                    order.OrderID);
            }
        }

        var returnRequests = await _context.ReturnRequests
            .AsNoTracking()
            .Where(request => request.SellerId == sellerId)
            .OrderByDescending(request => request.UpdatedAt)
            .Take(12)
            .ToListAsync(cancellationToken);

        foreach (var request in returnRequests)
        {
            if (request.Status.Equals("Return Requested", StringComparison.OrdinalIgnoreCase))
            {
                AddNotification(
                    "return",
                    "Return Request Submitted",
                    $"A return request was submitted for order #{request.OrderId}.",
                    request.UpdatedAt,
                    request.OrderId);
            }

            if (request.Status.Equals("Return Approved", StringComparison.OrdinalIgnoreCase)
                || request.Status.Equals("Return Rejected", StringComparison.OrdinalIgnoreCase))
            {
                AddNotification(
                    "return",
                    "Return Decision Updated",
                    $"Return for order #{request.OrderId} has been {request.Status.Replace("Return ", string.Empty).ToLowerInvariant()}.",
                    request.UpdatedAt,
                    request.OrderId);
            }

            if (request.Status.Equals("Item Returned", StringComparison.OrdinalIgnoreCase))
            {
                AddNotification(
                    "return",
                    "Return Completed",
                    $"Return workflow for order #{request.OrderId} has been completed.",
                    request.UpdatedAt,
                    request.OrderId);
            }

            if (request.Status.Equals("Refunded", StringComparison.OrdinalIgnoreCase))
            {
                AddNotification(
                    "refund",
                    "Refund Completed",
                    $"Refund for order #{request.OrderId} has been completed successfully.",
                    request.UpdatedAt,
                    request.OrderId);
            }
        }

        var lowStockProducts = await _context.ProductVariants
            .AsNoTracking()
            .Where(variant => variant.Product != null && variant.Product.SellerId == sellerId)
            .GroupBy(variant => new
            {
                variant.ProductId,
                ProductName = variant.Product != null ? variant.Product.ProductName : string.Empty
            })
            .Select(group => new
            {
                group.Key.ProductId,
                group.Key.ProductName,
                Stock = group.Sum(item => item.Quantity)
            })
            .Where(item => item.Stock <= 5)
            .OrderBy(item => item.Stock)
            .Take(5)
            .ToListAsync(cancellationToken);

        foreach (var product in lowStockProducts)
        {
            var isOutOfStock = product.Stock <= 0;
            AddNotification(
                "inventory",
                isOutOfStock ? "Out of Stock" : "Low Stock Alert",
                isOutOfStock
                    ? $"Product {product.ProductName} is now out of stock."
                    : $"Product {product.ProductName} is running low on stock with {product.Stock} units left.",
                now);
        }

        return notifications;
    }
}
