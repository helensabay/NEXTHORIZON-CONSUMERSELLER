using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MyAspNetApp.Data;

#nullable disable

namespace MyAspNetApp.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20260309103000_AddSellerReplyToReviews")]
    public partial class AddSellerReplyToReviews : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "SellerReply",
                table: "Reviews",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SellerReplyDate",
                table: "Reviews",
                type: "datetime2",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SellerReply",
                table: "Reviews");

            migrationBuilder.DropColumn(
                name: "SellerReplyDate",
                table: "Reviews");
        }
    }
}
