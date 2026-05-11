using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace NextHorizon.Data.Migrations
{
    /// <inheritdoc />
    public partial class RepairMissingMessagingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                IF OBJECT_ID(N'[dbo].[Users]', N'U') IS NULL
                    THROW 51100, 'Repair blocked: [dbo].[Users] is required before restoring messaging tables.', 1;

                IF OBJECT_ID(N'[dbo].[Consumers]', N'U') IS NULL
                    THROW 51101, 'Repair blocked: [dbo].[Consumers] is required before restoring messaging tables.', 1;

                IF OBJECT_ID(N'[dbo].[Sellers]', N'U') IS NULL
                    THROW 51102, 'Repair blocked: [dbo].[Sellers] is required before restoring messaging tables.', 1;

                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.columns c
                    INNER JOIN sys.types t ON t.user_type_id = c.user_type_id
                    WHERE c.object_id = OBJECT_ID(N'[dbo].[Users]', N'U')
                      AND c.name = N'user_id'
                      AND t.name = N'int'
                      AND c.is_nullable = 0
                )
                    THROW 51103, 'Repair blocked: [dbo].[Users].[user_id] must be NOT NULL INT.', 1;

                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.columns c
                    INNER JOIN sys.types t ON t.user_type_id = c.user_type_id
                    WHERE c.object_id = OBJECT_ID(N'[dbo].[Consumers]', N'U')
                      AND c.name = N'consumer_id'
                      AND t.name = N'int'
                      AND c.is_nullable = 0
                )
                    THROW 51104, 'Repair blocked: [dbo].[Consumers].[consumer_id] must be NOT NULL INT.', 1;

                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.columns c
                    INNER JOIN sys.types t ON t.user_type_id = c.user_type_id
                    WHERE c.object_id = OBJECT_ID(N'[dbo].[Sellers]', N'U')
                      AND c.name = N'seller_id'
                      AND t.name = N'int'
                      AND c.is_nullable = 0
                )
                    THROW 51105, 'Repair blocked: [dbo].[Sellers].[seller_id] must be NOT NULL INT.', 1;

                IF OBJECT_ID(N'[dbo].[MessagingConversations]', N'U') IS NULL
                BEGIN
                    CREATE TABLE dbo.MessagingConversations
                    (
                        ConversationId INT NOT NULL IDENTITY(1,1),
                        BuyerUserId INT NOT NULL,
                        SellerUserId INT NOT NULL,
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
                        SenderUserId INT NOT NULL,
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
                    THROW 51106, 'Repair blocked: [dbo].[MessagingConversations].[ConversationId] must be NOT NULL INT IDENTITY.', 1;

                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.columns c
                    INNER JOIN sys.types t ON t.user_type_id = c.user_type_id
                    WHERE c.object_id = OBJECT_ID(N'[dbo].[MessagingConversations]', N'U')
                      AND c.name = N'BuyerUserId'
                      AND t.name = N'int'
                      AND c.is_nullable = 0
                )
                    THROW 51107, 'Repair blocked: [dbo].[MessagingConversations].[BuyerUserId] must be NOT NULL INT.', 1;

                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.columns c
                    INNER JOIN sys.types t ON t.user_type_id = c.user_type_id
                    WHERE c.object_id = OBJECT_ID(N'[dbo].[MessagingConversations]', N'U')
                      AND c.name = N'SellerUserId'
                      AND t.name = N'int'
                      AND c.is_nullable = 0
                )
                    THROW 51108, 'Repair blocked: [dbo].[MessagingConversations].[SellerUserId] must be NOT NULL INT.', 1;

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
                    THROW 51109, 'Repair blocked: [dbo].[MessagingConversations].[ContextType] must be NOT NULL TINYINT.', 1;

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
                    THROW 51110, 'Repair blocked: [dbo].[MessagingMessages].[MessageId] must be NOT NULL BIGINT IDENTITY.', 1;

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
                    THROW 51111, 'Repair blocked: [dbo].[MessagingMessages].[ConversationId] must be NOT NULL INT.', 1;

                IF NOT EXISTS
                (
                    SELECT 1
                    FROM sys.columns c
                    INNER JOIN sys.types t ON t.user_type_id = c.user_type_id
                    WHERE c.object_id = OBJECT_ID(N'[dbo].[MessagingMessages]', N'U')
                      AND c.name = N'SenderUserId'
                      AND t.name = N'int'
                      AND c.is_nullable = 0
                )
                    THROW 51112, 'Repair blocked: [dbo].[MessagingMessages].[SenderUserId] must be NOT NULL INT.', 1;

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
                    THROW 51113, 'Repair blocked: [dbo].[MessagingMessages].[Body] must be NOT NULL NVARCHAR(2000).', 1;

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
                    THROW 51114, 'Repair blocked: [dbo].[MessagingConversations] must have a single-column primary key on [ConversationId].', 1;

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
                    THROW 51115, 'Repair blocked: [dbo].[MessagingMessages] must have a single-column primary key on [MessageId].', 1;

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

                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_MessagingConversations_Consumers_BuyerUserId')
                    ALTER TABLE dbo.MessagingConversations WITH CHECK
                        ADD CONSTRAINT FK_MessagingConversations_Consumers_BuyerUserId
                        FOREIGN KEY (BuyerUserId) REFERENCES dbo.Consumers(consumer_id) ON DELETE NO ACTION;

                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_MessagingConversations_Sellers_SellerUserId')
                    ALTER TABLE dbo.MessagingConversations WITH CHECK
                        ADD CONSTRAINT FK_MessagingConversations_Sellers_SellerUserId
                        FOREIGN KEY (SellerUserId) REFERENCES dbo.Sellers(seller_id) ON DELETE NO ACTION;

                IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = N'FK_MessagingMessages_User_SenderUserId')
                    ALTER TABLE dbo.MessagingMessages WITH CHECK
                        ADD CONSTRAINT FK_MessagingMessages_User_SenderUserId
                        FOREIGN KEY (SenderUserId) REFERENCES dbo.Users(user_id) ON DELETE NO ACTION;

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

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[MessagingMessages]', N'U') AND name = N'IX_MessagingMessages_SenderUserId')
                    CREATE INDEX IX_MessagingMessages_SenderUserId ON dbo.MessagingMessages (SenderUserId);

                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE object_id = OBJECT_ID(N'[dbo].[MessagingMessages]', N'U') AND name = N'IX_MessagingMessages_ConversationId_SentAt')
                    CREATE INDEX IX_MessagingMessages_ConversationId_SentAt ON dbo.MessagingMessages (ConversationId ASC, SentAt DESC);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally non-destructive: this repair migration restores missing production tables.
        }
    }
}
