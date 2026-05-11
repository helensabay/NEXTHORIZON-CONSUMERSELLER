using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NextHorizon.Data.Migrations;

public partial class StoreMessagingAttachmentsInDb : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF COL_LENGTH('dbo.MessagingMessages', 'AttachmentData') IS NULL
            BEGIN
                ALTER TABLE dbo.MessagingMessages ADD AttachmentData VARBINARY(MAX) NULL;
            END;

            IF COL_LENGTH('dbo.MessagingMessages', 'AttachmentContentType') IS NULL
            BEGIN
                ALTER TABLE dbo.MessagingMessages ADD AttachmentContentType NVARCHAR(100) NULL;
            END;

            IF COL_LENGTH('dbo.MessagingMessages', 'AttachmentFileName') IS NULL
            BEGIN
                ALTER TABLE dbo.MessagingMessages ADD AttachmentFileName NVARCHAR(255) NULL;
            END;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql(
            """
            IF COL_LENGTH('dbo.MessagingMessages', 'AttachmentFileName') IS NOT NULL
            BEGIN
                ALTER TABLE dbo.MessagingMessages DROP COLUMN AttachmentFileName;
            END;

            IF COL_LENGTH('dbo.MessagingMessages', 'AttachmentContentType') IS NOT NULL
            BEGIN
                ALTER TABLE dbo.MessagingMessages DROP COLUMN AttachmentContentType;
            END;

            IF COL_LENGTH('dbo.MessagingMessages', 'AttachmentData') IS NOT NULL
            BEGIN
                ALTER TABLE dbo.MessagingMessages DROP COLUMN AttachmentData;
            END;
            """);
    }
}
