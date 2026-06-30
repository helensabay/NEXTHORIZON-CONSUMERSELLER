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
                    o.[{orderIdCol}] AS OrderId,
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
                    oi.ProductId AS ProductId,
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
                    OrderId = GetInt32(reader, "OrderId") ?? 0,
                    ProductId = GetInt32(reader, "ProductId") ?? 0,
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

    public async Task<bool> UpdateUserPurchaseStatusAsync(int orderId, int? userId, int? consumerId, string status, CancellationToken cancellationToken)
    {
        if (orderId <= 0 || (!userId.HasValue && !consumerId.HasValue) || string.IsNullOrWhiteSpace(status))
        {
            return false;
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
            var orderIdCol = FindColumn(orderColumns, "OrderId", "OrderID");
            var userIdCol = FindColumn(orderColumns, "UserId", "user_id", "UserID");
            var consumerIdCol = FindColumn(orderColumns, "ConsumerId", "consumer_id", "ConsumerID");
            var statusCol = FindColumn(orderColumns, "Status", "status", "FulfillmentStatus");

            if (orderIdCol is null || statusCol is null)
            {
                return false;
            }

            var ownershipFilters = BuildOwnershipFilters(userIdCol, consumerIdCol);
            if (ownershipFilters.Count == 0)
            {
                return false;
            }

            await using var command = connection.CreateCommand();
            command.CommandText =
                $"""
                UPDATE dbo.Orders
                SET [{statusCol}] = @Status
                WHERE [{orderIdCol}] = @OrderId
                  AND ({string.Join(" OR ", ownershipFilters)});
                """;
            AddParameter(command, "@Status", status.Trim(), DbType.String);
            AddParameter(command, "@OrderId", orderId, DbType.Int32);
            AddParameter(command, "@UserIdText", userId?.ToString(), DbType.String);
            AddParameter(command, "@ConsumerId", consumerId, DbType.Int32);

            return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<bool> RequestUserPurchaseReturnAsync(
        int orderId,
        int? userId,
        int? consumerId,
        string reason,
        IReadOnlyList<string> proofUrls,
        byte[]? firstProofBytes,
        string? firstProofMimeType,
        CancellationToken cancellationToken)
    {
        if (orderId <= 0 ||
            (!userId.HasValue && !consumerId.HasValue) ||
            string.IsNullOrWhiteSpace(reason) ||
            proofUrls.Count == 0)
        {
            return false;
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
            var returnsColumns = await LoadColumnLookupAsync(connection, "returns", cancellationToken);
            var returnsTableName = "returns";
            if (returnsColumns.Count == 0)
            {
                returnsColumns = await LoadColumnLookupAsync(connection, "Returns", cancellationToken);
                returnsTableName = "Returns";
            }

            var orderIdCol = FindColumn(orderColumns, "OrderId", "OrderID");
            var userIdCol = FindColumn(orderColumns, "UserId", "user_id", "UserID");
            var consumerIdCol = FindColumn(orderColumns, "ConsumerId", "consumer_id", "ConsumerID");
            var statusCol = FindColumn(orderColumns, "Status", "status", "FulfillmentStatus");
            var reasonCol = FindColumn(orderColumns, "ReturnReason", "return_reason", "FailedDeliveryReason");
            var noteCol = FindColumn(orderColumns, "ReturnNote", "return_note");
            var proofUrlCol = FindColumn(orderColumns, "ReturnProofImage", "ReturnProofUrl", "return_proof", "ProofOfReturn");
            var proofUrlsCol = FindColumn(orderColumns, "ReturnProofImages", "ReturnProofUrls", "ReturnProofImagesJson", "ReturnProofUrlsJson");
            var proofDataCol = FindColumn(orderColumns, "ReturnProofImageData", "ReturnProofData", "ProofOfReturnData");
            var proofMimeCol = FindColumn(orderColumns, "ReturnProofImageMimeType", "ReturnProofMimeType", "ProofOfReturnMimeType");

            if (orderIdCol is null || statusCol is null)
            {
                return false;
            }

            var ownershipFilters = BuildOwnershipFilters(userIdCol, consumerIdCol);
            if (ownershipFilters.Count == 0)
            {
                return false;
            }

            var orderItemOrderIdCol = FindColumn(orderItemColumns, "OrderId", "OrderID");
            var orderItemSellerIdCol = FindColumn(orderItemColumns, "SellerId", "SellerID", "seller_id");
            var orderSellerCol = FindColumn(orderColumns, "SellerId", "SellerID", "seller_id");
            var sellerId = await ResolveOrderSellerIdAsync(
                connection,
                orderId,
                orderIdCol,
                orderSellerCol,
                orderItemOrderIdCol,
                orderItemSellerIdCol,
                cancellationToken);

            var normalizedReason = reason.Trim();
            var proofJson = System.Text.Json.JsonSerializer.Serialize(proofUrls);
            var assignments = new List<string> { $"{Quote(statusCol)} = @Status" };
            if (reasonCol is not null)
            {
                assignments.Add($"{Quote(reasonCol)} = @ReturnReason");
            }
            if (noteCol is not null)
            {
                assignments.Add($"{Quote(noteCol)} = @ReturnNote");
            }
            if (proofUrlCol is not null)
            {
                assignments.Add($"{Quote(proofUrlCol)} = @ReturnProofUrl");
            }
            if (proofUrlsCol is not null)
            {
                assignments.Add($"{Quote(proofUrlsCol)} = @ReturnProofUrls");
            }
            if (proofDataCol is not null && firstProofBytes is { Length: > 0 })
            {
                assignments.Add($"{Quote(proofDataCol)} = @ReturnProofData");
            }
            if (proofMimeCol is not null && firstProofBytes is { Length: > 0 })
            {
                assignments.Add($"{Quote(proofMimeCol)} = @ReturnProofMimeType");
            }

            await using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    $"""
                    UPDATE dbo.Orders
                    SET {string.Join(", ", assignments)}
                    WHERE {Quote(orderIdCol)} = @OrderId
                      AND ({string.Join(" OR ", ownershipFilters)})
                      AND LTRIM(RTRIM(CONVERT(NVARCHAR(50), {Quote(statusCol)}))) IN (N'Completed', N'Complete', N'To Review', N'Delivered');
                    """;
                AddParameter(command, "@Status", "Return Requested", DbType.String);
                AddParameter(command, "@ReturnReason", normalizedReason, DbType.String);
                AddParameter(command, "@ReturnNote", $"Buyer uploaded {proofUrls.Count} return proof image(s).", DbType.String);
                AddParameter(command, "@ReturnProofUrl", proofUrls[0], DbType.String);
                AddParameter(command, "@ReturnProofUrls", proofJson, DbType.String);
                AddParameter(command, "@ReturnProofData", firstProofBytes, DbType.Binary);
                AddParameter(command, "@ReturnProofMimeType", firstProofMimeType, DbType.String);
                AddParameter(command, "@OrderId", orderId, DbType.Int32);
                AddParameter(command, "@UserIdText", userId?.ToString(), DbType.String);
                AddParameter(command, "@ConsumerId", consumerId, DbType.Int32);

                if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
                {
                    return false;
                }
            }

            if (returnsColumns.Count > 0 && sellerId.HasValue)
            {
                await UpsertReturnRequestAsync(
                    connection,
                    returnsTableName,
                    returnsColumns,
                    orderId,
                    userId,
                    sellerId.Value,
                    normalizedReason,
                    proofJson,
                    cancellationToken);
            }

            return true;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<bool> ConfirmUserPurchaseReceivedAsync(int orderId, int? userId, int? consumerId, CancellationToken cancellationToken)
    {
        if (orderId <= 0 || (!userId.HasValue && !consumerId.HasValue))
        {
            return false;
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
            var orderIdCol = FindColumn(orderColumns, "OrderId", "OrderID");
            var userIdCol = FindColumn(orderColumns, "UserId", "user_id", "UserID");
            var consumerIdCol = FindColumn(orderColumns, "ConsumerId", "consumer_id", "ConsumerID");
            var statusCol = FindColumn(orderColumns, "Status", "status");
            var fulfillmentStatusCol = FindColumn(orderColumns, "FulfillmentStatus", "fulfillment_status");

            if (orderIdCol is null || statusCol is null)
            {
                return false;
            }

            var selectColumns = new List<string>
            {
                $"[{orderIdCol}] AS OrderId",
                $"[{statusCol}] AS Status"
            };
            if (userIdCol is not null)
            {
                selectColumns.Add($"[{userIdCol}] AS OrderUserId");
            }
            if (consumerIdCol is not null)
            {
                selectColumns.Add($"[{consumerIdCol}] AS OrderConsumerId");
            }
            if (fulfillmentStatusCol is not null)
            {
                selectColumns.Add($"[{fulfillmentStatusCol}] AS OrderFulfillmentStatus");
            }

            await using (var command = connection.CreateCommand())
            {
                command.CommandText =
                    $"""
                    SELECT TOP (1) {string.Join(", ", selectColumns)}
                    FROM dbo.Orders
                    WHERE [{orderIdCol}] = @OrderId;
                    """;
                AddParameter(command, "@OrderId", orderId, DbType.Int32);

                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                if (!await reader.ReadAsync(cancellationToken))
                {
                    return false;
                }

                var currentStatus = NormalizeStatus(GetString(reader, "Status"));
                var currentFulfillment = fulfillmentStatusCol is null
                    ? string.Empty
                    : NormalizeStatus(GetString(reader, "OrderFulfillmentStatus"));

                var ownershipMatches =
                    (userId.HasValue && string.Equals(GetString(reader, "OrderUserId"), userId.Value.ToString(), StringComparison.OrdinalIgnoreCase)) ||
                    (consumerId.HasValue && GetInt32(reader, "OrderConsumerId") == consumerId.Value);

                if (!ownershipMatches)
                {
                    return false;
                }

                var canConfirm = currentStatus is "Shipped" or "To Receive" or "Delivered"
                    || currentFulfillment is "Shipped" or "To Receive" or "Delivered";
                if (!canConfirm)
                {
                    return false;
                }
            }

            var assignments = new List<string> { $"[{statusCol}] = @Status" };
            if (fulfillmentStatusCol is not null)
            {
                assignments.Add($"[{fulfillmentStatusCol}] = @FulfillmentStatus");
            }

            await using var updateCommand = connection.CreateCommand();
            updateCommand.CommandText =
                $"""
                UPDATE dbo.Orders
                SET {string.Join(", ", assignments)}
                WHERE [{orderIdCol}] = @OrderId;
                """;
            AddParameter(updateCommand, "@Status", "Completed", DbType.String);
            AddParameter(updateCommand, "@FulfillmentStatus", "Completed", DbType.String);
            AddParameter(updateCommand, "@OrderId", orderId, DbType.Int32);

            return await updateCommand.ExecuteNonQueryAsync(cancellationToken) > 0;
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    public async Task<bool> UpdateUserPurchaseDetailsAsync(
        int orderId,
        int? userId,
        int? consumerId,
        string receiverName,
        string phoneNumber,
        string shippingAddress,
        string paymentMethod,
        string? color,
        string? size,
        int quantity,
        CancellationToken cancellationToken)
    {
        if (orderId <= 0 || (!userId.HasValue && !consumerId.HasValue))
        {
            return false;
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

            var orderIdCol = FindColumn(orderColumns, "OrderId", "OrderID");
            var userIdCol = FindColumn(orderColumns, "UserId", "user_id", "UserID");
            var consumerIdCol = FindColumn(orderColumns, "ConsumerId", "consumer_id", "ConsumerID");
            var statusCol = FindColumn(orderColumns, "Status", "status", "FulfillmentStatus");
            var fullNameCol = FindColumn(orderColumns, "FullName", "full_name");
            var phoneCol = FindColumn(orderColumns, "PhoneNumber", "phone_number");
            var streetCol = FindColumn(orderColumns, "StreetAddress", "street_address");
            var cityCol = FindColumn(orderColumns, "City", "city");
            var postalCodeCol = FindColumn(orderColumns, "PostalCode", "postal_code");
            var paymentCol = FindColumn(orderColumns, "PaymentMethod", "payment_method");

            var orderItemOrderIdCol = FindColumn(orderItemColumns, "OrderId", "OrderID");
            var orderItemSortCol = FindColumn(orderItemColumns, "OrderItemId", "OrderItemID");
            var orderItemSizeCol = FindColumn(orderItemColumns, "Size", "size");
            var orderItemColorCol = FindColumn(orderItemColumns, "Color", "color");
            var orderItemQtyCol = FindColumn(orderItemColumns, "Quantity", "quantity");

            if (orderIdCol is null || statusCol is null)
            {
                return false;
            }

            var ownershipFilters = BuildOwnershipFilters(userIdCol, consumerIdCol);
            if (ownershipFilters.Count == 0)
            {
                return false;
            }

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);
            try
            {
                await using (var command = connection.CreateCommand())
                {
                    command.Transaction = transaction;
                    var assignments = new List<string>();
                    if (fullNameCol is not null) assignments.Add($"[{fullNameCol}] = @FullName");
                    if (phoneCol is not null) assignments.Add($"[{phoneCol}] = @PhoneNumber");
                    if (streetCol is not null) assignments.Add($"[{streetCol}] = @StreetAddress");
                    if (cityCol is not null) assignments.Add($"[{cityCol}] = NULL");
                    if (postalCodeCol is not null) assignments.Add($"[{postalCodeCol}] = NULL");
                    if (paymentCol is not null) assignments.Add($"[{paymentCol}] = @PaymentMethod");

                    if (assignments.Count == 0)
                    {
                        return false;
                    }

                    command.CommandText =
                        $"""
                        UPDATE dbo.Orders
                        SET {string.Join(", ", assignments)}
                        WHERE [{orderIdCol}] = @OrderId
                          AND LOWER(LTRIM(RTRIM(CONVERT(NVARCHAR(50), [{statusCol}])))) IN (N'pending', N'placed', N'to pay')
                          AND ({string.Join(" OR ", ownershipFilters)});
                        """;
                    AddParameter(command, "@FullName", receiverName.Trim(), DbType.String);
                    AddParameter(command, "@PhoneNumber", phoneNumber.Trim(), DbType.String);
                    AddParameter(command, "@StreetAddress", shippingAddress.Trim(), DbType.String);
                    AddParameter(command, "@PaymentMethod", NormalizePaymentMethod(paymentMethod), DbType.String);
                    AddParameter(command, "@OrderId", orderId, DbType.Int32);
                    AddParameter(command, "@UserIdText", userId?.ToString(), DbType.String);
                    AddParameter(command, "@ConsumerId", consumerId, DbType.Int32);

                    if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        return false;
                    }
                }

                if (orderItemOrderIdCol is not null && (orderItemColorCol is not null || orderItemSizeCol is not null || orderItemQtyCol is not null))
                {
                    await using var itemCommand = connection.CreateCommand();
                    itemCommand.Transaction = transaction;
                    var itemAssignments = new List<string>();
                    if (orderItemColorCol is not null) itemAssignments.Add($"[{orderItemColorCol}] = @Color");
                    if (orderItemSizeCol is not null) itemAssignments.Add($"[{orderItemSizeCol}] = @Size");
                    if (orderItemQtyCol is not null) itemAssignments.Add($"[{orderItemQtyCol}] = @Quantity");

                    itemCommand.CommandText =
                        $"""
                        UPDATE dbo.OrderItems
                        SET {string.Join(", ", itemAssignments)}
                        WHERE [{orderItemOrderIdCol}] = @OrderId
                          AND [{orderItemSortCol ?? orderItemOrderIdCol}] =
                          (
                              SELECT TOP (1) [{orderItemSortCol ?? orderItemOrderIdCol}]
                              FROM dbo.OrderItems
                              WHERE [{orderItemOrderIdCol}] = @OrderId
                              ORDER BY [{orderItemSortCol ?? orderItemOrderIdCol}] ASC
                          );
                        """;
                    AddParameter(itemCommand, "@Color", string.IsNullOrWhiteSpace(color) ? DBNull.Value : color.Trim(), DbType.String);
                    AddParameter(itemCommand, "@Size", string.IsNullOrWhiteSpace(size) ? DBNull.Value : size.Trim(), DbType.String);
                    AddParameter(itemCommand, "@Quantity", Math.Max(1, quantity), DbType.Int32);
                    AddParameter(itemCommand, "@OrderId", orderId, DbType.Int32);
                    await itemCommand.ExecuteNonQueryAsync(cancellationToken);
                }

                await transaction.CommitAsync(cancellationToken);
                return true;
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static async Task<int?> ResolveOrderSellerIdAsync(
        DbConnection connection,
        int orderId,
        string orderIdColumn,
        string? orderSellerColumn,
        string? orderItemOrderIdColumn,
        string? orderItemSellerColumn,
        CancellationToken cancellationToken)
    {
        if (orderSellerColumn is not null)
        {
            await using var orderCommand = connection.CreateCommand();
            orderCommand.CommandText = $"SELECT TOP (1) {Quote(orderSellerColumn)} FROM dbo.Orders WHERE {Quote(orderIdColumn)} = @OrderId";
            AddParameter(orderCommand, "@OrderId", orderId, DbType.Int32);
            var value = await orderCommand.ExecuteScalarAsync(cancellationToken);
            if (value is not null && value != DBNull.Value && int.TryParse(Convert.ToString(value), out var parsed))
            {
                return parsed;
            }
        }

        if (orderItemOrderIdColumn is not null && orderItemSellerColumn is not null)
        {
            await using var itemCommand = connection.CreateCommand();
            itemCommand.CommandText = $"SELECT TOP (1) {Quote(orderItemSellerColumn)} FROM dbo.OrderItems WHERE {Quote(orderItemOrderIdColumn)} = @OrderId";
            AddParameter(itemCommand, "@OrderId", orderId, DbType.Int32);
            var value = await itemCommand.ExecuteScalarAsync(cancellationToken);
            if (value is not null && value != DBNull.Value && int.TryParse(Convert.ToString(value), out var parsed))
            {
                return parsed;
            }
        }

        return null;
    }

    private static async Task UpsertReturnRequestAsync(
        DbConnection connection,
        string tableName,
        IReadOnlySet<string> columns,
        int orderId,
        int? userId,
        int sellerId,
        string reason,
        string proofJson,
        CancellationToken cancellationToken)
    {
        var idCol = FindColumn(columns, "ReturnId", "return_id", "Id");
        var orderCol = FindColumn(columns, "OrderId", "order_id", "OrderID");
        var userCol = FindColumn(columns, "UserId", "user_id");
        var sellerCol = FindColumn(columns, "SellerId", "seller_id", "SellerID");
        var reasonCol = FindColumn(columns, "Reason", "reason", "ReturnReason");
        var messageCol = FindColumn(columns, "Message", "message", "ReturnMessage");
        var statusCol = FindColumn(columns, "Status", "status");
        var createdCol = FindColumn(columns, "CreatedAt", "created_at");

        if (orderCol is null || sellerCol is null)
        {
            return;
        }

        var assignments = new List<string>();
        if (reasonCol is not null)
        {
            assignments.Add($"{Quote(reasonCol)} = @Reason");
        }
        if (messageCol is not null)
        {
            assignments.Add($"{Quote(messageCol)} = @Message");
        }
        if (statusCol is not null)
        {
            assignments.Add($"{Quote(statusCol)} = @Status");
        }

        if (idCol is not null && assignments.Count > 0)
        {
            await using var updateCommand = connection.CreateCommand();
            updateCommand.CommandText =
                $"UPDATE {Quote(tableName)} SET {string.Join(", ", assignments)} WHERE {Quote(orderCol)} = @OrderId AND {Quote(sellerCol)} = @SellerId";
            AddParameter(updateCommand, "@Reason", reason, DbType.String);
            AddParameter(updateCommand, "@Message", proofJson, DbType.String);
            AddParameter(updateCommand, "@Status", "Return Requested", DbType.String);
            AddParameter(updateCommand, "@OrderId", orderId, DbType.Int32);
            AddParameter(updateCommand, "@SellerId", sellerId, DbType.Int32);

            if (await updateCommand.ExecuteNonQueryAsync(cancellationToken) > 0)
            {
                return;
            }
        }

        var insertColumns = new List<string> { Quote(orderCol), Quote(sellerCol) };
        var insertValues = new List<string> { "@OrderId", "@SellerId" };
        if (userCol is not null)
        {
            insertColumns.Add(Quote(userCol));
            insertValues.Add("@UserId");
        }
        if (reasonCol is not null)
        {
            insertColumns.Add(Quote(reasonCol));
            insertValues.Add("@Reason");
        }
        if (messageCol is not null)
        {
            insertColumns.Add(Quote(messageCol));
            insertValues.Add("@Message");
        }
        if (statusCol is not null)
        {
            insertColumns.Add(Quote(statusCol));
            insertValues.Add("@Status");
        }
        if (createdCol is not null)
        {
            insertColumns.Add(Quote(createdCol));
            insertValues.Add("SYSUTCDATETIME()");
        }

        await using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText =
            $"INSERT INTO {Quote(tableName)} ({string.Join(", ", insertColumns)}) VALUES ({string.Join(", ", insertValues)})";
        AddParameter(insertCommand, "@OrderId", orderId, DbType.Int32);
        AddParameter(insertCommand, "@SellerId", sellerId, DbType.Int32);
        AddParameter(insertCommand, "@UserId", userId, DbType.Int32);
        AddParameter(insertCommand, "@Reason", reason, DbType.String);
        AddParameter(insertCommand, "@Message", proofJson, DbType.String);
        AddParameter(insertCommand, "@Status", "Return Requested", DbType.String);
        await insertCommand.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string Quote(string identifier)
        => "[" + identifier.Replace("]", "]]") + "]";

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

    private static List<string> BuildOwnershipFilters(string? userIdCol, string? consumerIdCol)
    {
        var filters = new List<string>();
        if (userIdCol is not null)
        {
            filters.Add($"(@UserIdText IS NOT NULL AND LTRIM(RTRIM(CONVERT(NVARCHAR(50), [{userIdCol}]))) = @UserIdText)");
        }

        if (consumerIdCol is not null)
        {
            filters.Add($"(@ConsumerId IS NOT NULL AND [{consumerIdCol}] = @ConsumerId)");
        }

        return filters;
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
        var trimmed = (status ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            return "To Pay";
        }

        return trimmed.ToLowerInvariant() switch
        {
            "pending" => "To Pay",
            "placed" => "To Pay",
            "to pay" => "To Pay",
            "processing" => "To Ship",
            "to ship" => "To Ship",
            "shipped" => "To Receive",
            "to receive" => "To Receive",
            "delivered" => "To Review",
            "to review" => "To Review",
            "completed" => "To Review",
            "complete" => "To Review",
            "return requested" => "Returns",
            "return approved" => "Returns",
            "return rejected" => "Returns",
            "item returned" => "Returns",
            "refunded" => "Returns",
            "failed delivery" => "Returns",
            "returned" => "Returns",
            "return" => "Returns",
            "returns" => "Returns",
            "cancelled" => "Cancelled",
            "canceled" => "Cancelled",
            _ => trimmed
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
