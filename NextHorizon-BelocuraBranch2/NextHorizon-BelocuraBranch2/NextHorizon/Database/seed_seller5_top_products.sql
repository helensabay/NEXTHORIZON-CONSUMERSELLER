-- ============================================================
-- Seed: Top Performing Products for seller_id = 5 (Nike Cebu)
-- Creates 5 products + 25 orders + 25 order-item rows
-- spread across the last 30 days to exercise all range filters
-- ============================================================

BEGIN TRANSACTION;
BEGIN TRY

-- ── Step 1: Products ─────────────────────────────────────────────────────────
DECLARE @ProdIds TABLE (ProductId INT,
    SKU NVARCHAR(50));

INSERT INTO dbo.Products
    (ProductName, Price, Discount, Category, Details, ImagePath, Status, Brand,
    Stock, Colors, Sizes, Rating, ReviewCount, Gender, seller_id, SKU)
OUTPUT INSERTED.ProductId, INSERTED.SKU INTO @ProdIds(ProductId, SKU)
VALUES
    (N'Nike Air Max 270',
        5495.00, 0.00, N'Sneakers',
        N'Visible Air unit in the heel delivers all-day comfort with a bold, modern silhouette.',
        N'https://via.placeholder.com/400?text=Air+Max+270',
        N'Active', N'Nike', 50, N'Black,White,Blue', N'7,8,9,10,11', 4.8, 124, N'Unisex', 5, N'NH-NK-001'),

    (N'Nike Dri-FIT Training Shirt',
        1295.00, 0.00, N'Tops',
        N'Sweat-wicking Dri-FIT technology keeps you cool and dry through the toughest sessions.',
        N'https://via.placeholder.com/400?text=DRI-FIT+Shirt',
        N'Active', N'Nike', 80, N'Black,White,Red,Blue', N'S,M,L,XL,XXL', 4.6, 87, N'Unisex', 5, N'NH-NK-002'),

    (N'Nike Pro Training Shorts',
        1095.00, 0.00, N'Bottoms',
        N'Lightweight compression shorts with a snug fit designed to move with every rep.',
        N'https://via.placeholder.com/400?text=Pro+Shorts',
        N'Active', N'Nike', 60, N'Black,Grey,Navy', N'S,M,L,XL', 4.5, 63, N'Unisex', 5, N'NH-NK-003'),

    (N'Nike React Infinity Run FK 3',
        7295.00, 500.00, N'Sneakers',
        N'A wider, more stable design engineered to reduce injury and keep you running longer.',
        N'https://via.placeholder.com/400?text=React+Infinity',
        N'Active', N'Nike', 35, N'Black,White,Volt', N'6,7,8,9,10,11,12', 4.9, 56, N'Unisex', 5, N'NH-NK-004'),

    (N'Nike Sport Drawstring Bag',
        895.00, 0.00, N'Bags & Accessories',
        N'Durable, lightweight drawstring bag with a spacious main compartment for gym essentials.',
        N'https://via.placeholder.com/400?text=Sport+Bag',
        N'Active', N'Nike', 100, N'Black,Red,Blue', N'One Size', 4.4, 42, N'Unisex', 5, N'NH-NK-005');

-- Map SKUs to local variables for use in OrderItems
DECLARE @P1 INT = (SELECT ProductId
FROM @ProdIds
WHERE SKU = 'NH-NK-001'); -- Air Max 270       ₱5,495
DECLARE @P2 INT = (SELECT ProductId
FROM @ProdIds
WHERE SKU = 'NH-NK-002'); -- Dri-FIT Shirt     ₱1,295
DECLARE @P3 INT = (SELECT ProductId
FROM @ProdIds
WHERE SKU = 'NH-NK-003'); -- Training Shorts   ₱1,095
DECLARE @P4 INT = (SELECT ProductId
FROM @ProdIds
WHERE SKU = 'NH-NK-004'); -- React Infinity    ₱7,295
DECLARE @P5 INT = (SELECT ProductId
FROM @ProdIds
WHERE SKU = 'NH-NK-005'); -- Sport Bag         ₱895

-- ── Step 2+3: Orders + OrderItems ────────────────────────────────────────────
DECLARE @OID INT;

