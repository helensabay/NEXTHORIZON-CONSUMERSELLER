using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using MyAspNetApp.Data;

#nullable disable

namespace MyAspNetApp.Migrations
{
    [DbContext(typeof(MyAspNetApp.Data.AppDbContext))]
    [Migration("20260413050000_RemoveRedundantProductColumns")]
    public partial class RemoveRedundantProductColumns : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // drop columns that are now stored on product variants
            // use IF EXISTS to avoid errors when running against databases
            migrationBuilder.Sql(@"
                IF COL_LENGTH('Products','SKU') IS NOT NULL
                    ALTER TABLE Products DROP COLUMN [SKU];
                IF COL_LENGTH('Products','Weight') IS NOT NULL
                    ALTER TABLE Products DROP COLUMN [Weight];
                IF COL_LENGTH('Products','Length') IS NOT NULL
                    ALTER TABLE Products DROP COLUMN [Length];
                IF COL_LENGTH('Products','Height') IS NOT NULL
                    ALTER TABLE Products DROP COLUMN [Height];
                IF COL_LENGTH('Products','Width') IS NOT NULL
                    ALTER TABLE Products DROP COLUMN [Width];
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('Products','SKU') IS NULL
                    ALTER TABLE Products ADD [SKU] nvarchar(max) NULL;
                IF COL_LENGTH('Products','Weight') IS NULL
                    ALTER TABLE Products ADD [Weight] decimal(18,2) NULL;
                IF COL_LENGTH('Products','Length') IS NULL
                    ALTER TABLE Products ADD [Length] decimal(18,2) NULL;
                IF COL_LENGTH('Products','Height') IS NULL
                    ALTER TABLE Products ADD [Height] decimal(18,2) NULL;
                IF COL_LENGTH('Products','Width') IS NULL
                    ALTER TABLE Products ADD [Width] decimal(18,2) NULL;
            ");
        }
    }
}
