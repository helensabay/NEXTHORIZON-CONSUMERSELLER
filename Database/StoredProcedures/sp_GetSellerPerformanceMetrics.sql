CREATE OR ALTER PROCEDURE dbo.sp_GetSellerPerformanceMetrics
    @SellerId INT,
    @Today DATE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TodayStart DATETIME2 = CAST(@Today AS DATETIME2);
    DECLARE @TomorrowStart DATETIME2 = DATEADD(DAY, 1, @TodayStart);
    DECLARE @YesterdayStart DATETIME2 = DATEADD(DAY, -1, @TodayStart);

    ;WITH SellerOrders AS
    (
        SELECT
            CAST(ISNULL(o.TotalAmount, ISNULL(o.Subtotal, 0) + ISNULL(o.ShippingFee, 0)) AS DECIMAL(18,2)) AS TotalAmount,
            CAST(o.OrderDate AS DATETIME2) AS OrderDate,
            CAST(ISNULL(o.Quantity, 0) AS INT) AS Quantity,
            LTRIM(RTRIM(ISNULL(o.Status, ''))) AS Status,
            LTRIM(RTRIM(ISNULL(o.PaymentMethod, ''))) AS PaymentMethod
        FROM dbo.Orders o
        WHERE o.SellerId = @SellerId
    ),
    RecognizedOrders AS
    (
        SELECT *
        FROM SellerOrders
        WHERE Status IN ('Completed', 'Delivered', 'Complete')
    ),
    CodOrders AS
    (
        SELECT *
        FROM SellerOrders
        WHERE UPPER(PaymentMethod) IN ('COD', 'CASH ON DELIVERY')
    )
    SELECT
        CAST(ISNULL(SUM(CASE WHEN ro.OrderDate >= @TodayStart AND ro.OrderDate < @TomorrowStart THEN ro.TotalAmount ELSE 0 END), 0) AS DECIMAL(18,2)) AS TodaySales,
        CAST(ISNULL(SUM(CASE WHEN ro.OrderDate >= @YesterdayStart AND ro.OrderDate < @TodayStart THEN ro.TotalAmount ELSE 0 END), 0) AS DECIMAL(18,2)) AS YesterdaySales,
        CAST(ISNULL(SUM(ro.Quantity), 0) AS INT) AS TotalUnitsSold,
        CAST(COUNT(ro.OrderDate) AS INT) AS RecognizedOrders,
        CAST(ISNULL(SUM(ro.TotalAmount), 0) AS DECIMAL(18,2)) AS TotalRevenue,
        CAST(CASE
            WHEN ISNULL(SUM(CASE WHEN ro.OrderDate >= @YesterdayStart AND ro.OrderDate < @TodayStart THEN ro.TotalAmount ELSE 0 END), 0) = 0
                THEN 0
            ELSE
                ((ISNULL(SUM(CASE WHEN ro.OrderDate >= @TodayStart AND ro.OrderDate < @TomorrowStart THEN ro.TotalAmount ELSE 0 END), 0)
                  - ISNULL(SUM(CASE WHEN ro.OrderDate >= @YesterdayStart AND ro.OrderDate < @TodayStart THEN ro.TotalAmount ELSE 0 END), 0))
                  * 100.0)
                / NULLIF(ISNULL(SUM(CASE WHEN ro.OrderDate >= @YesterdayStart AND ro.OrderDate < @TodayStart THEN ro.TotalAmount ELSE 0 END), 0), 0)
        END AS DECIMAL(18,2)) AS SalesGrowth,
        CAST((SELECT ISNULL(SUM(co.TotalAmount), 0) FROM CodOrders co WHERE co.Status IN ('Completed', 'Delivered', 'Complete')) AS DECIMAL(18,2)) AS CodDeliveredRevenue,
        CAST((SELECT ISNULL(SUM(co.TotalAmount), 0) FROM CodOrders co WHERE co.Status IN ('To Ship', 'Shipped')) AS DECIMAL(18,2)) AS CodExposure,
        CAST((SELECT COUNT(1) FROM CodOrders co WHERE co.Status = 'Failed Delivery') AS INT) AS CodRtsCount,
        CAST(CASE
            WHEN (SELECT COUNT(1) FROM CodOrders) = 0 THEN 0
            ELSE ((SELECT COUNT(1) FROM CodOrders co WHERE co.Status IN ('Completed', 'Delivered', 'Complete')) * 100.0)
                / NULLIF((SELECT COUNT(1) FROM CodOrders), 0)
        END AS DECIMAL(18,2)) AS CodSuccessRate
    FROM RecognizedOrders ro;
END;

GO