-- ─── Within last 1 HOUR ──────────────────────────────────────────────────────
INSERT INTO dbo.Orders
    (OrderDate, Quantity, Subtotal, ShippingFee, TotalAmount, Status,
    FullName, Email, PhoneNumber, StreetAddress, City, PostalCode,
    DeliveryOption, PaymentMethod, seller_id, ProductName)
VALUES
    (DATEADD(MINUTE,-15,GETUTCDATE()), 1, 5495.00, 100.00, 5595.00, 'Paid',
        N'Maria Santos', 'msantos@email.com', '+63 917 111 0001',
        N'123 Cebu Ave', N'Cebu City', '6000', 'Standard', 'GCash', 5, N'Nike Air Max 270');
SET @OID = SCOPE_IDENTITY();
INSERT INTO dbo.OrderItems
    (OrderID, ProductID, Quantity, UnitPrice, Size, Color, SellerId)
VALUES
    (@OID, @P1, 1, 5495.00, N'10', N'Black', 5);

INSERT INTO dbo.Orders
    (OrderDate, Quantity, Subtotal, ShippingFee, TotalAmount, Status,
    FullName, Email, PhoneNumber, StreetAddress, City, PostalCode,
    DeliveryOption, PaymentMethod, seller_id, ProductName)
VALUES
    (DATEADD(MINUTE,-40,GETUTCDATE()), 2, 2590.00, 100.00, 2690.00, 'Paid',
        N'Juan Dela Cruz', 'jdelacruz@email.com', '+63 917 111 0002',
        N'456 Mandaue St', N'Mandaue City', '6014', 'Standard', 'COD', 5, N'Nike Dri-FIT Training Shirt');
SET @OID = SCOPE_IDENTITY();
INSERT INTO dbo.OrderItems
    (OrderID, ProductID, Quantity, UnitPrice, Size, Color, SellerId)
VALUES
    (@OID, @P2, 2, 1295.00, N'L', N'Black', 5);

-- ─── Within last 1 DAY (2 h – 23 h ago) ─────────────────────────────────────
INSERT INTO dbo.Orders
    (OrderDate, Quantity, Subtotal, ShippingFee, TotalAmount, Status,
    FullName, Email, PhoneNumber, StreetAddress, City, PostalCode,
    DeliveryOption, PaymentMethod, seller_id, ProductName)
VALUES
    (DATEADD(HOUR,-3,GETUTCDATE()), 1, 7295.00, 150.00, 7445.00, 'Paid',
        N'Ana Reyes', 'areyes@email.com', '+63 917 111 0003',
        N'789 Lapu-Lapu Blvd', N'Lapu-Lapu City', '6015', 'Express', 'Credit Card', 5, N'Nike React Infinity Run FK 3');
SET @OID = SCOPE_IDENTITY();
INSERT INTO dbo.OrderItems
    (OrderID, ProductID, Quantity, UnitPrice, Size, Color, SellerId)
VALUES
    (@OID, @P4, 1, 7295.00, N'9', N'Black', 5);

INSERT INTO dbo.Orders
    (OrderDate, Quantity, Subtotal, ShippingFee, TotalAmount, Status,
    FullName, Email, PhoneNumber, StreetAddress, City, PostalCode,
    DeliveryOption, PaymentMethod, seller_id, ProductName)
VALUES
    (DATEADD(HOUR,-6,GETUTCDATE()), 3, 3285.00, 85.00, 3370.00, 'Delivered',
        N'Carlo Mercado', 'cmercado@email.com', '+63 917 111 0004',
        N'321 Talisay Rd', N'Talisay City', '6045', 'Standard', 'GCash', 5, N'Nike Pro Training Shorts');
SET @OID = SCOPE_IDENTITY();
INSERT INTO dbo.OrderItems
    (OrderID, ProductID, Quantity, UnitPrice, Size, Color, SellerId)
VALUES
    (@OID, @P3, 3, 1095.00, N'M', N'Black', 5);

INSERT INTO dbo.Orders
    (OrderDate, Quantity, Subtotal, ShippingFee, TotalAmount, Status,
    FullName, Email, PhoneNumber, StreetAddress, City, PostalCode,
    DeliveryOption, PaymentMethod, seller_id, ProductName)
