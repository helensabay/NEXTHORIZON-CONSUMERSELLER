using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NextHorizon.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddMessagingModule : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            EnsureLegacyMessagingTables(migrationBuilder);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER PROCEDURE dbo.sp_MessageConversation_GetById
                    @ConversationID INT,
                    @UserID NVARCHAR(450)
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF NOT EXISTS (
                        SELECT 1
                        FROM dbo.MessagingConversations
                        WHERE ConversationId = @ConversationID
                          AND (BuyerUserId = @UserID OR SellerUserId = @UserID)
                    )
                    BEGIN
                        RETURN;
                    END

                    SELECT
                        c.ConversationId AS ConversationID,
                        c.BuyerUserId AS BuyerUserID,
                        c.SellerUserId AS SellerUserID,
                        c.ContextType AS ContextType,
                        c.OrderId AS OrderID,
                        c.LastMessageAt AS LastMessageAt,
                        c.BuyerLastReadAt AS BuyerLastReadAt,
                        c.SellerLastReadAt AS SellerLastReadAt,
                        CASE
                            WHEN lm.MessageId IS NULL THEN NULL
                            WHEN lm.IsDeleted = 1 THEN N'[deleted]'
                            WHEN LEN(lm.Body) > 120 THEN LEFT(lm.Body, 117) + N'...'
                            ELSE lm.Body
                        END AS LastMessagePreview,
                        (
                            SELECT COUNT(1)
                            FROM dbo.MessagingMessages m
                            WHERE m.ConversationId = c.ConversationId
                              AND m.IsDeleted = 0
                              AND m.SenderUserId <> @UserID
                              AND m.SentAt > COALESCE(
                                    CASE WHEN c.BuyerUserId = @UserID THEN c.BuyerLastReadAt ELSE c.SellerLastReadAt END,
                                    CONVERT(DATETIME2, '1900-01-01')
                              )
                        ) AS UnreadCount,
                        c.CreatedAt AS CreatedAt,
                        c.UpdatedAt AS UpdatedAt
                    FROM dbo.MessagingConversations c
                    OUTER APPLY
                    (
                        SELECT TOP (1)
                            m.MessageId,
                            m.Body,
                            m.IsDeleted
                        FROM dbo.MessagingMessages m
                        WHERE m.ConversationId = c.ConversationId
                        ORDER BY m.SentAt DESC, m.MessageId DESC
                    ) lm
                    WHERE c.ConversationId = @ConversationID;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER PROCEDURE dbo.sp_MessageConversation_CreateOrGet_General
                    @BuyerUserID NVARCHAR(450),
                    @SellerUserID NVARCHAR(450)
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF @BuyerUserID = @SellerUserID
                    BEGIN
                        THROW 50001, 'Buyer and seller must be different users.', 1;
                    END

                    DECLARE @ConversationID INT;

                    SELECT TOP (1) @ConversationID = ConversationId
                    FROM dbo.MessagingConversations
                    WHERE BuyerUserId = @BuyerUserID
                      AND SellerUserId = @SellerUserID
                      AND ContextType = 1;

                    IF @ConversationID IS NULL
                    BEGIN
                        INSERT INTO dbo.MessagingConversations
                        (
                            BuyerUserId,
                            SellerUserId,
                            ContextType,
                            OrderId,
                            LastMessageAt,
                            BuyerLastReadAt,
                            SellerLastReadAt,
                            CreatedAt,
                            UpdatedAt
                        )
                        VALUES
                        (
                            @BuyerUserID,
                            @SellerUserID,
                            1,
                            NULL,
                            NULL,
                            NULL,
                            NULL,
                            SYSUTCDATETIME(),
                            SYSUTCDATETIME()
                        );

                        SET @ConversationID = CAST(SCOPE_IDENTITY() AS INT);
                    END

                    EXEC dbo.sp_MessageConversation_GetById
                        @ConversationID = @ConversationID,
                        @UserID = @BuyerUserID;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER PROCEDURE dbo.sp_MessageConversation_CreateOrGet_Order
                    @OrderID INT,
                    @BuyerUserID NVARCHAR(450),
                    @SellerUserID NVARCHAR(450)
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF @BuyerUserID = @SellerUserID
                    BEGIN
                        THROW 50002, 'Buyer and seller must be different users for order conversations.', 1;
                    END

                    DECLARE @ConversationID INT;

                    SELECT TOP (1) @ConversationID = ConversationId
                    FROM dbo.MessagingConversations
                    WHERE OrderId = @OrderID
                      AND ContextType = 2;

                    IF @ConversationID IS NULL
                    BEGIN
                        INSERT INTO dbo.MessagingConversations
                        (
                            BuyerUserId,
                            SellerUserId,
                            ContextType,
                            OrderId,
                            LastMessageAt,
                            BuyerLastReadAt,
                            SellerLastReadAt,
                            CreatedAt,
                            UpdatedAt
                        )
                        VALUES
                        (
                            @BuyerUserID,
                            @SellerUserID,
                            2,
                            @OrderID,
                            NULL,
                            NULL,
                            NULL,
                            SYSUTCDATETIME(),
                            SYSUTCDATETIME()
                        );

                        SET @ConversationID = CAST(SCOPE_IDENTITY() AS INT);
                    END

                    EXEC dbo.sp_MessageConversation_GetById
                        @ConversationID = @ConversationID,
                        @UserID = @BuyerUserID;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER PROCEDURE dbo.sp_MessageConversation_ListByUser
                    @UserID NVARCHAR(450),
                    @Page INT,
                    @PageSize INT
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF @Page < 1 SET @Page = 1;
                    IF @PageSize < 1 SET @PageSize = 20;
                    IF @PageSize > 100 SET @PageSize = 100;

                    ;WITH Base AS
                    (
                        SELECT
                            c.ConversationId,
                            c.BuyerUserId,
                            c.SellerUserId,
                            c.ContextType,
                            c.OrderId,
                            c.LastMessageAt,
                            c.BuyerLastReadAt,
                            c.SellerLastReadAt,
                            c.CreatedAt,
                            c.UpdatedAt
                        FROM dbo.MessagingConversations c
                        WHERE c.BuyerUserId = @UserID OR c.SellerUserId = @UserID
                    )
                    SELECT
                        b.ConversationId AS ConversationID,
                        b.BuyerUserId AS BuyerUserID,
                        b.SellerUserId AS SellerUserID,
                        b.ContextType AS ContextType,
                        b.OrderId AS OrderID,
                        b.LastMessageAt AS LastMessageAt,
                        b.BuyerLastReadAt AS BuyerLastReadAt,
                        b.SellerLastReadAt AS SellerLastReadAt,
                        CASE
                            WHEN lm.MessageId IS NULL THEN NULL
                            WHEN lm.IsDeleted = 1 THEN N'[deleted]'
                            WHEN LEN(lm.Body) > 120 THEN LEFT(lm.Body, 117) + N'...'
                            ELSE lm.Body
                        END AS LastMessagePreview,
                        (
                            SELECT COUNT(1)
                            FROM dbo.MessagingMessages m
                            WHERE m.ConversationId = b.ConversationId
                              AND m.IsDeleted = 0
                              AND m.SenderUserId <> @UserID
                              AND m.SentAt > COALESCE(
                                    CASE WHEN b.BuyerUserId = @UserID THEN b.BuyerLastReadAt ELSE b.SellerLastReadAt END,
                                    CONVERT(DATETIME2, '1900-01-01')
                              )
                        ) AS UnreadCount,
                        b.CreatedAt AS CreatedAt,
                        b.UpdatedAt AS UpdatedAt
                    FROM Base b
                    OUTER APPLY
                    (
                        SELECT TOP (1)
                            m.MessageId,
                            m.Body,
                            m.IsDeleted
                        FROM dbo.MessagingMessages m
                        WHERE m.ConversationId = b.ConversationId
                        ORDER BY m.SentAt DESC, m.MessageId DESC
                    ) lm
                    ORDER BY COALESCE(b.LastMessageAt, b.CreatedAt) DESC, b.ConversationId DESC
                    OFFSET (@Page - 1) * @PageSize ROWS
                    FETCH NEXT @PageSize ROWS ONLY;

                    SELECT COUNT(1) AS TotalCount
                    FROM Base;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER PROCEDURE dbo.sp_Message_Send
                    @ConversationID INT,
                    @SenderUserID NVARCHAR(450),
                    @Body NVARCHAR(2000),
                    @AttachmentUrl NVARCHAR(400) = NULL
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF @Body IS NULL SET @Body = N'';

                    IF NOT EXISTS (
                        SELECT 1
                        FROM dbo.MessagingConversations
                        WHERE ConversationId = @ConversationID
                          AND (BuyerUserId = @SenderUserID OR SellerUserId = @SenderUserID)
                    )
                    BEGIN
                        RETURN;
                    END

                    DECLARE @SentAt DATETIME2 = SYSUTCDATETIME();

                    INSERT INTO dbo.MessagingMessages
                    (
                        ConversationId,
                        SenderUserId,
                        Body,
                        AttachmentUrl,
                        SentAt,
                        IsDeleted
                    )
                    VALUES
                    (
                        @ConversationID,
                        @SenderUserID,
                        @Body,
                        @AttachmentUrl,
                        @SentAt,
                        0
                    );

                    UPDATE dbo.MessagingConversations
                    SET LastMessageAt = @SentAt,
                        UpdatedAt = @SentAt
                    WHERE ConversationId = @ConversationID;

                    SELECT
                        MessageId AS MessageID,
                        ConversationId AS ConversationID,
                        SenderUserId AS SenderUserID,
                        Body AS Body,
                        AttachmentUrl AS AttachmentUrl,
                        SentAt AS SentAt,
                        IsDeleted AS IsDeleted
                    FROM dbo.MessagingMessages
                    WHERE MessageId = CAST(SCOPE_IDENTITY() AS BIGINT);
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER PROCEDURE dbo.sp_Message_List
                    @ConversationID INT,
                    @UserID NVARCHAR(450),
                    @Before DATETIME2 = NULL,
                    @PageSize INT = 50
                AS
                BEGIN
                    SET NOCOUNT ON;

                    IF @PageSize < 1 SET @PageSize = 50;
                    IF @PageSize > 100 SET @PageSize = 100;

                    DECLARE @IsMember BIT = 0;

                    IF EXISTS (
                        SELECT 1
                        FROM dbo.MessagingConversations
                        WHERE ConversationId = @ConversationID
                          AND (BuyerUserId = @UserID OR SellerUserId = @UserID)
                    )
                    BEGIN
                        SET @IsMember = 1;
                    END

                    SELECT @IsMember AS IsMember;

                    IF @IsMember = 0
                    BEGIN
                        RETURN;
                    END

                    SELECT TOP (@PageSize)
                        MessageId AS MessageID,
                        ConversationId AS ConversationID,
                        SenderUserId AS SenderUserID,
                        Body AS Body,
                        AttachmentUrl AS AttachmentUrl,
                        SentAt AS SentAt,
                        IsDeleted AS IsDeleted
                    FROM dbo.MessagingMessages
                    WHERE ConversationId = @ConversationID
                      AND (@Before IS NULL OR SentAt < @Before)
                    ORDER BY SentAt DESC, MessageId DESC;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER PROCEDURE dbo.sp_MessageConversation_MarkRead
                    @ConversationID INT,
                    @UserID NVARCHAR(450)
                AS
                BEGIN
                    SET NOCOUNT ON;

                    UPDATE dbo.MessagingConversations
                    SET BuyerLastReadAt = CASE WHEN BuyerUserId = @UserID THEN SYSUTCDATETIME() ELSE BuyerLastReadAt END,
                        SellerLastReadAt = CASE WHEN SellerUserId = @UserID THEN SYSUTCDATETIME() ELSE SellerLastReadAt END,
                        UpdatedAt = SYSUTCDATETIME()
                    WHERE ConversationId = @ConversationID
                      AND (BuyerUserId = @UserID OR SellerUserId = @UserID);

                    SELECT CAST(CASE WHEN @@ROWCOUNT > 0 THEN 1 ELSE 0 END AS BIT) AS Updated;
                END;
                """);

            migrationBuilder.Sql(
                """
                CREATE OR ALTER PROCEDURE dbo.sp_Message_SoftDelete
                    @MessageID BIGINT,
                    @UserID NVARCHAR(450)
                AS
                BEGIN
                    SET NOCOUNT ON;

                    UPDATE dbo.MessagingMessages
                    SET IsDeleted = 1
                    WHERE MessageId = @MessageID
                      AND SenderUserId = @UserID
                      AND IsDeleted = 0;

                    SELECT CAST(CASE WHEN @@ROWCOUNT > 0 THEN 1 ELSE 0 END AS BIT) AS Deleted;
                END;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_Message_SoftDelete;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_MessageConversation_MarkRead;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_Message_List;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_Message_Send;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_MessageConversation_ListByUser;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_MessageConversation_CreateOrGet_Order;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_MessageConversation_CreateOrGet_General;");
            migrationBuilder.Sql("DROP PROCEDURE IF EXISTS dbo.sp_MessageConversation_GetById;");

            migrationBuilder.DropTable(
                name: "MessagingMessages");

            migrationBuilder.DropTable(
                name: "MessagingConversations");
        }

        private static void EnsureLegacyMessagingTables(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[MessagingConversations]', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.MessagingConversations
                    (
                        ConversationId INT NOT NULL IDENTITY(1,1),
                        BuyerUserId NVARCHAR(450) NOT NULL,
                        SellerUserId NVARCHAR(450) NOT NULL,
                        ContextType TINYINT NOT NULL,
                        OrderId INT NULL,
                        LastMessageAt DATETIME2 NULL,
                        BuyerLastReadAt DATETIME2 NULL,
                        SellerLastReadAt DATETIME2 NULL,
                        CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_MessagingConversations_CreatedAt DEFAULT SYSUTCDATETIME(),
                        UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_MessagingConversations_UpdatedAt DEFAULT SYSUTCDATETIME(),
                        CONSTRAINT PK_MessagingConversations PRIMARY KEY (ConversationId)
                    );
                END;

                IF OBJECT_ID(N'[dbo].[MessagingMessages]', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.MessagingMessages
                    (
                        MessageId BIGINT NOT NULL IDENTITY(1,1),
                        ConversationId INT NOT NULL,
                        SenderUserId NVARCHAR(450) NOT NULL,
                        Body NVARCHAR(2000) NOT NULL,
                        AttachmentUrl NVARCHAR(400) NULL,
                        SentAt DATETIME2 NOT NULL CONSTRAINT DF_MessagingMessages_SentAt DEFAULT SYSUTCDATETIME(),
                        IsDeleted BIT NOT NULL CONSTRAINT DF_MessagingMessages_IsDeleted DEFAULT ((0)),
                        CONSTRAINT PK_MessagingMessages PRIMARY KEY (MessageId)
                    );
                END;

                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.columns c
                    INNER JOIN sys.types t ON t.user_type_id = c.user_type_id
                    WHERE c.object_id = OBJECT_ID(N'[dbo].[MessagingConversations]', N'U')
                      AND c.name = N'ConversationId'
                      AND t.name = N'int'
                      AND c.is_nullable = 0
                      AND c.is_identity = 1
                )
                    THROW 51060, 'Migration blocked: [dbo].[MessagingConversations].[ConversationId] must be NOT NULL INT IDENTITY.', 1;

                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.columns c
                    INNER JOIN sys.types t ON t.user_type_id = c.user_type_id
                    WHERE c.object_id = OBJECT_ID(N'[dbo].[MessagingConversations]', N'U')
                      AND c.name = N'BuyerUserId'
                      AND t.name = N'nvarchar'
                      AND c.max_length = 900
                      AND c.is_nullable = 0
                )
                    THROW 51061, 'Migration blocked: [dbo].[MessagingConversations].[BuyerUserId] must be NOT NULL NVARCHAR(450).', 1;

                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.columns c
                    INNER JOIN sys.types t ON t.user_type_id = c.user_type_id
                    WHERE c.object_id = OBJECT_ID(N'[dbo].[MessagingConversations]', N'U')
                      AND c.name = N'SellerUserId'
                      AND t.name = N'nvarchar'
                      AND c.max_length = 900
                      AND c.is_nullable = 0
                )
                    THROW 51062, 'Migration blocked: [dbo].[MessagingConversations].[SellerUserId] must be NOT NULL NVARCHAR(450).', 1;

                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.columns c
                    INNER JOIN sys.types t ON t.user_type_id = c.user_type_id
                    WHERE c.object_id = OBJECT_ID(N'[dbo].[MessagingConversations]', N'U')
                      AND c.name = N'ContextType'
                      AND t.name = N'tinyint'
                      AND c.is_nullable = 0
                )
                    THROW 51063, 'Migration blocked: [dbo].[MessagingConversations].[ContextType] must be NOT NULL TINYINT.', 1;

                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.columns c
                    INNER JOIN sys.types t ON t.user_type_id = c.user_type_id
                    WHERE c.object_id = OBJECT_ID(N'[dbo].[MessagingMessages]', N'U')
                      AND c.name = N'MessageId'
                      AND t.name = N'bigint'
                      AND c.is_nullable = 0
                      AND c.is_identity = 1
                )
                    THROW 51064, 'Migration blocked: [dbo].[MessagingMessages].[MessageId] must be NOT NULL BIGINT IDENTITY.', 1;

                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.columns c
                    INNER JOIN sys.types t ON t.user_type_id = c.user_type_id
                    WHERE c.object_id = OBJECT_ID(N'[dbo].[MessagingMessages]', N'U')
                      AND c.name = N'ConversationId'
                      AND t.name = N'int'
                      AND c.is_nullable = 0
                )
                    THROW 51065, 'Migration blocked: [dbo].[MessagingMessages].[ConversationId] must be NOT NULL INT.', 1;

                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.columns c
                    INNER JOIN sys.types t ON t.user_type_id = c.user_type_id
                    WHERE c.object_id = OBJECT_ID(N'[dbo].[MessagingMessages]', N'U')
                      AND c.name = N'SenderUserId'
                      AND t.name = N'nvarchar'
                      AND c.max_length = 900
                      AND c.is_nullable = 0
                )
                    THROW 51066, 'Migration blocked: [dbo].[MessagingMessages].[SenderUserId] must be NOT NULL NVARCHAR(450).', 1;

                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.columns c
                    INNER JOIN sys.types t ON t.user_type_id = c.user_type_id
                    WHERE c.object_id = OBJECT_ID(N'[dbo].[MessagingMessages]', N'U')
                      AND c.name = N'Body'
                      AND t.name = N'nvarchar'
                      AND c.max_length = 4000
                      AND c.is_nullable = 0
                )
                    THROW 51067, 'Migration blocked: [dbo].[MessagingMessages].[Body] must be NOT NULL NVARCHAR(2000).', 1;

                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.key_constraints kc
                    INNER JOIN sys.index_columns ic
                        ON ic.object_id = kc.parent_object_id
                       AND ic.index_id = kc.unique_index_id
                    INNER JOIN sys.columns c
                        ON c.object_id = ic.object_id
                       AND c.column_id = ic.column_id
                    WHERE kc.parent_object_id = OBJECT_ID(N'[dbo].[MessagingConversations]', N'U')
                      AND kc.[type] = N'PK'
                    GROUP BY kc.name
                    HAVING COUNT(*) = 1
                       AND MAX(CASE WHEN c.name = N'ConversationId' THEN 1 ELSE 0 END) = 1
                )
                    THROW 51068, 'Migration blocked: [dbo].[MessagingConversations] must have a single-column primary key on [ConversationId].', 1;

                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.key_constraints kc
                    INNER JOIN sys.index_columns ic
                        ON ic.object_id = kc.parent_object_id
                       AND ic.index_id = kc.unique_index_id
                    INNER JOIN sys.columns c
                        ON c.object_id = ic.object_id
                       AND c.column_id = ic.column_id
                    WHERE kc.parent_object_id = OBJECT_ID(N'[dbo].[MessagingMessages]', N'U')
                      AND kc.[type] = N'PK'
                    GROUP BY kc.name
                    HAVING COUNT(*) = 1
                       AND MAX(CASE WHEN c.name = N'MessageId' THEN 1 ELSE 0 END) = 1
                )
                    THROW 51069, 'Migration blocked: [dbo].[MessagingMessages] must have a single-column primary key on [MessageId].', 1;

                IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_MessagingConversations_ContextType')
                    ALTER TABLE dbo.MessagingConversations
                        ADD CONSTRAINT CK_MessagingConversations_ContextType CHECK ([ContextType] IN (1, 2));

                IF NOT EXISTS (SELECT 1 FROM sys.check_constraints WHERE name = N'CK_MessagingConversations_ContextType_Order')
                    ALTER TABLE dbo.MessagingConversations
                        ADD CONSTRAINT CK_MessagingConversations_ContextType_Order CHECK (([ContextType] = 1 AND [OrderId] IS NULL) OR ([ContextType] = 2 AND [OrderId] IS NOT NULL));

                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_MessagingMessages_MessagingConversations_ConversationId')
                    ALTER TABLE dbo.MessagingMessages WITH CHECK
                        ADD CONSTRAINT FK_MessagingMessages_MessagingConversations_ConversationId
                        FOREIGN KEY (ConversationId) REFERENCES dbo.MessagingConversations(ConversationId) ON DELETE CASCADE;

                IF OBJECT_ID(N'[dbo].[AspNetUsers]', N'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_MessagingConversations_AspNetUsers_BuyerUserId')
                    ALTER TABLE dbo.MessagingConversations WITH CHECK
                        ADD CONSTRAINT FK_MessagingConversations_AspNetUsers_BuyerUserId
                        FOREIGN KEY (BuyerUserId) REFERENCES dbo.AspNetUsers(Id) ON DELETE NO ACTION;

                IF OBJECT_ID(N'[dbo].[AspNetUsers]', N'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_MessagingConversations_AspNetUsers_SellerUserId')
                    ALTER TABLE dbo.MessagingConversations WITH CHECK
                        ADD CONSTRAINT FK_MessagingConversations_AspNetUsers_SellerUserId
                        FOREIGN KEY (SellerUserId) REFERENCES dbo.AspNetUsers(Id) ON DELETE NO ACTION;

                IF OBJECT_ID(N'[dbo].[AspNetUsers]', N'U') IS NOT NULL
                   AND NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_MessagingMessages_AspNetUsers_SenderUserId')
                    ALTER TABLE dbo.MessagingMessages WITH CHECK
                        ADD CONSTRAINT FK_MessagingMessages_AspNetUsers_SenderUserId
                        FOREIGN KEY (SenderUserId) REFERENCES dbo.AspNetUsers(Id) ON DELETE NO ACTION;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[MessagingConversations]', N'U') AND name = N'IX_MessagingConversations_BuyerUserId')
                    CREATE INDEX IX_MessagingConversations_BuyerUserId ON dbo.MessagingConversations (BuyerUserId);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[MessagingConversations]', N'U') AND name = N'IX_MessagingConversations_BuyerUserId_SellerUserId_ContextType')
                    CREATE UNIQUE INDEX IX_MessagingConversations_BuyerUserId_SellerUserId_ContextType
                        ON dbo.MessagingConversations (BuyerUserId, SellerUserId, ContextType)
                        WHERE [ContextType] = 1;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[MessagingConversations]', N'U') AND name = N'IX_MessagingConversations_LastMessageAt')
                    CREATE INDEX IX_MessagingConversations_LastMessageAt ON dbo.MessagingConversations (LastMessageAt);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[MessagingConversations]', N'U') AND name = N'IX_MessagingConversations_OrderId_ContextType')
                    CREATE UNIQUE INDEX IX_MessagingConversations_OrderId_ContextType
                        ON dbo.MessagingConversations (OrderId, ContextType)
                        WHERE [ContextType] = 2;

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[MessagingConversations]', N'U') AND name = N'IX_MessagingConversations_SellerUserId')
                    CREATE INDEX IX_MessagingConversations_SellerUserId ON dbo.MessagingConversations (SellerUserId);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[MessagingMessages]', N'U') AND name = N'IX_MessagingMessages_ConversationId_SentAt')
                    CREATE INDEX IX_MessagingMessages_ConversationId_SentAt ON dbo.MessagingMessages (ConversationId ASC, SentAt DESC);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[MessagingMessages]', N'U') AND name = N'IX_MessagingMessages_SenderUserId')
                    CREATE INDEX IX_MessagingMessages_SenderUserId ON dbo.MessagingMessages (SenderUserId);
                """);
        }
    }
}
