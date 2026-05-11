using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyAspNetApp.Migrations
{
    [DbContext(typeof(MyAspNetApp.Data.AppDbContext))]
    [Migration("20260507000000_AddVariantImageData")]
    public partial class AddVariantImageData : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH(N'dbo.ProductVariants', N'ImageData') IS NULL
                    ALTER TABLE dbo.ProductVariants ADD ImageData VARBINARY(MAX) NULL;

                IF COL_LENGTH(N'dbo.ProductVariants', N'ImageMimeType') IS NULL
                    ALTER TABLE dbo.ProductVariants ADD ImageMimeType NVARCHAR(200) NULL;
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH(N'dbo.ProductVariants', N'ImageMimeType') IS NOT NULL
                    ALTER TABLE dbo.ProductVariants DROP COLUMN ImageMimeType;

                IF COL_LENGTH(N'dbo.ProductVariants', N'ImageData') IS NOT NULL
                    ALTER TABLE dbo.ProductVariants DROP COLUMN ImageData;
                """);
        }
    }
}