VALUES
    (DATEADD(HOUR,-10,GETUTCDATE()), 1, 895.00, 85.00, 980.00, 'Completed',
        N'Liza Fernandez', 'lfernandez@email.com', '+63 917 111 0005',
        N'555 Minglanilla St', N'Minglanilla', '6046', 'Standard', 'COD', 5, N'Nike Sport Drawstring Bag');
SET @OID = SCOPE_IDENTITY();
INSERT INTO dbo.OrderItems
    (OrderID, ProductID, Quantity, UnitPrice, Size, Color, SellerId)
VALUES
    (@OID, @P5, 1, 895.00, N'One Size', N'Black', 5);

INSERT INTO dbo.Orders
    (OrderDate, Quantity, Subtotal, ShippingFee, TotalAmount, Status,
    FullName, Email, PhoneNumber, StreetAddress, City, PostalCode,
    DeliveryOption, PaymentMethod, seller_id, ProductName)
VALUES
    (DATEADD(HOUR,-18,GETUTCDATE()), 2, 10990.00, 150.00, 11140.00, 'Paid',
        N'Ramon Villanueva', 'rvillanueva@email.com', '+63 917 111 0006',
        N'12 Consolacion Ave', N'Consolacion', '6001', 'Express', 'GCash', 5, N'Nike Air Max 270');
SET @OID = SCOPE_IDENTITY();
INSERT INTO dbo.OrderItems
    (OrderID, ProductID, Quantity, UnitPrice, Size, Color, SellerId)
VALUES
    (@OID, @P1, 2, 5495.00, N'11', N'White', 5);

-- ─── Within last 7 DAYS (2 – 6 days ago) ────────────────────────────────────
INSERT INTO dbo.Orders
    (OrderDate, Quantity, Subtotal, ShippingFee, TotalAmount, Status,
    FullName, Email, PhoneNumber, StreetAddress, City, PostalCode,
    DeliveryOption, PaymentMethod, seller_id, ProductName)
VALUES
    (DATEADD(DAY,-2,GETUTCDATE()), 1, 1295.00, 85.00, 1380.00, 'Completed',
        N'Grace Uy', 'guy@email.com', '+63 917 111 0007',
        N'88 Labangon St', N'Cebu City', '6000', 'Standard', 'COD', 5, N'Nike Dri-FIT Training Shirt');
SET @OID = SCOPE_IDENTITY();
INSERT INTO dbo.OrderItems
    (OrderID, ProductID, Quantity, UnitPrice, Size, Color, SellerId)
VALUES
    (@OID, @P2, 1, 1295.00, N'M', N'White', 5);

INSERT INTO dbo.Orders
    (OrderDate, Quantity, Subtotal, ShippingFee, TotalAmount, Status,
    FullName, Email, PhoneNumber, StreetAddress, City, PostalCode,
    DeliveryOption, PaymentMethod, seller_id, ProductName)
VALUES
    (DATEADD(DAY,-2,GETUTCDATE()), 2, 14590.00, 150.00, 14740.00, 'Delivered',
        N'Paolo Abad', 'pabad@email.com', '+63 917 111 0008',
        N'9 Banilad Rd', N'Cebu City', '6000', 'Express', 'Credit Card', 5, N'Nike React Infinity Run FK 3');
SET @OID = SCOPE_IDENTITY();
INSERT INTO dbo.OrderItems
    (OrderID, ProductID, Quantity, UnitPrice, Size, Color, SellerId)
VALUES
    (@OID, @P4, 2, 7295.00, N'8', N'White', 5);

INSERT INTO dbo.Orders
    (OrderDate, Quantity, Subtotal, ShippingFee, TotalAmount, Status,
    FullName, Email, PhoneNumber, StreetAddress, City, PostalCode,
    DeliveryOption, PaymentMethod, seller_id, ProductName)
VALUES
    (DATEADD(DAY,-3,GETUTCDATE()), 3, 3885.00, 85.00, 3970.00, 'Completed',
        N'Sofia Lim', 'slim@email.com', '+63 917 111 0009',
        N'23 Talamban Rd', N'Cebu City', '6000', 'Standard', 'GCash', 5, N'Nike Dri-FIT Training Shirt');
