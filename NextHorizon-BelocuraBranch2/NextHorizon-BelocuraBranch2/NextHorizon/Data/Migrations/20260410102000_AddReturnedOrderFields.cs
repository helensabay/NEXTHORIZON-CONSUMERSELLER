using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NextHorizon.Data.Migrations;

public partial class AddReturnedOrderFields : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF COL_LENGTH('dbo.Orders', 'ReturnReason') IS NULL
                ALTER TABLE dbo.Orders ADD ReturnReason NVARCHAR(100) NULL;

            IF COL_LENGTH('dbo.Orders', 'ReturnNote') IS NULL
                ALTER TABLE dbo.Orders ADD ReturnNote NVARCHAR(MAX) NULL;

            IF COL_LENGTH('dbo.Orders', 'ReturnProofImageData') IS NULL
                ALTER TABLE dbo.Orders ADD ReturnProofImageData VARBINARY(MAX) NULL;

            IF COL_LENGTH('dbo.Orders', 'ReturnProofImageMimeType') IS NULL
                ALTER TABLE dbo.Orders ADD ReturnProofImageMimeType NVARCHAR(100) NULL;

            IF COL_LENGTH('dbo.Orders', 'ReturnProcessedAt') IS NULL
                ALTER TABLE dbo.Orders ADD ReturnProcessedAt DATETIME2 NULL;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF COL_LENGTH('dbo.Orders', 'ReturnProcessedAt') IS NOT NULL
                ALTER TABLE dbo.Orders DROP COLUMN ReturnProcessedAt;

            IF COL_LENGTH('dbo.Orders', 'ReturnProofImageMimeType') IS NOT NULL
                ALTER TABLE dbo.Orders DROP COLUMN ReturnProofImageMimeType;

            IF COL_LENGTH('dbo.Orders', 'ReturnProofImageData') IS NOT NULL
                ALTER TABLE dbo.Orders DROP COLUMN ReturnProofImageData;

            IF COL_LENGTH('dbo.Orders', 'ReturnNote') IS NOT NULL
                ALTER TABLE dbo.Orders DROP COLUMN ReturnNote;

            IF COL_LENGTH('dbo.Orders', 'ReturnReason') IS NOT NULL
                ALTER TABLE dbo.Orders DROP COLUMN ReturnReason;
            """);
    }
}
