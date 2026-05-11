-- ============================================================
-- Stored Procedure: sp_SearchSellerProducts
-- Description: Searches a seller's active products by
--              ProductName, ProductId, or SKU (from ProductVariants)
-- Used in: PromotionsController.SearchProducts (AJAX endpoint)
--          Called from AddPromotion.cshtml product search bar
-- ============================================================

CREATE OR ALTER PROCEDURE sp_SearchSellerProducts
    @SellerId INT,
    @Search   NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT DISTINCT
        p.ProductId,
        p.ProductName,
        p.Price,
        p.Category,
        MIN(v.SKU)       AS SKU,       -- First SKU from ProductVariants
        MIN(v.imagePath) AS ImagePath  -- First image from ProductVariants
    FROM dbo.Products p
    LEFT JOIN dbo.ProductVariants v ON p.ProductId = v.ProductId
    WHERE
        p.seller_id = @SellerId
        AND p.Status = 'active'
        AND (
            @Search IS NULL OR @Search = ''
            OR p.ProductName                    LIKE '%' + @Search + '%'  -- Search by name
            OR CAST(p.ProductId AS NVARCHAR)    LIKE '%' + @Search + '%'  -- Search by ID
            OR v.SKU                            LIKE '%' + @Search + '%'  -- Search by SKU (from variants)
            OR p.Category                       LIKE '%' + @Search + '%'  -- Search by category
        )
    GROUP BY
        p.ProductId,
        p.ProductName,
        p.Price,
        p.Category
    ORDER BY
        p.ProductName;
END