SET @OID = SCOPE_IDENTITY();
INSERT INTO dbo.OrderItems
    (OrderID, ProductID, Quantity, UnitPrice, Size, Color, SellerId)
VALUES
    (@OID, @P2, 3, 1295.00, N'L', N'Red', 5);

INSERT INTO dbo.Orders
    (OrderDate, Quantity, Subtotal, ShippingFee, TotalAmount, Status,
    FullName, Email, PhoneNumber, StreetAddress, City, PostalCode,
    DeliveryOption, PaymentMethod, seller_id, ProductName)
VALUES
    (DATEADD(DAY,-3,GETUTCDATE()), 1, 5495.00, 100.00, 5595.00, 'Delivered',
        N'Miguel Torres', 'mtorres@email.com', '+63 917 111 0010',
        N'7 Cabancalan', N'Mandaue City', '6014', 'Standard', 'COD', 5, N'Nike Air Max 270');
SET @OID = SCOPE_IDENTITY();
INSERT INTO dbo.OrderItems
    (OrderID, ProductID, Quantity, UnitPrice, Size, Color, SellerId)
VALUES
    (@OID, @P1, 1, 5495.00, N'9', N'Blue', 5);

INSERT INTO dbo.Orders
    (OrderDate, Quantity, Subtotal, ShippingFee, TotalAmount, Status,
    FullName, Email, PhoneNumber, StreetAddress, City, PostalCode,
    DeliveryOption, PaymentMethod, seller_id, ProductName)
VALUES
    (DATEADD(DAY,-4,GETUTCDATE()), 2, 1790.00, 85.00, 1875.00, 'Paid',
        N'Elena Cruz', 'ecruz@email.com', '+63 917 111 0011',
        N'45 Basak', N'Lapu-Lapu City', '6015', 'Standard', 'GCash', 5, N'Nike Sport Drawstring Bag');
SET @OID = SCOPE_IDENTITY();
INSERT INTO dbo.OrderItems
    (OrderID, ProductID, Quantity, UnitPrice, Size, Color, SellerId)
VALUES
    (@OID, @P5, 2, 895.00, N'One Size', N'Red', 5);

INSERT INTO dbo.Orders
    (OrderDate, Quantity, Subtotal, ShippingFee, TotalAmount, Status,
    FullName, Email, PhoneNumber, StreetAddress, City, PostalCode,
    DeliveryOption, PaymentMethod, seller_id, ProductName)
VALUES
    (DATEADD(DAY,-5,GETUTCDATE()), 4, 4380.00, 85.00, 4465.00, 'Completed',
        N'Ben Espinosa', 'bespinosa@email.com', '+63 917 111 0012',
        N'66 Naga Rd', N'Naga City', '6036', 'Standard', 'COD', 5, N'Nike Pro Training Shorts');
SET @OID = SCOPE_IDENTITY();
INSERT INTO dbo.OrderItems
    (OrderID, ProductID, Quantity, UnitPrice, Size, Color, SellerId)
VALUES
    (@OID, @P3, 4, 1095.00, N'L', N'Grey', 5);

INSERT INTO dbo.Orders
    (OrderDate, Quantity, Subtotal, ShippingFee, TotalAmount, Status,
    FullName, Email, PhoneNumber, StreetAddress, City, PostalCode,
    DeliveryOption, PaymentMethod, seller_id, ProductName)
VALUES
    (DATEADD(DAY,-6,GETUTCDATE()), 1, 7295.00, 150.00, 7445.00, 'Delivered',
        N'Rica Gomez', 'rgomez@email.com', '+63 917 111 0013',
        N'11 Mactan', N'Lapu-Lapu City', '6015', 'Express', 'Credit Card', 5, N'Nike React Infinity Run FK 3');
SET @OID = SCOPE_IDENTITY();
INSERT INTO dbo.OrderItems
    (OrderID, ProductID, Quantity, UnitPrice, Size, Color, SellerId)
VALUES
    (@OID, @P4, 1, 7295.00, N'10', N'Volt', 5);

