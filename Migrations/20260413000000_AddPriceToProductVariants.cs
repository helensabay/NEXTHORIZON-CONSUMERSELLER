using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MyAspNetApp.Data;

#nullable disable

namespace MyAspNetApp.Migrations
{
    [DbContext(typeof(MyAspNetApp.Data.AppDbContext))]
    [Migration("20260413000000_AddPriceToProductVariants")]
    public partial class AddPriceToProductVariants : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Price",
                table: "ProductVariants",
                type: "decimal(18,2)",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Price",
                table: "ProductVariants");
        }
    }
}
