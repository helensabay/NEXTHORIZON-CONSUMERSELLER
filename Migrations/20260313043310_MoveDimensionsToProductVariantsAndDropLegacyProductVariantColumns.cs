using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyAspNetApp.Migrations
{
    /// <inheritdoc />
    public partial class MoveDimensionsToProductVariantsAndDropLegacyProductVariantColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('ProductVariants', 'Length') IS NULL
                    ALTER TABLE ProductVariants ADD [Length] decimal(18,2) NULL;

                IF COL_LENGTH('ProductVariants', 'Height') IS NULL
                    ALTER TABLE ProductVariants ADD [Height] decimal(18,2) NULL;

                IF COL_LENGTH('ProductVariants', 'Width') IS NULL
                    ALTER TABLE ProductVariants ADD [Width] decimal(18,2) NULL;

                IF COL_LENGTH('ProductVariants', 'SKU') IS NULL
                    ALTER TABLE ProductVariants ADD [SKU] nvarchar(max) NOT NULL CONSTRAINT DF_ProductVariants_SKU DEFAULT('');

                IF COL_LENGTH('ProductVariants', 'Style') IS NULL
                    ALTER TABLE ProductVariants ADD [Style] nvarchar(max) NOT NULL CONSTRAINT DF_ProductVariants_Style DEFAULT('');

                IF COL_LENGTH('ProductVariants', 'Size') IS NULL
                    ALTER TABLE ProductVariants ADD [Size] nvarchar(max) NOT NULL CONSTRAINT DF_ProductVariants_Size DEFAULT('');

                IF COL_LENGTH('ProductVariants', 'Quantity') IS NULL
                    ALTER TABLE ProductVariants ADD [Quantity] int NOT NULL CONSTRAINT DF_ProductVariants_Quantity DEFAULT(0);

                IF COL_LENGTH('ProductVariants', 'Availability') IS NULL
                    ALTER TABLE ProductVariants ADD [Availability] nvarchar(max) NOT NULL CONSTRAINT DF_ProductVariants_Availability DEFAULT('In Stock');

                IF COL_LENGTH('ProductVariants', 'imagePath') IS NULL
                    ALTER TABLE ProductVariants ADD [imagePath] nvarchar(max) NOT NULL CONSTRAINT DF_ProductVariants_imagePath DEFAULT('');

                -- Backfill dimensions from parent product for existing variants.
                IF COL_LENGTH('Products', 'Length') IS NOT NULL
                   AND COL_LENGTH('Products', 'Height') IS NOT NULL
                   AND COL_LENGTH('Products', 'Width') IS NOT NULL
                BEGIN
                    EXEC('UPDATE pv
                          SET pv.[Length] = COALESCE(pv.[Length], p.[Length]),
                              pv.[Height] = COALESCE(pv.[Height], p.[Height]),
                              pv.[Width] = COALESCE(pv.[Width], p.[Width])
                          FROM ProductVariants pv
                          INNER JOIN Products p ON p.ProductId = pv.ProductId;');
                END

                -- Legacy mapping from old ProductVariants schema if present.
                IF COL_LENGTH('ProductVariants', 'ColorName') IS NOT NULL
                BEGIN
                    EXEC('UPDATE ProductVariants
                          SET Style = CASE WHEN Style = '''' THEN ISNULL(ColorName, '''') ELSE Style END;');
                END

                IF COL_LENGTH('ProductVariants', 'Sizes') IS NOT NULL
                BEGIN
                    EXEC('UPDATE ProductVariants
                          SET Size = CASE WHEN Size = '''' THEN ISNULL(Sizes, '''') ELSE Size END;');
                END

                IF COL_LENGTH('ProductVariants', 'Stock') IS NOT NULL
                BEGIN
                    EXEC('UPDATE ProductVariants
                          SET Quantity = CASE WHEN Quantity = 0 THEN ISNULL(Stock, 0) ELSE Quantity END;');
                END

                UPDATE ProductVariants
                SET Availability = CASE WHEN Quantity > 0 THEN 'In Stock' ELSE 'Pre-Order' END
                WHERE Availability IS NULL OR Availability = '';

                -- Remove denormalized product-variant columns from Products.
                IF COL_LENGTH('Products', 'Colors') IS NOT NULL
                    ALTER TABLE Products DROP COLUMN [Colors];

                IF COL_LENGTH('Products', 'ColorStocks') IS NOT NULL
                    ALTER TABLE Products DROP COLUMN [ColorStocks];

                IF COL_LENGTH('Products', 'ColorSizes') IS NOT NULL
                    ALTER TABLE Products DROP COLUMN [ColorSizes];
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('Products', 'Colors') IS NULL
                    ALTER TABLE Products ADD [Colors] nvarchar(max) NULL;

                IF COL_LENGTH('Products', 'ColorStocks') IS NULL
                    ALTER TABLE Products ADD [ColorStocks] nvarchar(max) NULL;

                IF COL_LENGTH('Products', 'ColorSizes') IS NULL
                    ALTER TABLE Products ADD [ColorSizes] nvarchar(max) NULL;
            ");
        }
    }
}
