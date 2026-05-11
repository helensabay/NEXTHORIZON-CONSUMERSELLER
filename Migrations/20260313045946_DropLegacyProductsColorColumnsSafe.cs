using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyAspNetApp.Migrations
{
    /// <inheritdoc />
    public partial class DropLegacyProductsColorColumnsSafe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
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
