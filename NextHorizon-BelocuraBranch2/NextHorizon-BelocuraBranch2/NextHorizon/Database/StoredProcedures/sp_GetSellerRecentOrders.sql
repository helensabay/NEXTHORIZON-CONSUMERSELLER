CREATE OR ALTER PROCEDURE dbo.sp_GetSellerRecentOrders
    @SellerId INT,
    @Top INT = 10
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@Top)
        o.OrderID                    AS OrderId,
        o.FullName                   AS Customer,
        o.ProductName,
        ISNULL(img.ProductImage, '') AS ProductImage,
        ISNULL(item.Size, '')        AS Size,
        ISNULL(img.Sku, '')          AS Sku,
        o.OrderDate                  AS DateTime,
        ISNULL(o.DeliveryOption, '') AS Courier,
        o.Status,
        o.TotalAmount
    FROM dbo.Orders o
    OUTER APPLY (
        SELECT TOP (1)
            oi.OrderItemID,
            oi.ProductID,
            oi.Size,
            oi.Color,
            oi.VariantId
        FROM dbo.OrderItems oi
        WHERE oi.OrderID = o.OrderID
        ORDER BY oi.OrderItemID
    ) item
    OUTER APPLY (
        SELECT TOP (1)
            pv.SKU AS Sku,
            CASE
                WHEN pv.VariantId IS NULL THEN ''
                WHEN pv.ImageData IS NOT NULL OR NULLIF(LTRIM(RTRIM(pv.imagePath)), '') IS NOT NULL
                    THEN CONCAT('/ProductImage/Variant/', pv.VariantId)
                ELSE ''
            END AS ProductImage
        FROM dbo.ProductVariants pv
        WHERE pv.ProductId = item.ProductID
          AND (
                (item.VariantId IS NOT NULL AND pv.VariantId = item.VariantId)
                OR (
                    item.VariantId IS NULL
                    AND (item.Size IS NULL OR item.Size = '' OR pv.Size = item.Size)
                    AND (item.Color IS NULL OR item.Color = '' OR pv.Style = item.Color)
                )
          )
        ORDER BY CASE WHEN item.VariantId IS NOT NULL AND pv.VariantId = item.VariantId THEN 0 ELSE 1 END,
                 pv.VariantId
    ) img
    WHERE o.seller_id = @SellerId
    ORDER BY o.OrderDate DESC, o.OrderID DESC;
END;
GO

