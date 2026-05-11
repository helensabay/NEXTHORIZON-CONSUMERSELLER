using Microsoft.EntityFrameworkCore.Migrations;

namespace NextHorizon.Data.Migrations;

public partial class AddReturnsTable : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF OBJECT_ID(N'dbo.returns', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.returns
                (
                    ReturnId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    OrderId INT NOT NULL,
                    UserId INT NOT NULL,
                    SellerId INT NULL,
                    Reason NVARCHAR(150) NOT NULL,
                    FileName NVARCHAR(260) NULL,
                    ContentType NVARCHAR(100) NULL,
                    ImageData VARBINARY(MAX) NULL,
                    Message NVARCHAR(MAX) NULL,
                    Status NVARCHAR(50) NOT NULL CONSTRAINT DF_returns_Status DEFAULT N'Return Requested',
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_returns_CreatedAt DEFAULT SYSUTCDATETIME(),
                    UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_returns_UpdatedAt DEFAULT SYSUTCDATETIME()
                );
            END;

            IF COL_LENGTH('dbo.returns', 'Message') IS NULL
                ALTER TABLE dbo.returns ADD Message NVARCHAR(MAX) NULL;

            IF COL_LENGTH('dbo.returns', 'Status') IS NULL
                ALTER TABLE dbo.returns ADD Status NVARCHAR(50) NULL;

            IF COL_LENGTH('dbo.returns', 'UpdatedAt') IS NULL
                ALTER TABLE dbo.returns ADD UpdatedAt DATETIME2 NULL;

            UPDATE dbo.returns
            SET Status = ISNULL(NULLIF(Status, N''), N'Return Requested'),
                UpdatedAt = ISNULL(UpdatedAt, CreatedAt)
            WHERE Status IS NULL OR LTRIM(RTRIM(Status)) = N'' OR UpdatedAt IS NULL;

            IF EXISTS (
                SELECT 1 FROM sys.columns
                WHERE object_id = OBJECT_ID(N'dbo.returns')
                  AND name = N'Status'
                  AND is_nullable = 1)
            BEGIN
                ALTER TABLE dbo.returns ALTER COLUMN Status NVARCHAR(50) NOT NULL;
            END;

            IF EXISTS (
                SELECT 1 FROM sys.columns
                WHERE object_id = OBJECT_ID(N'dbo.returns')
                  AND name = N'UpdatedAt'
                  AND is_nullable = 1)
            BEGIN
                ALTER TABLE dbo.returns ALTER COLUMN UpdatedAt DATETIME2 NOT NULL;
            END;

            IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_returns_Status')
                ALTER TABLE dbo.returns ADD CONSTRAINT DF_returns_Status DEFAULT N'Return Requested' FOR Status;

            IF NOT EXISTS (SELECT 1 FROM sys.default_constraints WHERE name = N'DF_returns_UpdatedAt')
                ALTER TABLE dbo.returns ADD CONSTRAINT DF_returns_UpdatedAt DEFAULT SYSUTCDATETIME() FOR UpdatedAt;

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_returns_SellerId_Status' AND object_id = OBJECT_ID(N'dbo.returns'))
                CREATE INDEX IX_returns_SellerId_Status ON dbo.returns (SellerId, Status);

            IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = N'IX_returns_OrderId' AND object_id = OBJECT_ID(N'dbo.returns'))
                CREATE INDEX IX_returns_OrderId ON dbo.returns (OrderId);
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF COL_LENGTH('dbo.returns', 'UpdatedAt') IS NOT NULL
                ALTER TABLE dbo.returns DROP CONSTRAINT IF EXISTS DF_returns_UpdatedAt;

            IF COL_LENGTH('dbo.returns', 'Status') IS NOT NULL
                ALTER TABLE dbo.returns DROP CONSTRAINT IF EXISTS DF_returns_Status;

            IF COL_LENGTH('dbo.returns', 'Message') IS NOT NULL
                ALTER TABLE dbo.returns DROP COLUMN Message;

            IF COL_LENGTH('dbo.returns', 'Status') IS NOT NULL
                ALTER TABLE dbo.returns DROP COLUMN Status;

            IF COL_LENGTH('dbo.returns', 'UpdatedAt') IS NOT NULL
                ALTER TABLE dbo.returns DROP COLUMN UpdatedAt;
            """);
    }
}