INSERT INTO dbo.Orders
    (OrderDate, Quantity, Subtotal, ShippingFee, TotalAmount, Status,
    FullName, Email, PhoneNumber, StreetAddress, City, PostalCode,
    DeliveryOption, PaymentMethod, seller_id, ProductName)
VALUES
    (DATEADD(DAY,-6,GETUTCDATE()), 2, 2590.00, 85.00, 2675.00, 'Paid',
        N'Dante Bautista', 'dbautista@email.com', '+63 917 111 0014',
        N'34 Liloan', N'Liloan', '6002', 'Standard', 'GCash', 5, N'Nike Dri-FIT Training Shirt');
SET @OID = SCOPE_IDENTITY();
INSERT INTO dbo.OrderItems
    (OrderID, ProductID, Quantity, UnitPrice, Size, Color, SellerId)
VALUES
    (@OID, @P2, 2, 1295.00, N'XL', N'Blue', 5);

-- ─── Within last 30 DAYS (8 – 30 days ago) ───────────────────────────────────
INSERT INTO dbo.Orders
    (OrderDate, Quantity, Subtotal, ShippingFee, TotalAmount, Status,
    FullName, Email, PhoneNumber, StreetAddress, City, PostalCode,
    DeliveryOption, PaymentMethod, seller_id, ProductName)
VALUES
    (DATEADD(DAY,-8,GETUTCDATE()), 3, 16485.00, 150.00, 16635.00, 'Delivered',
        N'Jenny Lao', 'jlao@email.com', '+63 917 111 0015',
        N'56 Compostela', N'Compostela', '6003', 'Express', 'Credit Card', 5, N'Nike Air Max 270');
SET @OID = SCOPE_IDENTITY();
INSERT INTO dbo.OrderItems
    (OrderID, ProductID, Quantity, UnitPrice, Size, Color, SellerId)
VALUES
    (@OID, @P1, 3, 5495.00, N'8', N'White', 5);

INSERT INTO dbo.Orders
    (OrderDate, Quantity, Subtotal, ShippingFee, TotalAmount, Status,
    FullName, Email, PhoneNumber, StreetAddress, City, PostalCode,
    DeliveryOption, PaymentMethod, seller_id, ProductName)
VALUES
    (DATEADD(DAY,-10,GETUTCDATE()), 2, 2190.00, 85.00, 2275.00, 'Completed',
        N'Mark Sy', 'msy@email.com', '+63 917 111 0016',
        N'78 Carcar Rd', N'Carcar City', '6019', 'Standard', 'COD', 5, N'Nike Pro Training Shorts');
SET @OID = SCOPE_IDENTITY();
INSERT INTO dbo.OrderItems
    (OrderID, ProductID, Quantity, UnitPrice, Size, Color, SellerId)
VALUES
    (@OID, @P3, 2, 1095.00, N'S', N'Navy', 5);

INSERT INTO dbo.Orders
    (OrderDate, Quantity, Subtotal, ShippingFee, TotalAmount, Status,
    FullName, Email, PhoneNumber, StreetAddress, City, PostalCode,
    DeliveryOption, PaymentMethod, seller_id, ProductName)
VALUES
    (DATEADD(DAY,-12,GETUTCDATE()), 1, 5495.00, 100.00, 5595.00, 'Delivered',
        N'Ana Ramos', 'aramos@email.com', '+63 917 111 0017',
        N'90 Alcantara', N'Alcantara', '6038', 'Standard', 'GCash', 5, N'Nike Air Max 270');
SET @OID = SCOPE_IDENTITY();
INSERT INTO dbo.OrderItems
    (OrderID, ProductID, Quantity, UnitPrice, Size, Color, SellerId)
VALUES
    (@OID, @P1, 1, 5495.00, N'7', N'Black', 5);

INSERT INTO dbo.Orders
    (OrderDate, Quantity, Subtotal, ShippingFee, TotalAmount, Status,
    FullName, Email, PhoneNumber, StreetAddress, City, PostalCode,
    DeliveryOption, PaymentMethod, seller_id, ProductName)
VALUES
    (DATEADD(DAY,-14,GETUTCDATE()), 5, 6475.00, 85.00, 6560.00, 'Completed',
        N'Leo Chua', 'lchua@email.com', '+63 917 111 0018',
        N'12 Danao City', N'Danao', '6004', 'Standard', 'COD', 5, N'Nike Dri-FIT Training Shirt');
