using Microsoft.EntityFrameworkCore;
using NextHorizon.Data;
using NextHorizon.Models;
using System.Threading;


namespace NextHorizon.Services
{
    public class OrderService : IOrderService
    {
        private readonly AppDbContext _context;
        public OrderService(AppDbContext context)
        {
            _context = context;
        }
        public async Task<List<Logistics>> GetCouriersAsync()
        
{
    return await _context.Logistics.ToListAsync();
}
public async Task UpdateOrderAsync(Order order)
{
    _context.Orders.Update(order);
    await _context.SaveChangesAsync();
}
       public async Task<List<Order>> GetOrdersBySellerAsync(int sellerId, DateTime? startDate = null, DateTime? endDate = null)
{
    var query = _context.Orders
        .Include(o => o.OrderItems)
            .ThenInclude(item => item.Product)
        .Where(o => o.seller_id == sellerId)
        .AsNoTracking()
        .AsSplitQuery();

    if (startDate.HasValue)
    {
        var normalizedStartDate = startDate.Value.Date;
        query = query.Where(o => o.OrderDate >= normalizedStartDate);
    }

    if (endDate.HasValue)
    {
        var exclusiveEndDate = endDate.Value.Date.AddDays(1);
        query = query.Where(o => o.OrderDate < exclusiveEndDate);
    }

    var orders = await query
        .OrderByDescending(o => o.OrderDate)
        .ToListAsync();

    await PopulateOrdersWithVariantDataAsync(orders);
    await ApplySellerFacingStatusesAsync(sellerId, orders);
    return orders;
}
   public async Task<AcceptOrderResult> AcceptOrderAsync(int orderId, int sellerId, int courierId)
{
    await using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);

    var order = await _context.Orders
        .Include(o => o.OrderItems)
        .FirstOrDefaultAsync(o => o.OrderID == orderId && o.seller_id == sellerId);

    if (order == null)
    {
        return new AcceptOrderResult { Success = false, Message = "Order not found or unauthorized." };
    }

    if (!string.Equals(order.Status, "Pending", StringComparison.OrdinalIgnoreCase))
    {
        return new AcceptOrderResult { Success = false, Message = $"Only pending orders can be accepted. Current status: {order.Status ?? "Unknown"}." };
    }

    if (order.OrderItems == null || order.OrderItems.Count == 0)
    {
        return new AcceptOrderResult { Success = false, Message = "This order has no line items to reserve from inventory." };
    }

