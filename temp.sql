IF OBJECT_ID(N'[__EFMigrationsHistory]') IS NULL
BEGIN
    CREATE TABLE [__EFMigrationsHistory] (
        [MigrationId] nvarchar(150) NOT NULL,
        [ProductVersion] nvarchar(32) NOT NULL,
        CONSTRAINT [PK___EFMigrationsHistory] PRIMARY KEY ([MigrationId])
    );
END;
GO

BEGIN TRANSACTION;
ALTER TABLE [Products] ADD [ColorStocks] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260305080709_AddColorStocksToProducts', N'10.0.3');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [Products] ADD [ColorSizes] nvarchar(max) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260305084412_AddColorSizesToProducts', N'10.0.3');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [Reviews] ADD [SellerReply] nvarchar(max) NULL;

ALTER TABLE [Reviews] ADD [SellerReplyDate] datetime2 NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260309103000_AddSellerReplyToReviews', N'10.0.3');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [Products] ADD [Gender] nvarchar(max) NOT NULL DEFAULT N'';

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260311234500_AddGenderToProducts', N'10.0.3');

COMMIT;
GO

BEGIN TRANSACTION;
CREATE TABLE [ProductVariants] (
    [Id] int NOT NULL IDENTITY,
    [ProductId] int NOT NULL,
    [ColorName] nvarchar(max) NOT NULL,
    [Sizes] nvarchar(max) NULL,
    [Stock] int NOT NULL,
    CONSTRAINT [PK_ProductVariants] PRIMARY KEY ([Id]),
    CONSTRAINT [FK_ProductVariants_Products_ProductId] FOREIGN KEY ([ProductId]) REFERENCES [Products] ([ProductId]) ON DELETE CASCADE
);

CREATE INDEX [IX_ProductVariants_ProductId] ON [ProductVariants] ([ProductId]);

INSERT INTO ProductVariants (ProductId, ColorName, Sizes, Stock) SELECT p.ProductId, c.value AS ColorName, s.value AS Sizes, ISNULL(TRY_CAST(st.value AS int), 0) AS Stock FROM Products p CROSS APPLY OPENJSON(CONCAT('[', CHAR(34), REPLACE(ISNULL(p.Colors, ''), ',', CONCAT(CHAR(34), ',', CHAR(34))), CHAR(34), ']')) AS c LEFT JOIN OPENJSON(CONCAT('[', CHAR(34), REPLACE(ISNULL(p.ColorSizes, ''), '|', CONCAT(CHAR(34), ',', CHAR(34))), CHAR(34), ']')) AS s     ON s.[key] = c.[key] LEFT JOIN OPENJSON(CONCAT('[', CHAR(34), REPLACE(ISNULL(p.ColorStocks, ''), ',', CONCAT(CHAR(34), ',', CHAR(34))), CHAR(34), ']')) AS st     ON st.[key] = c.[key];

DECLARE @var nvarchar(max);
SELECT @var = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Products]') AND [c].[name] = N'ColorSizes');
IF @var IS NOT NULL EXEC(N'ALTER TABLE [Products] DROP CONSTRAINT ' + @var + ';');
ALTER TABLE [Products] DROP COLUMN [ColorSizes];

DECLARE @var1 nvarchar(max);
SELECT @var1 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Products]') AND [c].[name] = N'ColorStocks');
IF @var1 IS NOT NULL EXEC(N'ALTER TABLE [Products] DROP CONSTRAINT ' + @var1 + ';');
ALTER TABLE [Products] DROP COLUMN [ColorStocks];

DECLARE @var2 nvarchar(max);
SELECT @var2 = QUOTENAME([d].[name])
FROM [sys].[default_constraints] [d]
INNER JOIN [sys].[columns] [c] ON [d].[parent_column_id] = [c].[column_id] AND [d].[parent_object_id] = [c].[object_id]
WHERE ([d].[parent_object_id] = OBJECT_ID(N'[Products]') AND [c].[name] = N'Colors');
IF @var2 IS NOT NULL EXEC(N'ALTER TABLE [Products] DROP CONSTRAINT ' + @var2 + ';');
ALTER TABLE [Products] DROP COLUMN [Colors];

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260313032833_NormalizeProductVariants', N'10.0.3');

COMMIT;
GO

BEGIN TRANSACTION;
ALTER TABLE [Products] ADD [SKU] nvarchar(max) NULL;

ALTER TABLE [Products] ADD [Weight] decimal(18,2) NULL;

ALTER TABLE [Products] ADD [Length] decimal(18,2) NULL;

ALTER TABLE [Products] ADD [Height] decimal(18,2) NULL;

ALTER TABLE [Products] ADD [Width] decimal(18,2) NULL;

INSERT INTO [__EFMigrationsHistory] ([MigrationId], [ProductVersion])
VALUES (N'20260412083000_AddSkuAndDimensionsToProducts', N'10.0.3');

COMMIT;
GO