SET @OID = SCOPE_IDENTITY();
INSERT INTO dbo.OrderItems
    (OrderID, ProductID, Quantity, UnitPrice, Size, Color, SellerId)
VALUES
    (@OID, @P2, 5, 1295.00, N'S', N'Black', 5);

INSERT INTO dbo.Orders
    (OrderDate, Quantity, Subtotal, ShippingFee, TotalAmount, Status,
    FullName, Email, PhoneNumber, StreetAddress, City, PostalCode,
    DeliveryOption, PaymentMethod, seller_id, ProductName)
VALUES
    (DATEADD(DAY,-16,GETUTCDATE()), 1, 7295.00, 150.00, 7445.00, 'Delivered',
        N'Coco Martin', 'cmartin@email.com', '+63 917 111 0019',
        N'45 Bogo City', N'Bogo', '6010', 'Express', 'Credit Card', 5, N'Nike React Infinity Run FK 3');
SET @OID = SCOPE_IDENTITY();
INSERT INTO dbo.OrderItems
    (OrderID, ProductID, Quantity, UnitPrice, Size, Color, SellerId)
VALUES
    (@OID, @P4, 1, 7295.00, N'11', N'Black', 5);

INSERT INTO dbo.Orders
    (OrderDate, Quantity, Subtotal, ShippingFee, TotalAmount, Status,
    FullName, Email, PhoneNumber, StreetAddress, City, PostalCode,
    DeliveryOption, PaymentMethod, seller_id, ProductName)
VALUES
    (DATEADD(DAY,-18,GETUTCDATE()), 4, 3580.00, 85.00, 3665.00, 'Completed',
        N'Tina Pascual', 'tpascual@email.com', '+63 917 111 0020',
        N'23 Cebu City', N'Cebu City', '6000', 'Standard', 'GCash', 5, N'Nike Sport Drawstring Bag');
SET @OID = SCOPE_IDENTITY();
INSERT INTO dbo.OrderItems
    (OrderID, ProductID, Quantity, UnitPrice, Size, Color, SellerId)
VALUES
    (@OID, @P5, 4, 895.00, N'One Size', N'Blue', 5);

INSERT INTO dbo.Orders
    (OrderDate, Quantity, Subtotal, ShippingFee, TotalAmount, Status,
    FullName, Email, PhoneNumber, StreetAddress, City, PostalCode,
    DeliveryOption, PaymentMethod, seller_id, ProductName)
VALUES
    (DATEADD(DAY,-20,GETUTCDATE()), 2, 10990.00, 150.00, 11140.00, 'Delivered',
        N'Rico Blanco', 'rblanco@email.com', '+63 917 111 0021',
        N'77 Mandaue', N'Mandaue City', '6014', 'Express', 'Credit Card', 5, N'Nike Air Max 270');
SET @OID = SCOPE_IDENTITY();
INSERT INTO dbo.OrderItems
    (OrderID, ProductID, Quantity, UnitPrice, Size, Color, SellerId)
VALUES
    (@OID, @P1, 2, 5495.00, N'10', N'Blue', 5);

INSERT INTO dbo.Orders
    (OrderDate, Quantity, Subtotal, ShippingFee, TotalAmount, Status,
    FullName, Email, PhoneNumber, StreetAddress, City, PostalCode,
    DeliveryOption, PaymentMethod, seller_id, ProductName)
VALUES
    (DATEADD(DAY,-22,GETUTCDATE()), 3, 3885.00, 85.00, 3970.00, 'Completed',
        N'Ivy Lin', 'ilin@email.com', '+63 917 111 0022',
        N'33 Talisay', N'Talisay City', '6045', 'Standard', 'COD', 5, N'Nike Dri-FIT Training Shirt');
SET @OID = SCOPE_IDENTITY();
INSERT INTO dbo.OrderItems
    (OrderID, ProductID, Quantity, UnitPrice, Size, Color, SellerId)
VALUES
    (@OID, @P2, 3, 1295.00, N'M', N'White', 5);

