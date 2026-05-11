CREATE OR ALTER PROCEDURE dbo.sp_GetSellerPerformanceMetrics
    @SellerId INT,
    @Today DATE
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @TodayStart DATETIME2 = CAST(@Today AS DATETIME2);
    DECLARE @TomorrowStart DATETIME2 = DATEADD(DAY, 1, @TodayStart);
    DECLARE @YesterdayStart DATETIME2 = DATEADD(DAY, -1, @TodayStart);
    DECLARE @SellerColumn SYSNAME =
    (
        SELECT TOP (1)
        c.name
    FROM sys.columns c
    WHERE c.object_id = OBJECT_ID(N'dbo.Orders')
        AND REPLACE(LOWER(c.name), N'_', N'') = N'sellerid'
    );

    DECLARE @DateColumn SYSNAME =
    (
        SELECT TOP (1)
        c.name
    FROM sys.columns c
    WHERE c.object_id = OBJECT_ID(N'dbo.Orders')
        AND REPLACE(LOWER(c.name), N'_', N'') IN (N'orderdate', N'createdutc', N'createdat')
    );

    DECLARE @AmountColumn SYSNAME =
    (
        SELECT TOP (1)
        c.name
    FROM sys.columns c
    WHERE c.object_id = OBJECT_ID(N'dbo.Orders')
        AND REPLACE(LOWER(c.name), N'_', N'') IN (N'totalamount', N'amount')
    );

    DECLARE @StatusColumn SYSNAME =
    (
        SELECT TOP (1)
        c.name
    FROM sys.columns c
    WHERE c.object_id = OBJECT_ID(N'dbo.Orders')
        AND REPLACE(LOWER(c.name), N'_', N'') IN (N'status', N'orderstatus')
    );

    DECLARE @QuantityColumn SYSNAME =
    (
        SELECT TOP (1)
        c.name
    FROM sys.columns c
    WHERE c.object_id = OBJECT_ID(N'dbo.Orders')
        AND REPLACE(LOWER(c.name), N'_', N'') IN (N'quantity', N'qty', N'unitsold', N'totalqty')
    );

    IF @DateColumn IS NULL OR @AmountColumn IS NULL
    BEGIN
        SELECT
            CAST(0 AS DECIMAL(18,2)) AS TodaySales,
            CAST(0 AS DECIMAL(18,2)) AS YesterdaySales,
            CAST(0 AS INT) AS TotalUnitsSold,
            CAST(0 AS INT) AS RecognizedOrders,
            CAST(0 AS DECIMAL(18,2)) AS TotalRevenue,
            CAST(0 AS DECIMAL(18,2)) AS SalesGrowth;
        RETURN;
    END;

    DECLARE @Sql NVARCHAR(MAX) = N'
;WITH SellerOrders AS
(
    SELECT
        CAST(o.' + QUOTENAME(@AmountColumn) + N' AS DECIMAL(18,2)) AS TotalAmount,
        o.' + QUOTENAME(@DateColumn) + N' AS OrderDate,
        ' + CASE WHEN @QuantityColumn IS NULL
            THEN N'CAST(0 AS INT)'
            ELSE N'CAST(o.' + QUOTENAME(@QuantityColumn) + N' AS INT)'
          END + N' AS Quantity
    FROM dbo.Orders o
    WHERE 1 = 1 ';

    IF @SellerColumn IS NOT NULL
    BEGIN
        SET @Sql += N' AND o.' + QUOTENAME(@SellerColumn) + N' = @SellerId';
    END;

    IF @StatusColumn IS NOT NULL
    BEGIN
        SET @Sql += N' AND o.' + QUOTENAME(@StatusColumn) + N' IN (''Completed'', ''Delivered'')';
    END;

    SET @Sql += N'
)
SELECT
    CAST(ISNULL(SUM(CASE WHEN so.OrderDate >= @TodayStart AND so.OrderDate < @TomorrowStart THEN so.TotalAmount ELSE 0 END), 0) AS DECIMAL(18,2)) AS TodaySales,
    CAST(ISNULL(SUM(CASE WHEN so.OrderDate >= @YesterdayStart AND so.OrderDate < @TodayStart THEN so.TotalAmount ELSE 0 END), 0) AS DECIMAL(18,2)) AS YesterdaySales,
    CAST(ISNULL(SUM(so.Quantity), 0) AS INT) AS TotalUnitsSold,
    CAST(COUNT(*) AS INT) AS RecognizedOrders,
    CAST(ISNULL(SUM(so.TotalAmount), 0) AS DECIMAL(18,2)) AS TotalRevenue,
    CAST(CASE
        WHEN ISNULL(SUM(CASE WHEN so.OrderDate >= @YesterdayStart AND so.OrderDate < @TodayStart THEN so.TotalAmount ELSE 0 END), 0) = 0
            THEN 0
        ELSE
            ((ISNULL(SUM(CASE WHEN so.OrderDate >= @TodayStart AND so.OrderDate < @TomorrowStart THEN so.TotalAmount ELSE 0 END), 0)
              - ISNULL(SUM(CASE WHEN so.OrderDate >= @YesterdayStart AND so.OrderDate < @TodayStart THEN so.TotalAmount ELSE 0 END), 0))
              * 100.0)
            / NULLIF(ISNULL(SUM(CASE WHEN so.OrderDate >= @YesterdayStart AND so.OrderDate < @TodayStart THEN so.TotalAmount ELSE 0 END), 0), 0)
    END AS DECIMAL(18,2)) AS SalesGrowth
FROM SellerOrders so;';

    EXEC sp_executesql
        @Sql,
        N'@SellerId INT, @TodayStart DATETIME2, @TomorrowStart DATETIME2, @YesterdayStart DATETIME2',
        @SellerId = @SellerId,
        @TodayStart = @TodayStart,
        @TomorrowStart = @TomorrowStart,
        @YesterdayStart = @YesterdayStart;
END;

GO
