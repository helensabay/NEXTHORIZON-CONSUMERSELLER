-- Migration: Add binary image columns to ProductVariants
-- Run: sqlcmd -S "100.102.166.9,1433" -d "NextHorizondb" -U "joa" -P "Joa123!" -i ".\NextHorizon\Database\add_variant_image_binary.sql"

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'ProductVariants' AND COLUMN_NAME = 'ImageData'
)
BEGIN
    ALTER TABLE dbo.ProductVariants ADD ImageData VARBINARY(MAX) NULL;
    PRINT 'Added ImageData column to ProductVariants';
END

IF NOT EXISTS (
    SELECT 1 FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_NAME = 'ProductVariants' AND COLUMN_NAME = 'ImageMimeType'
)
BEGIN
    ALTER TABLE dbo.ProductVariants ADD ImageMimeType NVARCHAR(50) NULL;
    PRINT 'Added ImageMimeType column to ProductVariants';
END
