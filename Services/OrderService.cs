using System.Collections.Concurrent;
using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using MyAspNetApp.Data;
using MyAspNetApp.Models;

namespace MyAspNetApp.Services;

public class OrderService
{
    private static readonly ConcurrentDictionary<string, IReadOnlySet<string>> ColumnLookupCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly AppDbContext _dbContext;
    private readonly MediaPathService _mediaPathService;

    public OrderService(AppDbContext dbContext, MediaPathService mediaPathService)
    {
        _dbContext = dbContext;
        _mediaPathService = mediaPathService;
    }

    public async Task<List<OrderViewModel>> GetUserPurchasesAsync(int? userId, int? consumerId, CancellationToken cancellationToken)
    {
        if (!userId.HasValue && !consumerId.HasValue)
        {
            return new List<OrderViewModel>();
        }

        await using var connection = _dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            var orderColumns = await LoadColumnLookupAsync(connection, "Orders", cancellationToken);
            var orderItemColumns = await LoadColumnLookupAsync(connection, "OrderItems", cancellationToken);
            var productColumns = await LoadColumnLookupAsync(connection, "Products", cancellationToken);
            var variantColumns = await LoadColumnLookupAsync(connection, "ProductVariants", cancellationToken);
            var colorImageColumns = await LoadColumnLookupAsync(connection, "ProductColorImages", cancellationToken);
            var sellerColumns = await LoadColumnLookupAsync(connection, "Sellers", cancellationToken);

            var orderIdCol = FindColumn(orderColumns, "OrderId", "OrderID");
            var orderNumberCol = FindColumn(orderColumns, "OrderNumber", "OrderNo");
            var userIdCol = FindColumn(orderColumns, "UserId", "user_id", "UserID");
            var consumerIdCol = FindColumn(orderColumns, "ConsumerId", "consumer_id", "ConsumerID");
            var fullNameCol = FindColumn(orderColumns, "FullName", "full_name");
            var phoneCol = FindColumn(orderColumns, "PhoneNumber", "phone_number");
            var streetCol = FindColumn(orderColumns, "StreetAddress", "street_address");
            var cityCol = FindColumn(orderColumns, "City", "city");
            var postalCodeCol = FindColumn(orderColumns, "PostalCode", "postal_code");
            var paymentCol = FindColumn(orderColumns, "PaymentMethod", "payment_method");
            var statusCol = FindColumn(orderColumns, "Status", "status");
            var totalAmountCol = FindColumn(orderColumns, "TotalAmount", "total_amount");
            var orderDateCol = FindColumn(orderColumns, "OrderDate", "order_date", "CreatedAt", "created_at");
            var etaCol = FindColumn(orderColumns, "EstimatedDeliveryDate", "estimated_delivery_date");

            var orderItemOrderIdCol = FindColumn(orderItemColumns, "OrderId", "OrderID");
            var orderItemProductIdCol = FindColumn(orderItemColumns, "ProductId", "ProductID");
            var orderItemSellerIdCol = FindColumn(orderItemColumns, "SellerId", "SellerID");
            var orderItemSizeCol = FindColumn(orderItemColumns, "Size", "size");
            var orderItemColorCol = FindColumn(orderItemColumns, "Color", "color");
            var orderItemQtyCol = FindColumn(orderItemColumns, "Quantity", "quantity");
            var orderItemSortCol = FindColumn(orderItemColumns, "OrderItemId", "OrderItemID");
            var orderItemIdCol = FindColumn(orderItemColumns, "OrderItemId", "OrderItemID");
            var orderItemImagePathCol = FindColumn(orderItemColumns, "ProductImage", "ProductImagePath", "ImagePath", "Image");

            var productIdCol = FindColumn(productColumns, "ProductId", "ProductID", "Id", "ID");
            var productNameCol = FindColumn(productColumns, "ProductName", "Name", "name");
            var productImageCol = FindColumn(productColumns, "ImagePath", "Image", "image", "ProductImage");

            var variantProductIdCol = FindColumn(variantColumns, "ProductId", "ProductID");
            var variantSizeCol = FindColumn(variantColumns, "Size", "size");
            var variantStyleCol = FindColumn(variantColumns, "Style", "style", "Color", "color", "ColorName", "color_name");
            var variantIdCol = FindColumn(variantColumns, "VariantId", "VariantID", "Id", "ID");
            var variantImagePathCol = FindColumn(variantColumns, "imagePath", "ImagePath");

            var colorImageProductIdCol = FindColumn(colorImageColumns, "ProductId", "ProductID");
            var colorImageColorCol = FindColumn(colorImageColumns, "ColorName", "Color", "Style", "color_name");
            var colorImagePathCol = FindColumn(colorImageColumns, "ImagePath", "imagePath", "Image", "ProductImage");
            var colorImageSortCol = FindColumn(colorImageColumns, "Id", "ID", "ProductColorImageId", "ProductColorImageID");

            var sellerIdCol = FindColumn(sellerColumns, "SellerId", "SellerID", "seller_id");
            var sellerNameCol = FindColumn(sellerColumns, "BusinessName", "business_name", "ShopName", "shop_name");

            if (orderIdCol is null || orderItemOrderIdCol is null || orderItemProductIdCol is null || productIdCol is null)
            {
                return new List<OrderViewModel>();
            }

            var orderNumberExpr = orderNumberCol is not null
                ? $"COALESCE(o.[{orderNumberCol}], CONCAT(N'ORD-', CONVERT(NVARCHAR(20), o.[{orderIdCol}])))"
                : $"CONCAT(N'ORD-', CONVERT(NVARCHAR(20), o.[{orderIdCol}]))";

            var orderDateExpr = orderDateCol is not null
                ? $"o.[{orderDateCol}]"
                : "GETDATE()";

            var totalAmountExpr = totalAmountCol is not null
                ? $"o.[{totalAmountCol}]"
                : "0";

            var statusExpr = statusCol is not null ? $"o.[{statusCol}]" : "N'Pending'";
            var paymentExpr = paymentCol is not null ? $"o.[{paymentCol}]" : "N'GCash'";
            var fullNameExpr = fullNameCol is not null ? $"o.[{fullNameCol}]" : "N''";
            var phoneExpr = phoneCol is not null ? $"o.[{phoneCol}]" : "N''";
            var streetExpr = streetCol is not null ? $"o.[{streetCol}]" : "N''";
            var cityExpr = cityCol is not null ? $"o.[{cityCol}]" : "N''";
            var postalExpr = postalCodeCol is not null ? $"o.[{postalCodeCol}]" : "N''";
            var etaExpr = etaCol is not null ? $"o.[{etaCol}]" : "NULL";

            var orderItemColorSelect = orderItemColorCol is not null ? $"i.[{orderItemColorCol}] AS Color" : "NULL AS Color";
            var orderItemSizeSelect = orderItemSizeCol is not null ? $"i.[{orderItemSizeCol}] AS Size" : "NULL AS Size";
            var orderItemQtySelect = orderItemQtyCol is not null ? $"i.[{orderItemQtyCol}] AS Quantity" : "1 AS Quantity";
            var orderItemProductSelect = orderItemProductIdCol is not null ? $"i.[{orderItemProductIdCol}] AS ProductId" : "NULL AS ProductId";
            var orderItemSellerSelect = orderItemSellerIdCol is not null ? $"i.[{orderItemSellerIdCol}] AS SellerId" : "NULL AS SellerId";
            var orderItemIdSelect = orderItemIdCol is not null ? $"i.[{orderItemIdCol}] AS OrderItemId" : "NULL AS OrderItemId";
            var orderItemImagePathSelect = orderItemImagePathCol is not null ? $"i.[{orderItemImagePathCol}] AS ProductImage" : "NULL AS ProductImage";

            var sellerNameExpr = sellerNameCol is not null ? $"s.[{sellerNameCol}]" : "N'Monochrome Official Store'";
            var productNameExpr = productNameCol is not null ? $"p.[{productNameCol}]" : "N'Order Item'";
            var productImageExpr = productImageCol is not null ? $"p.[{productImageCol}]" : "NULL";

            var variantImagePathExpr = variantImagePathCol is not null ? $"v.[{variantImagePathCol}]" : "NULL";
            var variantIdExpr = variantIdCol is not null ? $"v.[{variantIdCol}]" : "NULL";
            var variantProductIdExpr = variantProductIdCol is not null ? $"v.[{variantProductIdCol}]" : "NULL";
            var variantSizeExpr = variantSizeCol is not null ? $"v.[{variantSizeCol}]" : "NULL";
            var variantStyleExpr = variantStyleCol is not null ? $"v.[{variantStyleCol}]" : "NULL";
            var variantSortExpr = variantIdCol is not null ? $"v.[{variantIdCol}]" : "(SELECT NULL)";
            var variantMatchProductExpr = variantProductIdCol is not null ? $"v.[{variantProductIdCol}] = oi.ProductId" : "1 = 0";
            var variantMatchSizeExpr = variantSizeCol is not null
                ? $"(oi.Size IS NULL OR LTRIM(RTRIM(CONVERT(NVARCHAR(100), v.[{variantSizeCol}]))) = LTRIM(RTRIM(CONVERT(NVARCHAR(100), oi.Size))))"
                : "1 = 1";
            var variantMatchStyleExpr = variantStyleCol is not null
                ? $"(oi.Color IS NULL OR LTRIM(RTRIM(CONVERT(NVARCHAR(100), v.[{variantStyleCol}]))) = LTRIM(RTRIM(CONVERT(NVARCHAR(100), oi.Color))))"
                : "1 = 1";
            var variantSizeOrderExpr = variantSizeCol is not null
                ? $"(oi.Size IS NOT NULL AND LTRIM(RTRIM(CONVERT(NVARCHAR(100), v.[{variantSizeCol}]))) = LTRIM(RTRIM(CONVERT(NVARCHAR(100), oi.Size))))"
                : "1 = 0";
            var variantStyleOrderExpr = variantStyleCol is not null
                ? $"(oi.Color IS NOT NULL AND LTRIM(RTRIM(CONVERT(NVARCHAR(100), v.[{variantStyleCol}]))) = LTRIM(RTRIM(CONVERT(NVARCHAR(100), oi.Color))))"
                : "1 = 0";

            var colorImagePathExpr = "ci.ImagePath";
            var colorImageApply = "OUTER APPLY (SELECT NULL AS ImagePath) ci";
            if (colorImageProductIdCol is not null && colorImagePathCol is not null)
            {
                var colorImageColorSelect = colorImageColorCol is not null ? $"ci0.[{colorImageColorCol}] AS ColorName" : "NULL AS ColorName";
                var colorImageColorMatchExpr = colorImageColorCol is not null
                    ? $"(oi.Color IS NULL OR LTRIM(RTRIM(CONVERT(NVARCHAR(100), ci0.[{colorImageColorCol}]))) = LTRIM(RTRIM(CONVERT(NVARCHAR(100), oi.Color))))"
                    : "1 = 1";
                var colorImageColorOrderExpr = colorImageColorCol is not null
                    ? $"(oi.Color IS NOT NULL AND LTRIM(RTRIM(CONVERT(NVARCHAR(100), ci0.[{colorImageColorCol}]))) = LTRIM(RTRIM(CONVERT(NVARCHAR(100), oi.Color))))"
                    : "1 = 0";
                var colorImageSortExpr = colorImageSortCol is not null ? $"ci0.[{colorImageSortCol}]" : "(SELECT NULL)";

                colorImageApply =
                    $"""
                    OUTER APPLY
                    (
                        SELECT TOP (1)
                            ci0.[{colorImagePathCol}] AS ImagePath,
                            {colorImageColorSelect}
                        FROM dbo.ProductColorImages ci0
                        WHERE ci0.[{colorImageProductIdCol}] = oi.ProductId
                          AND {colorImageColorMatchExpr}
                        ORDER BY
                            CASE WHEN {colorImageColorOrderExpr} THEN 0 ELSE 1 END,
                            {colorImageSortExpr}
                    ) ci
                    """;
            }

            var resolvedImagePathExpr = $"COALESCE(oi.ProductImage, {variantImagePathExpr}, {colorImagePathExpr}, {productImageExpr})";

            var orderFilters = new List<string>();
            if (userIdCol is not null)
            {
                orderFilters.Add($"(@UserIdText IS NOT NULL AND LTRIM(RTRIM(CONVERT(NVARCHAR(50), o.[{userIdCol}]))) = @UserIdText)");
            }

            if (consumerIdCol is not null)
            {
                orderFilters.Add($"(@ConsumerId IS NOT NULL AND o.[{consumerIdCol}] = @ConsumerId)");
            }

            if (orderFilters.Count == 0)
            {
                return new List<OrderViewModel>();
            }

            await using var command = connection.CreateCommand();
            command.CommandText =
                $"""
                SELECT
                    {orderNumberExpr} AS OrderNumber,
                    {statusExpr} AS Status,
                    {paymentExpr} AS PaymentMethod,
                    {fullNameExpr} AS FullName,
                    {phoneExpr} AS PhoneNumber,
                    {streetExpr} AS StreetAddress,
                    {cityExpr} AS City,
                    {postalExpr} AS PostalCode,
                    {orderDateExpr} AS OrderDate,
                    {etaExpr} AS EstimatedDeliveryDate,
                    {totalAmountExpr} AS TotalAmount,
                    oi.Color AS Color,
                    oi.Size AS Size,
                    oi.Quantity AS Quantity,
                    oi.OrderItemId AS OrderItemId,
                    v.VariantId AS VariantId,
                    {productNameExpr} AS ProductName,
                    {resolvedImagePathExpr} AS ImagePath,
                    {sellerNameExpr} AS SellerName
                FROM dbo.Orders o
                OUTER APPLY
                (
                    SELECT TOP (1)
                        {orderItemColorSelect},
                        {orderItemSizeSelect},
                        {orderItemQtySelect},
                        {orderItemProductSelect},
                        {orderItemSellerSelect},
                        {orderItemIdSelect},
                        {orderItemImagePathSelect}
                    FROM dbo.OrderItems i
                    WHERE i.[{orderItemOrderIdCol}] = o.[{orderIdCol}]
                    ORDER BY i.[{orderItemSortCol ?? orderItemOrderIdCol}] ASC
                ) oi
                OUTER APPLY
                (
                    SELECT TOP (1)
                        {variantIdExpr} AS VariantId,
                        {variantProductIdExpr} AS ProductId,
                        {variantSizeExpr} AS Size,
                        {variantStyleExpr} AS Style,
                        {variantImagePathExpr} AS ImagePath
                    FROM dbo.ProductVariants v
                    WHERE {variantMatchProductExpr}
                      AND {variantMatchSizeExpr}
                      AND {variantMatchStyleExpr}
                    ORDER BY
                        CASE WHEN {variantSizeOrderExpr} THEN 0 ELSE 1 END,
                        CASE WHEN {variantStyleOrderExpr} THEN 0 ELSE 1 END,
                        {variantSortExpr}
                ) v
                {colorImageApply}
                LEFT JOIN dbo.Products p ON p.[{productIdCol}] = oi.ProductId
                LEFT JOIN dbo.Sellers s ON {(sellerIdCol is not null ? $"s.[{sellerIdCol}] = oi.SellerId" : "1 = 0")}
                WHERE ({string.Join(" OR ", orderFilters)})
                ORDER BY COALESCE({orderDateExpr}, GETDATE()) DESC, o.[{orderIdCol}] DESC;
                """;
            command.CommandType = CommandType.Text;

            AddParameter(command, "@UserIdText", userId?.ToString(), DbType.String);
            AddParameter(command, "@ConsumerId", consumerId, DbType.Int32);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var results = new List<OrderViewModel>();

            while (await reader.ReadAsync(cancellationToken))
            {
                var streetAddress = GetString(reader, "StreetAddress");
                var city = GetString(reader, "City");
                var postalCode = GetString(reader, "PostalCode");
                var shippingAddress = string.Join(", ", new[] { streetAddress, city, postalCode }
                    .Where(value => !string.IsNullOrWhiteSpace(value)));

                var productImage = _mediaPathService.NormalizePublicPath(GetString(reader, "ImagePath"));
                var productName = GetString(reader, "ProductName");
                var orderItemId = GetInt32(reader, "OrderItemId");
                var variantId = GetInt32(reader, "VariantId");
                var imageSrc = BuildPurchaseImageUrl(orderItemId, variantId, productImage);

                results.Add(new OrderViewModel
                {
                    OrderNumber = GetString(reader, "OrderNumber", "Order"),
                    Status = NormalizeStatus(GetString(reader, "Status")),
                    ProductName = string.IsNullOrWhiteSpace(productName) ? "Order Item" : productName,
                    ProductImage = imageSrc,
                    PaymentMethod = NormalizePaymentMethod(GetString(reader, "PaymentMethod")),
                    SellerName = GetString(reader, "SellerName", "Monochrome Official Store"),
                    ReceiverName = GetString(reader, "FullName"),
                    PhoneNumber = GetString(reader, "PhoneNumber"),
                    ShippingAddress = shippingAddress,
                    OrderDate = GetDateTime(reader, "OrderDate") ?? DateTime.Now,
                    EstimatedArrival = GetDateTime(reader, "EstimatedDeliveryDate")?.ToString("MMM dd, yyyy"),
                    Color = GetString(reader, "Color", "N/A"),
                    Size = GetString(reader, "Size", "N/A"),
                    Quantity = GetInt32(reader, "Quantity") ?? 1,
                    TotalAmount = GetDecimal(reader, "TotalAmount") ?? 0m
                });
            }

            return results;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<IReadOnlySet<string>> LoadColumnLookupAsync(DbConnection connection, string tableName, CancellationToken cancellationToken)
    {
        if (ColumnLookupCache.TryGetValue(tableName, out var cachedColumns))
        {
            return cachedColumns;
        }

        var columns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME = @TableName;
            """;
        var parameter = command.CreateParameter();
        parameter.ParameterName = "@TableName";
        parameter.DbType = DbType.String;
        parameter.Value = tableName;
        command.Parameters.Add(parameter);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            if (!reader.IsDBNull(0))
            {
                columns.Add(reader.GetString(0));
            }
        }

        ColumnLookupCache[tableName] = columns;
        return columns;
    }

    private static string? FindColumn(IReadOnlySet<string> columns, params string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            if (columns.Contains(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static void AddParameter(DbCommand command, string name, object? value, DbType dbType)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    private static string GetString(DbDataReader reader, string columnName, string fallback = "")
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
        {
            return fallback;
        }

        var value = reader.GetValue(ordinal)?.ToString();
        return string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
    }

    private static int? GetInt32(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        return reader.GetValue(ordinal) switch
        {
            int value => value,
            long value => Convert.ToInt32(value),
            decimal value => Convert.ToInt32(value),
            short value => value,
            byte value => value,
            string value when int.TryParse(value, out var parsed) => parsed,
            _ => null
        };
    }

    private static decimal? GetDecimal(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        return reader.GetValue(ordinal) switch
        {
            decimal value => value,
            double value => Convert.ToDecimal(value),
            float value => Convert.ToDecimal(value),
            int value => value,
            long value => value,
            string value when decimal.TryParse(value, out var parsed) => parsed,
            _ => null
        };
    }

    private static string BuildPurchaseImageUrl(int? orderItemId, int? variantId, string? fallbackPath)
    {
        if (orderItemId.HasValue)
        {
            var url = $"/AccountProfile/OrderItemImage?orderItemId={orderItemId.Value}";
            if (variantId.HasValue)
            {
                url += $"&variantId={variantId.Value}";
            }

            return url;
        }

        return string.IsNullOrWhiteSpace(fallbackPath)
            ? "/images/placeholder.png"
            : fallbackPath;
    }

    private static DateTime? GetDateTime(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        return reader.GetValue(ordinal) switch
        {
            DateTime value => value,
            string value when DateTime.TryParse(value, out var parsed) => parsed,
            _ => null
        };
    }

    private static string NormalizeStatus(string? status)
    {
        return (status ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "pending" => "To Pay",
            "placed" => "To Pay",
            "processing" => "To Ship",
            "shipped" => "To Receive",
            "delivered" => "To Review",
            "completed" => "Completed",
            "returned" => "Returns",
            "return" => "Returns",
            "cancelled" => "Cancelled",
            "canceled" => "Cancelled",
            _ => "To Pay"
        };
    }

    private static string NormalizePaymentMethod(string? paymentMethod)
    {
        return (paymentMethod ?? string.Empty).Trim() switch
        {
            "Card" => "Credit Card",
            "PayPal" => "PayPal",
            "COD" => "COD",
            var value when string.IsNullOrWhiteSpace(value) => "GCash",
            var value => value
        };
    }

}