INSERT INTO dbo.Orders
    (OrderDate, Quantity, Subtotal, ShippingFee, TotalAmount, Status,
    FullName, Email, PhoneNumber, StreetAddress, City, PostalCode,
    DeliveryOption, PaymentMethod, seller_id, ProductName)
VALUES
    (DATEADD(DAY,-25,GETUTCDATE()), 2, 2190.00, 85.00, 2275.00, 'Completed',
        N'Eddie Reyes', 'ereyes@email.com', '+63 917 111 0023',
        N'15 Compostela', N'Compostela', '6003', 'Standard', 'GCash', 5, N'Nike Pro Training Shorts');
SET @OID = SCOPE_IDENTITY();
INSERT INTO dbo.OrderItems
    (OrderID, ProductID, Quantity, UnitPrice, Size, Color, SellerId)
VALUES
    (@OID, @P3, 2, 1095.00, N'XL', N'Black', 5);

INSERT INTO dbo.Orders
    (OrderDate, Quantity, Subtotal, ShippingFee, TotalAmount, Status,
    FullName, Email, PhoneNumber, StreetAddress, City, PostalCode,
    DeliveryOption, PaymentMethod, seller_id, ProductName)
VALUES
    (DATEADD(DAY,-28,GETUTCDATE()), 1, 895.00, 85.00, 980.00, 'Delivered',
        N'Ruth Navarro', 'rnavarro@email.com', '+63 917 111 0024',
        N'8 Minglanilla', N'Minglanilla', '6046', 'Standard', 'COD', 5, N'Nike Sport Drawstring Bag');
SET @OID = SCOPE_IDENTITY();
INSERT INTO dbo.OrderItems
    (OrderID, ProductID, Quantity, UnitPrice, Size, Color, SellerId)
VALUES
    (@OID, @P5, 1, 895.00, N'One Size', N'Red', 5);

INSERT INTO dbo.Orders
    (OrderDate, Quantity, Subtotal, ShippingFee, TotalAmount, Status,
    FullName, Email, PhoneNumber, StreetAddress, City, PostalCode,
    DeliveryOption, PaymentMethod, seller_id, ProductName)
VALUES
    (DATEADD(DAY,-30,GETUTCDATE()), 1, 7295.00, 150.00, 7445.00, 'Completed',
        N'Oliver Tan', 'otan@email.com', '+63 917 111 0025',
        N'55 Carcar', N'Carcar City', '6019', 'Express', 'Credit Card', 5, N'Nike React Infinity Run FK 3');
SET @OID = SCOPE_IDENTITY();
INSERT INTO dbo.OrderItems
    (OrderID, ProductID, Quantity, UnitPrice, Size, Color, SellerId)
VALUES
    (@OID, @P4, 1, 7295.00, N'9', N'White', 5);

-- ── Verification ─────────────────────────────────────────────────────────────
    SELECT 'Products inserted'    AS [Step], COUNT(*) AS [Count]
    FROM dbo.Products
    WHERE seller_id = 5
UNION ALL
    SELECT 'Orders inserted', COUNT(*)
    FROM dbo.Orders
    WHERE seller_id = 5 AND Status <> 'Cancelled'
UNION ALL
    SELECT 'OrderItems inserted', COUNT(*)
    FROM dbo.OrderItems
    WHERE SellerId = 5;

-- Top-products preview (mirrors app query)
SELECT TOP 5
    p.ProductName,
    SUM(oi.Quantity)                          AS UnitsSold,
    SUM(oi.Quantity * oi.UnitPrice)           AS Revenue
FROM dbo.Orders o
    INNER JOIN dbo.OrderItems oi ON oi.OrderID = o.OrderID
    LEFT JOIN dbo.Products   p ON p.ProductId = oi.ProductID
WHERE o.seller_id = 5
    AND ISNULL(o.Status,'') <> 'Cancelled'
GROUP BY p.ProductName
ORDER BY UnitsSold DESC, Revenue DESC;

COMMIT TRANSACTION;
PRINT 'Seed completed successfully.';

END TRY
BEGIN CATCH
    ROLLBACK TRANSACTION;
    PRINT 'Seed FAILED – transaction rolled back.';
    THROW;
END CATCH;
