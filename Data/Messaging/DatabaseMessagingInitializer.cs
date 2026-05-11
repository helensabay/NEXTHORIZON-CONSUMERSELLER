using Microsoft.EntityFrameworkCore;

namespace MyAspNetApp.Data.Messaging;

public static class DatabaseMessagingInitializer
{
    public static async Task EnsureSchemaAsync(AppDbContext dbContext, CancellationToken cancellationToken = default)
    {
        const string sql =
            """
            IF OBJECT_ID(N'dbo.MessagingConversations', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.MessagingConversations
                (
                    ConversationId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    BuyerUserId INT NOT NULL,
                    SellerUserId INT NOT NULL,
                    ContextType TINYINT NOT NULL,
                    OrderId INT NULL,
                    LastMessageAt DATETIME2 NULL,
                    BuyerLastReadAt DATETIME2 NULL,
                    SellerLastReadAt DATETIME2 NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_MessagingConversations_CreatedAt DEFAULT SYSUTCDATETIME(),
                    UpdatedAt DATETIME2 NOT NULL CONSTRAINT DF_MessagingConversations_UpdatedAt DEFAULT SYSUTCDATETIME()
                );

                CREATE UNIQUE INDEX IX_MessagingConversations_General
                    ON dbo.MessagingConversations (BuyerUserId, SellerUserId, ContextType)
                    WHERE ContextType = 1;

                CREATE UNIQUE INDEX IX_MessagingConversations_Order
                    ON dbo.MessagingConversations (OrderId, ContextType)
                    WHERE ContextType = 2 AND OrderId IS NOT NULL;
            END;

            IF OBJECT_ID(N'dbo.MessagingMessages', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.MessagingMessages
                (
                    MessageId BIGINT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    ConversationId INT NOT NULL,
                    SenderUserId INT NOT NULL,
                    Body NVARCHAR(2000) NOT NULL CONSTRAINT DF_MessagingMessages_Body DEFAULT N'',
                    AttachmentUrl NVARCHAR(400) NULL,
                    SentAt DATETIME2 NOT NULL CONSTRAINT DF_MessagingMessages_SentAt DEFAULT SYSUTCDATETIME(),
                    IsDeleted BIT NOT NULL CONSTRAINT DF_MessagingMessages_IsDeleted DEFAULT 0
                );

                ALTER TABLE dbo.MessagingMessages
                ADD CONSTRAINT FK_MessagingMessages_Conversation
                    FOREIGN KEY (ConversationId) REFERENCES dbo.MessagingConversations(ConversationId)
                    ON DELETE CASCADE;

                CREATE INDEX IX_MessagingMessages_ConversationId_SentAt
                    ON dbo.MessagingMessages (ConversationId, SentAt DESC, MessageId DESC);
            END;

            IF OBJECT_ID(N'dbo.MessagingConversations', N'U') IS NOT NULL
               AND OBJECT_ID(N'dbo.MessagingMessages', N'U') IS NOT NULL
            BEGIN
                DECLARE @OrderConversations TABLE
                (
                    ConversationId INT NOT NULL PRIMARY KEY,
                    BuyerUserId INT NOT NULL,
                    SellerUserId INT NOT NULL,
                    TargetConversationId INT NULL
                );

                INSERT INTO @OrderConversations (ConversationId, BuyerUserId, SellerUserId, TargetConversationId)
                SELECT
                    orderConversation.ConversationId,
                    orderConversation.BuyerUserId,
                    orderConversation.SellerUserId,
                    generalConversation.ConversationId
                FROM dbo.MessagingConversations AS orderConversation
                OUTER APPLY
                (
                    SELECT TOP (1) existingGeneral.ConversationId
                    FROM dbo.MessagingConversations AS existingGeneral
                    WHERE existingGeneral.BuyerUserId = orderConversation.BuyerUserId
                      AND existingGeneral.SellerUserId = orderConversation.SellerUserId
                      AND existingGeneral.ContextType = 1
                    ORDER BY existingGeneral.ConversationId
                ) AS generalConversation
                WHERE orderConversation.ContextType = 2;

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
                SELECT
                    pending.BuyerUserId,
                    pending.SellerUserId,
                    1,
                    NULL,
                    NULL,
                    NULL,
                    NULL,
                    SYSUTCDATETIME(),
                    SYSUTCDATETIME()
                FROM @OrderConversations AS pending
                WHERE pending.TargetConversationId IS NULL
                GROUP BY pending.BuyerUserId, pending.SellerUserId;

                UPDATE pending
                SET TargetConversationId = generalConversation.ConversationId
                FROM @OrderConversations AS pending
                INNER JOIN dbo.MessagingConversations AS generalConversation
                    ON generalConversation.BuyerUserId = pending.BuyerUserId
                   AND generalConversation.SellerUserId = pending.SellerUserId
                   AND generalConversation.ContextType = 1
                WHERE pending.TargetConversationId IS NULL;

                UPDATE message
                SET ConversationId = pending.TargetConversationId
                FROM dbo.MessagingMessages AS message
                INNER JOIN @OrderConversations AS pending
                    ON pending.ConversationId = message.ConversationId
                WHERE pending.TargetConversationId IS NOT NULL
                  AND pending.TargetConversationId <> pending.ConversationId;

                UPDATE generalConversation
                SET LastMessageAt = latest.LatestSentAt,
                    UpdatedAt = COALESCE(latest.LatestSentAt, generalConversation.UpdatedAt)
                FROM dbo.MessagingConversations AS generalConversation
                INNER JOIN
                (
                    SELECT
                        message.ConversationId,
                        MAX(message.SentAt) AS LatestSentAt
                    FROM dbo.MessagingMessages AS message
                    GROUP BY message.ConversationId
                ) AS latest
                    ON latest.ConversationId = generalConversation.ConversationId
                WHERE generalConversation.ContextType = 1;

                DELETE orderConversation
                FROM dbo.MessagingConversations AS orderConversation
                INNER JOIN @OrderConversations AS pending
                    ON pending.ConversationId = orderConversation.ConversationId
                WHERE orderConversation.ContextType = 2;
            END;
            """;

        await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
    }
}
