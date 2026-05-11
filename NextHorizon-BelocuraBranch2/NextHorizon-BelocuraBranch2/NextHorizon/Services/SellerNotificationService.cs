using System.Data;
using System.Text.RegularExpressions;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using NextHorizon.Data;
using NextHorizon.Models;

namespace NextHorizon.Services;

public sealed class SellerNotificationService : ISellerNotificationService
{
    private static readonly Regex LegacyProductNameRegex = new("Your product \"(?<name>[^\"]+)\"", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly Regex ProductNameRegex = new(@"Product\s+(?<name>.+?)(?:\s+was|\s+is|\s+with|\s*$)", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly AppDbContext _dbContext;

    public SellerNotificationService(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SellerNotification> CreateAsync(SellerNotification notification, CancellationToken cancellationToken = default)
    {
        notification.RecipientType = string.IsNullOrWhiteSpace(notification.RecipientType) ? "seller" : notification.RecipientType.Trim();
        notification.CreatedAt = notification.CreatedAt == default ? DateTime.UtcNow : notification.CreatedAt;
        var parsedRecipientId = int.TryParse(notification.RecipientId, out var recipientIdValue) ? recipientIdValue : 0;

        var sql = @"
INSERT INTO [dbo].[Notifications] ([RecipientType], [RecipientId], [OrderId], [Message], [IsRead], [CreatedAt], [category])
VALUES (@recipientType, @recipientId, @orderId, @message, @isRead, @createdAt, @category);
SELECT CAST(SCOPE_IDENTITY() AS int);";

        var recipientType = new SqlParameter("@recipientType", SqlDbType.NVarChar, 32)
        {
            Value = notification.RecipientType
        };
        var recipientId = new SqlParameter("@recipientId", SqlDbType.Int)
        {
            Value = parsedRecipientId
        };
        var orderId = new SqlParameter("@orderId", SqlDbType.Int)
        {
            Value = notification.OrderId.HasValue ? notification.OrderId.Value : DBNull.Value
        };
        var message = new SqlParameter("@message", SqlDbType.NVarChar, 2000)
        {
            Value = notification.Message ?? string.Empty
        };
        var isRead = new SqlParameter("@isRead", SqlDbType.Bit)
        {
            Value = notification.IsRead
        };
        var createdAt = new SqlParameter("@createdAt", SqlDbType.DateTime2)
        {
            Value = notification.CreatedAt
        };
        var category = new SqlParameter("@category", SqlDbType.NVarChar, 64)
        {
            Value = notification.Category ?? string.Empty
        };

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        command.Parameters.Add(recipientType);
        command.Parameters.Add(recipientId);
        command.Parameters.Add(orderId);
        command.Parameters.Add(message);
        command.Parameters.Add(isRead);
        command.Parameters.Add(createdAt);
        command.Parameters.Add(category);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        notification.NotificationId = ConvertToInt(result);
        notification.RecipientId = recipientId.Value.ToString() ?? notification.RecipientId;
        return await NormalizeReadModelAsync(notification, parsedRecipientId, cancellationToken);
    }

    public async Task<SellerNotification> CreateIfMissingAsync(SellerNotification notification, CancellationToken cancellationToken = default)
    {
        notification.RecipientType = string.IsNullOrWhiteSpace(notification.RecipientType) ? "seller" : notification.RecipientType.Trim();
        notification.CreatedAt = notification.CreatedAt == default ? DateTime.UtcNow : notification.CreatedAt;

        var parsedRecipientId = int.TryParse(notification.RecipientId, out var recipientIdValue) ? recipientIdValue : 0;
        var existingId = await FindExistingNotificationIdAsync(notification, cancellationToken);
        if (existingId > 0)
        {
            notification.NotificationId = existingId;
            notification.RecipientId = parsedRecipientId > 0 ? parsedRecipientId.ToString() : notification.RecipientId;
            return await NormalizeReadModelAsync(notification, parsedRecipientId, cancellationToken);
        }

        return await CreateAsync(notification, cancellationToken);
    }

    public async Task<IReadOnlyList<SellerNotification>> ListAsync(int sellerId, int take = 20, bool unreadOnly = false, CancellationToken cancellationToken = default)
    {
        var notifications = new List<SellerNotification>();
        var safeTake = Math.Clamp(take, 1, 100);

        var sql = $@"
SELECT TOP (@take)
    TRY_CONVERT(int, [NotificationId]) AS [NotificationId],
    CONVERT(nvarchar(32), [RecipientType]) AS [RecipientType],
    CONVERT(nvarchar(64), [RecipientId]) AS [RecipientId],
    TRY_CONVERT(int, [OrderId]) AS [OrderId],
    CONVERT(nvarchar(2000), [Message]) AS [Message],
    TRY_CONVERT(bit, [IsRead]) AS [IsRead],
    TRY_CONVERT(datetime2, [CreatedAt]) AS [CreatedAt],
    CONVERT(nvarchar(64), [category]) AS [Category]
FROM [dbo].[Notifications]
WHERE CONVERT(nvarchar(32), [RecipientType]) = @recipientType
  AND CONVERT(nvarchar(64), [RecipientId]) = @recipientId
  {(unreadOnly ? "AND TRY_CONVERT(bit, [IsRead]) = 0" : string.Empty)}
ORDER BY TRY_CONVERT(datetime2, [CreatedAt]) DESC, TRY_CONVERT(int, [NotificationId]) DESC;";

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        command.Parameters.Add(new SqlParameter("@take", SqlDbType.Int) { Value = safeTake });
        command.Parameters.Add(new SqlParameter("@recipientType", SqlDbType.NVarChar, 32) { Value = "seller" });
        command.Parameters.Add(new SqlParameter("@recipientId", SqlDbType.NVarChar, 64) { Value = sellerId.ToString() });

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            notifications.Add(new SellerNotification
            {
                NotificationId = reader.IsDBNull(0) ? 0 : ConvertToInt(reader.GetValue(0)),
                RecipientType = reader.IsDBNull(1) ? "seller" : Convert.ToString(reader.GetValue(1)) ?? "seller",
                RecipientId = reader.IsDBNull(2) ? sellerId.ToString() : Convert.ToString(reader.GetValue(2)) ?? sellerId.ToString(),
                OrderId = reader.IsDBNull(3) ? null : ConvertToNullableInt(reader.GetValue(3)),
                Message = reader.IsDBNull(4) ? string.Empty : Convert.ToString(reader.GetValue(4)) ?? string.Empty,
                IsRead = !reader.IsDBNull(5) && Convert.ToBoolean(reader.GetValue(5)),
                CreatedAt = reader.IsDBNull(6) ? DateTime.UtcNow : Convert.ToDateTime(reader.GetValue(6)),
                Category = reader.IsDBNull(7) ? string.Empty : Convert.ToString(reader.GetValue(7)) ?? string.Empty
            });
        }

        var normalizedNotifications = new List<SellerNotification>(notifications.Count);
        foreach (var notification in notifications)
        {
            normalizedNotifications.Add(await NormalizeReadModelAsync(notification, sellerId, cancellationToken));
        }

        return CollapseDuplicateNotifications(normalizedNotifications)
            .Take(safeTake)
            .ToList();
    }

    public async Task<int> GetUnreadCountAsync(int sellerId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
SELECT COUNT(1)
FROM [dbo].[Notifications]
WHERE CONVERT(nvarchar(32), [RecipientType]) = @recipientType
  AND CONVERT(nvarchar(64), [RecipientId]) = @recipientId
  AND TRY_CONVERT(bit, [IsRead]) = 0;";

        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        command.Parameters.Add(new SqlParameter("@recipientType", SqlDbType.NVarChar, 32) { Value = "seller" });
        command.Parameters.Add(new SqlParameter("@recipientId", SqlDbType.NVarChar, 64) { Value = sellerId.ToString() });

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return ConvertToInt(result);
    }

    public async Task<bool> MarkReadAsync(int sellerId, int notificationId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE [dbo].[Notifications]
SET [IsRead] = 1
WHERE [NotificationId] = @notificationId
  AND CONVERT(nvarchar(32), [RecipientType]) = @recipientType
  AND CONVERT(nvarchar(64), [RecipientId]) = @recipientId
  AND TRY_CONVERT(bit, [IsRead]) = 0;";

        var affected = await ExecuteNonQueryAsync(
            sql,
            cancellationToken,
            new SqlParameter("@notificationId", SqlDbType.Int) { Value = notificationId },
            new SqlParameter("@recipientType", SqlDbType.NVarChar, 32) { Value = "seller" },
            new SqlParameter("@recipientId", SqlDbType.NVarChar, 64) { Value = sellerId.ToString() });

        return affected > 0;
    }

    public async Task<int> MarkAllReadAsync(int sellerId, CancellationToken cancellationToken = default)
    {
        const string sql = @"
UPDATE [dbo].[Notifications]
SET [IsRead] = 1
WHERE CONVERT(nvarchar(32), [RecipientType]) = @recipientType
  AND CONVERT(nvarchar(64), [RecipientId]) = @recipientId
  AND TRY_CONVERT(bit, [IsRead]) = 0;";

        return await ExecuteNonQueryAsync(
            sql,
            cancellationToken,
            new SqlParameter("@recipientType", SqlDbType.NVarChar, 32) { Value = "seller" },
            new SqlParameter("@recipientId", SqlDbType.NVarChar, 64) { Value = sellerId.ToString() });
    }

    private async Task<int> ExecuteNonQueryAsync(string sql, CancellationToken cancellationToken, params SqlParameter[] parameters)
    {
        var connection = _dbContext.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
        {
            await connection.OpenAsync(cancellationToken);
        }

        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.CommandType = CommandType.Text;
        foreach (var parameter in parameters)
        {
            command.Parameters.Add(parameter);
        }

        return await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<SellerNotification> NormalizeReadModelAsync(
        SellerNotification notification,
        int sellerId,
        CancellationToken cancellationToken)
    {
        notification.Category = InferCategory(notification);
        notification.Priority = InferPriority(notification);
        notification.DeliveryMode = InferDeliveryMode(notification);
        notification.Type = InferType(notification);
        notification.Title = InferTitle(notification);
        notification.Message = await NormalizeMessageAsync(notification, sellerId, cancellationToken);
        notification.ActionRequired = InferActionRequired(notification);
        notification.LinkType = InferLinkType(notification);
        notification.LinkTarget = InferLinkTarget(notification);
        return notification;
    }

    private async Task<int> FindExistingNotificationIdAsync(SellerNotification notification, CancellationToken cancellationToken)
    {
        var parsedRecipientId = int.TryParse(notification.RecipientId, out var recipientIdValue) ? recipientIdValue : 0;

        var recipientId = parsedRecipientId > 0 ? parsedRecipientId.ToString() : notification.RecipientId;

        var existingId = await _dbContext.SellerNotifications
            .AsNoTracking()
            .Where(item => item.RecipientType == notification.RecipientType)
            .Where(item => item.RecipientId == recipientId)
            .Where(item => item.OrderId == notification.OrderId)
            .Where(item => item.Category == (notification.Category ?? string.Empty))
            .Where(item => item.Message == (notification.Message ?? string.Empty))
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.NotificationId)
            .Select(item => item.NotificationId)
            .FirstOrDefaultAsync(cancellationToken);

        return existingId;
    }

    private async Task<string> NormalizeMessageAsync(
        SellerNotification notification,
        int sellerId,
        CancellationToken cancellationToken)
    {
        var message = (notification.Message ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(message))
        {
            return string.Empty;
        }

        message = Regex.Replace(message, @"\bno image attach\b", "No image attached", RegexOptions.IgnoreCase);
        message = Regex.Replace(message, @"(?<=[.!?])(?=[A-Z])", " ");
        message = Regex.Replace(message, @"\s+", " ").Trim();

        if (!message.Contains("Price: ?0.00", StringComparison.OrdinalIgnoreCase)
            && !message.Contains("Price: ₱0.00", StringComparison.OrdinalIgnoreCase)
            && !message.Contains("Price: PHP 0.00", StringComparison.OrdinalIgnoreCase))
        {
            return message;
        }

        var productNameMatch = LegacyProductNameRegex.Match(message);
        if (!productNameMatch.Success)
        {
            return message;
        }

        var productName = productNameMatch.Groups["name"].Value.Trim();
        if (string.IsNullOrWhiteSpace(productName) || sellerId <= 0)
        {
            return message;
        }

        var productPrice = await ResolveCurrentProductPriceAsync(sellerId, productName, cancellationToken);
        if (!productPrice.HasValue || productPrice.Value <= 0)
        {
            return message;
        }

        return Regex.Replace(
            message,
            @"Price:\s*(\?|₱|PHP\s*)?0\.00",
            $"Price: ₱{productPrice.Value:N2}",
            RegexOptions.IgnoreCase);
    }

    private async Task<decimal?> ResolveCurrentProductPriceAsync(
        int sellerId,
        string productName,
        CancellationToken cancellationToken)
    {
        var product = await _dbContext.Products
            .AsNoTracking()
            .Where(product => product.SellerId == sellerId && product.ProductName == productName)
            .Select(product => new
            {
                product.ProductId,
                product.Price
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (product == null)
        {
            return null;
        }

        var variantPrices = await _dbContext.ProductVariants
            .AsNoTracking()
            .Where(variant => variant.ProductId == product.ProductId && variant.Price.HasValue && variant.Price.Value > 0)
            .Select(variant => variant.Price!.Value)
            .ToListAsync(cancellationToken);

        return variantPrices.Count > 0 ? variantPrices.Min() : product.Price;
    }

    private static string InferTitle(SellerNotification notification)
    {
        if (!string.IsNullOrWhiteSpace(notification.Title))
        {
            return notification.Title;
        }

        var category = notification.Category?.Trim() ?? string.Empty;
        var normalizedCategory = category.ToLowerInvariant();
        var message = notification.Message?.Trim() ?? string.Empty;

        if (normalizedCategory is "productapproval" or "product_approval")
        {
            return "Product Approved";
        }

        if (normalizedCategory is "productrejection" or "product_rejection")
        {
            return "Product Rejected";
        }

        if (message.Contains("approved and is now live", StringComparison.OrdinalIgnoreCase))
        {
            return "Product Approved";
        }

        if (message.Contains("rejected.", StringComparison.OrdinalIgnoreCase)
            || message.Contains("rejected. reason:", StringComparison.OrdinalIgnoreCase))
        {
            return "Product Rejected";
        }

        if (message.Contains("pending approval", StringComparison.OrdinalIgnoreCase))
        {
            return "Product Submitted";
        }

        if (message.Contains("new order", StringComparison.OrdinalIgnoreCase)
            || message.Contains("receive a new order", StringComparison.OrdinalIgnoreCase)
            || message.Contains("received a new order", StringComparison.OrdinalIgnoreCase))
        {
            return "New Order Received";
        }

        if (message.Contains("waiting for your confirmation", StringComparison.OrdinalIgnoreCase)
            || message.Contains("fulfillment update", StringComparison.OrdinalIgnoreCase))
        {
            return "Order Needs Your Action";
        }

        if (message.Contains("payout account", StringComparison.OrdinalIgnoreCase))
        {
            return "Payout Account Update";
        }

        if (message.Contains("withdrawal request", StringComparison.OrdinalIgnoreCase))
        {
            return "Withdrawal Requested";
        }

        if (message.Contains("promotion", StringComparison.OrdinalIgnoreCase))
        {
            return message.Contains("awaiting review", StringComparison.OrdinalIgnoreCase)
                ? "Promotion Submitted"
                : "Promotion Updated";
        }

        if (message.Contains("help center", StringComparison.OrdinalIgnoreCase)
            || message.Contains("support", StringComparison.OrdinalIgnoreCase))
        {
            return "Help Center Update";
        }

        if (message.Contains("product", StringComparison.OrdinalIgnoreCase))
        {
            if (message.Contains("running low on stock", StringComparison.OrdinalIgnoreCase))
            {
                return "Low Stock Alert";
            }

            if (message.Contains("out of stock", StringComparison.OrdinalIgnoreCase))
            {
                return "Out of Stock";
            }

            return "Product Update";
        }

        return normalizedCategory switch
        {
            "order" => "Order Update",
            "refund" => "Refund Update",
            "return" => "Return Update",
            "cancellation" => "Cancellation Update",
            "message" => "New Message",
            "payout" => "Payout Update",
            "inventory" => "Inventory Alert",
            "system" => "System Update",
            _ => "Notification"
        };
    }

    private static string InferType(SellerNotification notification)
    {
        if (!string.IsNullOrWhiteSpace(notification.Type))
        {
            return notification.Type.Trim();
        }

        var category = InferCategory(notification);
        var message = (notification.Message ?? string.Empty).Trim();

        return category switch
        {
            "message" when message.Contains("attachment", StringComparison.OrdinalIgnoreCase) => "message.follow_up_attachment_alert",
            "message" => "message.new_customer_message",
            "order" when message.Contains("ready for shipment", StringComparison.OrdinalIgnoreCase) => "order.ready_to_ship",
            "order" when message.Contains("waiting for your confirmation", StringComparison.OrdinalIgnoreCase) => "order.pending_seller_action",
            "order" when message.Contains("new order", StringComparison.OrdinalIgnoreCase) => "order.new_received",
            "order" => "order.status_updated",
            "return" when message.Contains("submitted", StringComparison.OrdinalIgnoreCase) => "return.request_submitted",
            "return" when message.Contains("completed", StringComparison.OrdinalIgnoreCase) => "return.completed",
            "return" => "return.status_updated",
            "refund" => "refund.completed",
            "inventory" when message.Contains("out of stock", StringComparison.OrdinalIgnoreCase) => "inventory.out_of_stock",
            "inventory" => "inventory.low_stock",
            "payout" when message.Contains("withdrawal request", StringComparison.OrdinalIgnoreCase) => "payout.withdrawal_requested",
            "system" when message.Contains("promotion", StringComparison.OrdinalIgnoreCase) => "system.promotion_update",
            "system" when message.Contains("help center", StringComparison.OrdinalIgnoreCase)
                || message.Contains("support", StringComparison.OrdinalIgnoreCase) => "system.support_update",
            "system" when message.Contains("product", StringComparison.OrdinalIgnoreCase) => "system.product_update",
            _ => string.IsNullOrWhiteSpace(category) ? "notification" : category
        };
    }

    private static string InferPriority(SellerNotification notification)
    {
        if (!string.IsNullOrWhiteSpace(notification.Priority))
        {
            return notification.Priority.Trim().ToLowerInvariant();
        }

        var category = InferCategory(notification);
        var message = (notification.Message ?? string.Empty).Trim();

        if (category is "message" or "inventory")
        {
            return "high";
        }

        if (category is "refund" or "return")
        {
            return "high";
        }

        if (message.Contains("pending", StringComparison.OrdinalIgnoreCase)
            || message.Contains("awaiting", StringComparison.OrdinalIgnoreCase))
        {
            return "high";
        }

        return "medium";
    }

    private static string InferDeliveryMode(SellerNotification notification)
    {
        if (!string.IsNullOrWhiteSpace(notification.DeliveryMode))
        {
            return notification.DeliveryMode.Trim();
        }

        return InferCategory(notification) switch
        {
            "message" => "realtime,in_app",
            "refund" => "in_app,email",
            "return" => "in_app,email",
            "inventory" => "in_app,email",
            "payout" => "realtime,email,in_app",
            _ => "in_app"
        };
    }

    private static bool InferActionRequired(SellerNotification notification)
    {
        if (notification.ActionRequired)
        {
            return true;
        }

        var message = (notification.Message ?? string.Empty).Trim();
        var type = InferType(notification);

        return type is "message.new_customer_message"
            or "message.follow_up_attachment_alert"
            or "order.pending_seller_action"
            or "order.ready_to_ship"
            or "return.request_submitted"
            or "inventory.low_stock"
            or "inventory.out_of_stock"
            || message.Contains("needs review", StringComparison.OrdinalIgnoreCase)
            || message.Contains("awaiting", StringComparison.OrdinalIgnoreCase)
            || message.Contains("waiting for your", StringComparison.OrdinalIgnoreCase);
    }

    private static string? InferLinkType(SellerNotification notification)
    {
        if (!string.IsNullOrWhiteSpace(notification.LinkType))
        {
            return notification.LinkType.Trim();
        }

        return InferCategory(notification) switch
        {
            "message" => "message_thread",
            "order" or "refund" or "return" or "cancellation" => "order",
            "inventory" => "product",
            "payout" => "payout",
            "system" when IsPromotionMessage(notification.Message) => "system_page",
            "system" when IsSupportMessage(notification.Message) => "system_page",
            "system" when IsProductMessage(notification.Message) => "product",
            _ => null
        };
    }

    private static string? InferLinkTarget(SellerNotification notification)
    {
        if (!string.IsNullOrWhiteSpace(notification.LinkTarget))
        {
            return notification.LinkTarget.Trim();
        }

        var category = InferCategory(notification);
        var message = (notification.Message ?? string.Empty).Trim();

        return category switch
        {
            "message" => notification.OrderId.HasValue
                ? $"/Seller/SellerMessenger?orderId={notification.OrderId.Value}"
                : "/Seller/SellerMessenger",
            "order" or "refund" or "cancellation" => "/Dashboard/OrderManagement",
            "return" => "/Dashboard/OrderManagement?status=Return",
            "inventory" => TryInferProductLink(message),
            "payout" => message.Contains("account", StringComparison.OrdinalIgnoreCase)
                ? "/Dashboard/PayoutAccounts"
                : "/Dashboard/Withdraw",
            "system" when IsPromotionMessage(message) => "/Promotions/Promotion",
            "system" when IsSupportMessage(message) => "/Dashboard/HelpCenter",
            "system" when IsProductMessage(message) => TryInferProductLink(message),
            _ => null
        };
    }

    private static bool IsPromotionMessage(string? message)
        => (message ?? string.Empty).Contains("promotion", StringComparison.OrdinalIgnoreCase);

    private static bool IsSupportMessage(string? message)
        => (message ?? string.Empty).Contains("help center", StringComparison.OrdinalIgnoreCase)
            || (message ?? string.Empty).Contains("support", StringComparison.OrdinalIgnoreCase);

    private static bool IsProductMessage(string? message)
        => (message ?? string.Empty).Contains("product", StringComparison.OrdinalIgnoreCase);

    private static string InferCategory(SellerNotification notification)
    {
        var category = (notification.Category ?? string.Empty).Trim().ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(category))
        {
            return category;
        }

        var message = (notification.Message ?? string.Empty).Trim();
        if (message.Contains("order #", StringComparison.OrdinalIgnoreCase)
            || message.Contains("new order", StringComparison.OrdinalIgnoreCase)
            || message.Contains("receive a new order", StringComparison.OrdinalIgnoreCase)
            || message.Contains("received a new order", StringComparison.OrdinalIgnoreCase)
            || message.Contains("waiting for your confirmation", StringComparison.OrdinalIgnoreCase))
        {
            return "order";
        }

        if (message.Contains("return", StringComparison.OrdinalIgnoreCase))
        {
            return "return";
        }

        if (message.Contains("refund", StringComparison.OrdinalIgnoreCase))
        {
            return "refund";
        }

        if (message.Contains("withdrawal", StringComparison.OrdinalIgnoreCase)
            || message.Contains("payout", StringComparison.OrdinalIgnoreCase))
        {
            return "payout";
        }

        if (message.Contains("stock", StringComparison.OrdinalIgnoreCase))
        {
            return "inventory";
        }

        if (message.Contains("message", StringComparison.OrdinalIgnoreCase))
        {
            return "message";
        }

        if (message.Contains("promotion", StringComparison.OrdinalIgnoreCase)
            || message.Contains("product", StringComparison.OrdinalIgnoreCase)
            || message.Contains("help center", StringComparison.OrdinalIgnoreCase)
            || message.Contains("support", StringComparison.OrdinalIgnoreCase))
        {
            return "system";
        }

        return string.Empty;
    }

    private static IReadOnlyList<SellerNotification> CollapseDuplicateNotifications(IReadOnlyList<SellerNotification> notifications)
    {
        var suppressedTypes = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "order.new_received"
        };

        var actionOrderIds = notifications
            .Where(item => item.OrderId.HasValue && string.Equals(item.Type, "order.pending_seller_action", StringComparison.OrdinalIgnoreCase))
            .Select(item => item.OrderId!.Value)
            .ToHashSet();

        return notifications
            .Where(item => !(item.OrderId.HasValue
                && actionOrderIds.Contains(item.OrderId.Value)
                && suppressedTypes.Contains(item.Type)))
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.NotificationId)
            .ToList();
    }

    private static string? TryInferProductLink(string? message)
    {
        var match = ProductNameRegex.Match(message ?? string.Empty);
        if (!match.Success)
        {
            return "/Seller";
        }

        return "/Seller";
    }

    private static int ConvertToInt(object? value)
    {
        if (value == null || value == DBNull.Value)
        {
            return 0;
        }

        return Convert.ToInt32(value);
    }

    private static int? ConvertToNullableInt(object? value)
    {
        if (value == null || value == DBNull.Value)
        {
            return null;
        }

        return Convert.ToInt32(value);
    }
}