    foreach (var item in order.OrderItems)
    {
        var variant = await ResolveVariantForOrderItemAsync(item);
        if (variant == null)
        {
            await transaction.RollbackAsync();
            return new AcceptOrderResult
            {
                Success = false,
                Message = $"Inventory record not found for product {item.ProductID} ({item.Color ?? "Default"} / {item.Size ?? "Default"})."
            };
        }

        var affectedRows = await _context.Database.ExecuteSqlInterpolatedAsync($@"
UPDATE dbo.ProductVariants
SET Quantity = Quantity - {item.Quantity},
    Availability = CASE WHEN Quantity - {item.Quantity} > 0 THEN N'In Stock' ELSE N'Out of Stock' END
WHERE VariantId = {variant.Id} AND Quantity >= {item.Quantity}");

        if (affectedRows == 0)
        {
            await transaction.RollbackAsync();

            var latestQuantity = await _context.ProductVariants
                .Where(v => v.Id == variant.Id)
                .Select(v => (int?)v.Quantity)
                .FirstOrDefaultAsync();

            var availableQuantity = latestQuantity ?? 0;
            var variantLabel = string.Join(" / ", new[] { item.Color, item.Size }.Where(value => !string.IsNullOrWhiteSpace(value)));
            if (string.IsNullOrWhiteSpace(variantLabel))
            {
                variantLabel = "selected variant";
            }

            return new AcceptOrderResult
            {
                Success = false,
                Message = $"Insufficient stock for {variantLabel}. Requested {item.Quantity}, available {availableQuantity}."
            };
        }
    }

    order.Status = "To Ship";
    order.FulfillmentStatus = "To Ship";
    order.logistics_id = courierId;
    await _context.SaveChangesAsync();
    await transaction.CommitAsync();

    return new AcceptOrderResult
    {
        Success = true,
        Message = "Order accepted and inventory updated successfully."
    };
}

private async Task PopulateOrdersWithVariantDataAsync(IEnumerable<Order> orders)
{
    var orderList = orders?.ToList() ?? new List<Order>();
    if (orderList.Count == 0)
    {
        return;
    }

    var allItems = orderList
        .SelectMany(order => order.OrderItems ?? new List<OrderItem>())
        .ToList();

    if (allItems.Count == 0)
    {
        foreach (var order in orderList)
        {
            order.ProductImage = string.Empty;
        }
        return;
    }

    var variantMap = await BuildVariantLookupAsync(allItems);

    foreach (var item in allItems)
    {
        if (!TryResolveVariant(item, variantMap, out var variant) || variant == null)
        {
            continue;
        }

        item.Sku = variant.SKU;
        if (item.Product != null)
        {
            item.Product.ImagePath = BuildVariantImageUrl(variant);
        }
    }

    foreach (var order in orderList)
    {
        var primaryItem = order.OrderItems?
            .OrderBy(item => item.OrderItemID)
            .FirstOrDefault();

        if (primaryItem != null && TryResolveVariant(primaryItem, variantMap, out var variant) && variant != null)
        {
            order.ProductImage = BuildVariantImageUrl(variant);
        }
        else
        {
            order.ProductImage = string.Empty;
        }
    }
}

private sealed class VariantLookupData
{
    public Dictionary<int, DbProductVariant> ById { get; } = new();
    public Dictionary<string, DbProductVariant> ByCompositeKey { get; } = new(StringComparer.OrdinalIgnoreCase);
}

private async Task<VariantLookupData> BuildVariantLookupAsync(IEnumerable<OrderItem> items)
{
    var itemList = items.ToList();
    var lookup = new VariantLookupData();

    var variantIds = itemList
        .Where(item => item.VariantId.HasValue)
        .Select(item => item.VariantId!.Value)
        .Distinct()
        .ToList();

    if (variantIds.Count > 0)
    {
        var directVariants = await _context.ProductVariants
            .AsNoTracking()
            .Where(v => variantIds.Contains(v.Id))
            .ToListAsync();

        foreach (var variant in directVariants)
        {
            lookup.ById[variant.Id] = variant;
        }
    }

    var unresolvedProductIds = itemList
        .Where(item => !item.VariantId.HasValue || !lookup.ById.ContainsKey(item.VariantId.Value))
        .Select(item => item.ProductID)
        .Distinct()
        .ToList();

    if (unresolvedProductIds.Count > 0)
    {
        var fallbackVariants = await _context.ProductVariants
            .AsNoTracking()
            .Where(v => unresolvedProductIds.Contains(v.ProductId))
            .OrderBy(v => v.Id)
            .ToListAsync();

        foreach (var variant in fallbackVariants)
        {
            var key = BuildVariantLookupKey(variant.ProductId, variant.Size, variant.Style);
            if (!lookup.ByCompositeKey.ContainsKey(key))
            {
                lookup.ByCompositeKey[key] = variant;
            }
        }
    }

    return lookup;
}

private static string BuildVariantLookupKey(int productId, string? size, string? color)
{
    return string.Join('|', new[]
    {
        productId.ToString(),
        (size ?? string.Empty).Trim().ToLowerInvariant(),
        (color ?? string.Empty).Trim().ToLowerInvariant()
    });
}

private static bool TryResolveVariant(OrderItem item, VariantLookupData lookup, out DbProductVariant? variant)
{
    variant = null;

    if (item.VariantId.HasValue && lookup.ById.TryGetValue(item.VariantId.Value, out var directVariant))
    {
        variant = directVariant;
        return true;
    }

    var exactKey = BuildVariantLookupKey(item.ProductID, item.Size, item.Color);
    if (lookup.ByCompositeKey.TryGetValue(exactKey, out var exactVariant))
    {
        variant = exactVariant;
        return true;
    }

    var sizeAgnosticKey = BuildVariantLookupKey(item.ProductID, null, item.Color);
    if (lookup.ByCompositeKey.TryGetValue(sizeAgnosticKey, out var colorVariant))
    {
        variant = colorVariant;
        return true;
    }

    var colorAgnosticKey = BuildVariantLookupKey(item.ProductID, item.Size, null);
    if (lookup.ByCompositeKey.TryGetValue(colorAgnosticKey, out var sizeVariant))
    {
        variant = sizeVariant;
        return true;
    }

    var defaultKey = BuildVariantLookupKey(item.ProductID, null, null);
    if (lookup.ByCompositeKey.TryGetValue(defaultKey, out var defaultVariant))
    {
        variant = defaultVariant;
        return true;
    }

    return false;
}

private static string BuildVariantImageUrl(DbProductVariant variant)
{
    if (variant.ImageData is { Length: > 0 } || !string.IsNullOrWhiteSpace(variant.ImagePath))
    {
        return $"/ProductImage/Variant/{variant.Id}";
    }

    return string.Empty;
}

private async Task<DbProductVariant?> ResolveVariantForOrderItemAsync(OrderItem item)
{
    if (item.VariantId.HasValue)
    {
        var directVariant = await _context.ProductVariants.FirstOrDefaultAsync(v => v.Id == item.VariantId.Value);
        if (directVariant != null)
        {
            return directVariant;
        }
    }

    return await _context.ProductVariants.FirstOrDefaultAsync(v =>
        v.ProductId == item.ProductID
        && (item.Size == null || v.Size == item.Size)
        && (item.Color == null || v.Style == item.Color));
}
public async Task<bool> DeclineOrderAsync(int orderId, int sellerId, string reason)
{
    // 1. Find the order (with security check for the seller)
    var order = await _context.Orders
        .FirstOrDefaultAsync(o => o.OrderID == orderId && o.seller_id == sellerId);

    if (order == null) return false; 

    // 2. Change the status and save the exact reason
    order.Status = "Cancelled";
    order.CancellationReason = reason;

    // 3. Save to database
    await _context.SaveChangesAsync();
    return true;
}
public async Task<Order?> GetOrderByIdAsync(int orderId, int sellerId)
{
    var order = await _context.Orders
        .AsNoTracking()
        .Include(o => o.OrderItems)
            .ThenInclude(i => i.Product)
        .AsSplitQuery()
        .FirstOrDefaultAsync(o => o.OrderID == orderId && o.seller_id == sellerId);

    if (order != null)
    {
        await PopulateOrdersWithVariantDataAsync(new[] { order });
        await ApplySellerFacingStatusesAsync(sellerId, new[] { order });
    }

    return order;
}
public async Task ApplySellerFacingStatusesAsync(int sellerId, IList<Order> orders, CancellationToken cancellationToken = default)
{
    if (orders == null || orders.Count == 0)
    {
        return;
    }
    var orderIds = orders
        .Select(order => order.OrderID)
        .Where(orderId => orderId > 0)
        .Distinct()
        .ToList();
    Dictionary<int, string> latestReturnStatusesByOrderId = new();
    if (orderIds.Count > 0)
    {
        var latestReturnStatuses = await _context.ReturnRequests
            .AsNoTracking()
            .Where(request => request.SellerId == sellerId && orderIds.Contains(request.OrderId))
            .OrderByDescending(request => request.CreatedAt)
            .ThenByDescending(request => request.ReturnId)
            .Select(request => new
            {
                request.OrderId,
                request.Status
            })
            .ToListAsync(cancellationToken);
        latestReturnStatusesByOrderId = latestReturnStatuses
            .GroupBy(request => request.OrderId)
            .ToDictionary(
                group => group.Key,
                group => NormalizeStatus(group.First().Status));
    }
    foreach (var order in orders)
    {
        latestReturnStatusesByOrderId.TryGetValue(order.OrderID, out var returnStatus);
        order.EffectiveStatus = ResolveSellerFacingStatus(order.Status, returnStatus);
    }
}
public async Task<int> CountOrdersBySellerFacingStatusAsync(int sellerId, string status, CancellationToken cancellationToken = default)
{
    var orders = await _context.Orders
        .AsNoTracking()
        .Where(order => order.seller_id == sellerId)
        .Select(order => new Order
        {
            OrderID = order.OrderID,
            Status = order.Status,
            seller_id = order.seller_id
        })
        .ToListAsync(cancellationToken);
    await ApplySellerFacingStatusesAsync(sellerId, orders, cancellationToken);
    return orders.Count(order => string.Equals(order.EffectiveStatus, status, StringComparison.OrdinalIgnoreCase));
}
private static string ResolveSellerFacingStatus(string? orderStatus, string? returnStatus)
{
    return !string.IsNullOrWhiteSpace(returnStatus)
        ? NormalizeStatus(returnStatus)
        : NormalizeStatus(orderStatus);
}
private static string NormalizeStatus(string? status)
{
    return status?.Trim() ?? string.Empty;
}    }
}











