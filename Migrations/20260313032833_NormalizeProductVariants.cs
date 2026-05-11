using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MyAspNetApp.Migrations
{
    /// <inheritdoc />
    public partial class NormalizeProductVariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Create ProductVariants table only if it doesn't exist  
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[ProductVariants]', N'U') IS NULL
                BEGIN
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
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProductVariants");

            migrationBuilder.AddColumn<string>(
                name: "ColorSizes",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ColorStocks",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Colors",
                table: "Products",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
