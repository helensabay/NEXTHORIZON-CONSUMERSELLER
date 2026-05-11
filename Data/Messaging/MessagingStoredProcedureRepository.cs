using System.Data;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using MyAspNetApp.Models;
using MyAspNetApp.Models.Messaging;

namespace MyAspNetApp.Data.Messaging;

public sealed class MessagingStoredProcedureRepository : IMessagingRepository
{
    private readonly AppDbContext _dbContext;

    public MessagingStoredProcedureRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<MessageConversationSummary> CreateOrGetGeneralAsync(int buyerUserId, int sellerUserId, CancellationToken cancellationToken)
        => WithOpenConnectionAsync(async connection =>
        {
            var conversationId = await GetOrCreateConversationIdAsync(
                connection,
                buyerUserId,
                sellerUserId,
                ConversationContextType.General,
                null,
                cancellationToken);

            var summary = await GetConversationSnapshotAsync(connection, conversationId, cancellationToken);
            return summary ?? throw new InvalidOperationException("Conversation was created but could not be reloaded.");
        }, cancellationToken);

    public Task<MessageConversationSummary> CreateOrGetOrderAsync(int orderId, int buyerUserId, int sellerUserId, CancellationToken cancellationToken)
        => WithOpenConnectionAsync(async connection =>
        {
            var conversationId = await GetOrCreateConversationIdAsync(
                connection,
                buyerUserId,
                sellerUserId,
                ConversationContextType.General,
                null,
                cancellationToken);

            var summary = await GetConversationSnapshotAsync(connection, conversationId, cancellationToken);
            return summary ?? throw new InvalidOperationException("Order conversation was created but could not be reloaded.");
        }, cancellationToken);

    public Task<PagedResult<MessageConversationSummary>> ListByActorAsync(
        MessageActorContext actor,
        ConversationActorScope scope,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken)
        => WithOpenConnectionAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
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
                        c.UpdatedAt,
                        CASE
                            WHEN c.BuyerUserId = @ActorConsumerID THEN CAST(1 AS TINYINT)
                            WHEN c.SellerUserId = @ActorSellerID THEN CAST(2 AS TINYINT)
                            ELSE CAST(0 AS TINYINT)
                        END AS ActorSide
                    FROM dbo.MessagingConversations c
                    WHERE (
                            @ActorScope = 2
                            AND c.SellerUserId = @ActorSellerID
                        )
                       OR (
                            @ActorScope = 1
                            AND c.BuyerUserId = @ActorConsumerID
                        )
                       OR (
                            @ActorScope = 0
                            AND (
                                    c.BuyerUserId = @ActorConsumerID
                                 OR c.SellerUserId = @ActorSellerID
                            )
                        )
                      AND (
                            c.ContextType = 1
                         OR NOT EXISTS
                            (
                                SELECT 1
                                FROM dbo.MessagingConversations general
                                WHERE general.BuyerUserId = c.BuyerUserId
                                  AND general.SellerUserId = c.SellerUserId
                                  AND general.ContextType = 1
                            )
                      )
                )
                SELECT
                    b.ConversationId AS ConversationID,
                    b.BuyerUserId AS BuyerUserID,
                    b.SellerUserId AS SellerUserID,
                    b.ContextType AS ContextType,
                    b.OrderId AS OrderID,
                    COALESCE(lm.SentAt, b.LastMessageAt) AS LastMessageAt,
                    b.BuyerLastReadAt AS BuyerLastReadAt,
                    b.SellerLastReadAt AS SellerLastReadAt,
                    CASE
                        WHEN lm.MessageId IS NULL THEN NULL
                        WHEN lm.IsDeleted = 1 THEN N'[deleted]'
                        WHEN NULLIF(LTRIM(RTRIM(lm.Body)), N'') IS NULL
                             AND (lm.AttachmentUrl IS NOT NULL OR lm.AttachmentData IS NOT NULL) THEN N'Attachment sent'
                        WHEN LEN(lm.Body) > 120 THEN LEFT(lm.Body, 117) + N'...'
                        ELSE lm.Body
                    END AS LastMessagePreview,
                    (
                        SELECT COUNT(1)
                        FROM dbo.MessagingMessages m
                        INNER JOIN dbo.MessagingConversations mc
                            ON mc.ConversationId = m.ConversationId
                        WHERE mc.BuyerUserId = b.BuyerUserId
                          AND mc.SellerUserId = b.SellerUserId
                          AND m.IsDeleted = 0
                          AND m.SenderUserId <> @ActorUserID
                          AND m.SentAt > COALESCE(
                                CASE
                                    WHEN b.ActorSide = 1 THEN b.BuyerLastReadAt
                                    WHEN b.ActorSide = 2 THEN b.SellerLastReadAt
                                    ELSE NULL
                                END,
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
                        m.AttachmentData,
                        m.AttachmentUrl,
                        m.IsDeleted,
                        m.SentAt
                    FROM dbo.MessagingMessages m
                    INNER JOIN dbo.MessagingConversations mc
                        ON mc.ConversationId = m.ConversationId
                    WHERE mc.BuyerUserId = b.BuyerUserId
                      AND mc.SellerUserId = b.SellerUserId
                    ORDER BY m.SentAt DESC, m.MessageId DESC
                ) lm
                ORDER BY COALESCE(lm.SentAt, b.LastMessageAt, b.CreatedAt) DESC, b.ConversationId DESC
                OFFSET (@Page - 1) * @PageSize ROWS
                FETCH NEXT @PageSize ROWS ONLY;

                ;WITH Base AS
                (
                    SELECT c.ConversationId
                    FROM dbo.MessagingConversations c
                    WHERE (
                            @ActorScope = 2
                            AND c.SellerUserId = @ActorSellerID
                        )
                       OR (
                            @ActorScope = 1
                            AND c.BuyerUserId = @ActorConsumerID
                        )
                       OR (
                            @ActorScope = 0
                            AND (
                                    c.BuyerUserId = @ActorConsumerID
                                 OR c.SellerUserId = @ActorSellerID
                            )
                        )
                      AND (
                            c.ContextType = 1
                         OR NOT EXISTS
                            (
                                SELECT 1
                                FROM dbo.MessagingConversations general
                                WHERE general.BuyerUserId = c.BuyerUserId
                                  AND general.SellerUserId = c.SellerUserId
                                  AND general.ContextType = 1
                            )
                      )
                )
                SELECT COUNT(1) AS TotalCount
                FROM Base;
                """;
            command.CommandType = CommandType.Text;

            AddParameter(command, "@ActorUserID", actor.UserId, DbType.Int32);
            AddParameter(command, "@ActorConsumerID", actor.ConsumerId, DbType.Int32);
            AddParameter(command, "@ActorSellerID", ResolveActorSellerId(actor), DbType.Int32);
            AddParameter(command, "@ActorScope", (int)scope, DbType.Int32);
            AddParameter(command, "@Page", pageNumber, DbType.Int32);
            AddParameter(command, "@PageSize", pageSize, DbType.Int32);

            var items = new List<MessageConversationSummary>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(MapConversation(reader));
            }

            var totalCount = 0;
            if (await reader.NextResultAsync(cancellationToken) && await reader.ReadAsync(cancellationToken))
            {
                totalCount = reader.GetInt32(reader.GetOrdinal("TotalCount"));
            }

            return new PagedResult<MessageConversationSummary>
            {
                PageNumber = pageNumber,
                PageSize = pageSize,
                TotalCount = totalCount,
                Items = items,
            };
        }, cancellationToken);

    public Task<MessageConversationSummary?> FindConversationAsync(
        MessageActorContext actor,
        ConversationContextType contextType,
        int? sellerUserId,
        int? orderId,
        CancellationToken cancellationToken)
        => WithOpenConnectionAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT TOP (1)
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
                        WHEN NULLIF(LTRIM(RTRIM(lm.Body)), N'') IS NULL
                             AND (lm.AttachmentUrl IS NOT NULL OR lm.AttachmentData IS NOT NULL) THEN N'Attachment sent'
                        WHEN LEN(lm.Body) > 120 THEN LEFT(lm.Body, 117) + N'...'
                        ELSE lm.Body
                    END AS LastMessagePreview,
                    (
                        SELECT COUNT(1)
                        FROM dbo.MessagingMessages m
                        WHERE m.ConversationId = c.ConversationId
                          AND m.IsDeleted = 0
                          AND m.SenderUserId <> @ActorUserID
                          AND m.SentAt > COALESCE(
                                CASE
                                    WHEN c.BuyerUserId = @ActorConsumerID THEN c.BuyerLastReadAt
                                    WHEN c.SellerUserId = @ActorSellerID THEN c.SellerLastReadAt
                                    ELSE NULL
                                END,
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
                        m.AttachmentData,
                        m.AttachmentUrl,
                        m.IsDeleted
                    FROM dbo.MessagingMessages m
                    WHERE m.ConversationId = c.ConversationId
                    ORDER BY m.SentAt DESC, m.MessageId DESC
                ) lm
                WHERE (
                        c.BuyerUserId = @ActorConsumerID
                     OR c.SellerUserId = @ActorSellerID
                  )
                  AND (@SellerUserID IS NULL OR c.SellerUserId = @SellerUserID)
                  AND (
                        (@ContextType = 1 AND c.OrderId IS NULL)
                     OR (@ContextType = 2 AND (c.ContextType = 1 OR c.OrderId = @OrderID))
                  )
                ORDER BY
                    CASE WHEN c.ContextType = 1 THEN 0 ELSE 1 END,
                    c.ConversationId DESC;
                """;
            command.CommandType = CommandType.Text;

            AddParameter(command, "@ActorUserID", actor.UserId, DbType.Int32);
            AddParameter(command, "@ActorConsumerID", actor.ConsumerId, DbType.Int32);
            AddParameter(command, "@ActorSellerID", ResolveActorSellerId(actor), DbType.Int32);
            AddParameter(command, "@ContextType", (byte)contextType, DbType.Byte);
            AddParameter(command, "@SellerUserID", sellerUserId, DbType.Int32);
            AddParameter(command, "@OrderID", orderId, DbType.Int32);

            return await ReadSingleConversationAsync(command, cancellationToken);
        }, cancellationToken);

    public Task<MessageConversationSummary?> GetConversationAsync(int conversationId, MessageActorContext actor, CancellationToken cancellationToken)
        => WithOpenConnectionAsync(connection => GetConversationForActorAsync(connection, conversationId, actor, cancellationToken), cancellationToken);

    public Task<MessageItem?> SendMessageAsync(
        int conversationId,
        MessageActorContext actor,
        string body,
        byte[]? attachmentData,
        string? attachmentContentType,
        string? attachmentFileName,
        CancellationToken cancellationToken)
        => WithOpenConnectionAsync(async connection =>
        {
            if (!await IsConversationParticipantAsync(connection, conversationId, actor, cancellationToken))
            {
                return null;
            }

            var targetConversationId = await ResolveCanonicalConversationIdAsync(connection, conversationId, cancellationToken);

            await using var insertCommand = connection.CreateCommand();
            insertCommand.CommandText =
                """
                DECLARE @Inserted TABLE
                (
                    MessageID BIGINT,
                    ConversationID INT,
                    SenderUserID INT,
                    Body NVARCHAR(2000),
                    AttachmentUrl NVARCHAR(400),
                    AttachmentContentType NVARCHAR(200),
                    AttachmentFileName NVARCHAR(510),
                    SentAt DATETIME2,
                    IsDeleted BIT
                );

                INSERT INTO dbo.MessagingMessages
                (
                    ConversationId,
                    SenderUserId,
                    Body,
                    AttachmentData,
                    AttachmentContentType,
                    AttachmentFileName,
                    AttachmentUrl,
                    SentAt,
                    IsDeleted
                )
                OUTPUT
                    INSERTED.MessageId,
                    INSERTED.ConversationId,
                    INSERTED.SenderUserId,
                    INSERTED.Body,
                    INSERTED.AttachmentUrl,
                    INSERTED.AttachmentContentType,
                    INSERTED.AttachmentFileName,
                    INSERTED.SentAt,
                    INSERTED.IsDeleted
                INTO @Inserted
                VALUES
                (
                    @ConversationID,
                    @SenderUserID,
                    @Body,
                    @AttachmentData,
                    @AttachmentContentType,
                    @AttachmentFileName,
                    @AttachmentUrl,
                    SYSDATETIME(),
                    0
                );

                SELECT
                    MessageID,
                    ConversationID,
                    SenderUserID,
                    Body,
                    AttachmentUrl,
                    AttachmentContentType,
                    AttachmentFileName,
                    SentAt,
                    IsDeleted
                FROM @Inserted;
                """;
            insertCommand.CommandType = CommandType.Text;

            AddParameter(insertCommand, "@ConversationID", targetConversationId, DbType.Int32);
            AddParameter(insertCommand, "@SenderUserID", actor.UserId, DbType.Int32);
            AddParameter(insertCommand, "@Body", body ?? string.Empty, DbType.String);
            AddParameter(insertCommand, "@AttachmentData", attachmentData, DbType.Binary);
            AddParameter(insertCommand, "@AttachmentContentType", attachmentContentType, DbType.String);
            AddParameter(insertCommand, "@AttachmentFileName", attachmentFileName, DbType.String);
            AddParameter(insertCommand, "@AttachmentUrl", null, DbType.String);

            await using var reader = await insertCommand.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            var item = MapMessage(reader);
            await reader.CloseAsync();

            await using var updateCommand = connection.CreateCommand();
            updateCommand.CommandText =
                """
                UPDATE dbo.MessagingConversations
                SET LastMessageAt = @SentAt,
                    UpdatedAt = @SentAt
                WHERE ConversationId = @ConversationID;
                """;
            updateCommand.CommandType = CommandType.Text;

            AddParameter(updateCommand, "@ConversationID", targetConversationId, DbType.Int32);
            AddParameter(updateCommand, "@SentAt", item.SentAt, DbType.DateTime2);

            await updateCommand.ExecuteNonQueryAsync(cancellationToken);
            return item;
        }, cancellationToken);

    public Task<IReadOnlyList<MessageItem>?> ListMessagesAsync(int conversationId, MessageActorContext actor, DateTime? before, int pageSize, CancellationToken cancellationToken)
        => WithOpenConnectionAsync(async connection =>
        {
            if (!await IsConversationParticipantAsync(connection, conversationId, actor, cancellationToken))
            {
                return null;
            }

            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT TOP (@PageSize)
                    MessageId AS MessageID,
                    ConversationId AS ConversationID,
                    SenderUserId AS SenderUserID,
                    Body AS Body,
                    COALESCE(
                        NULLIF(AttachmentUrl, N''),
                        CASE
                            WHEN AttachmentData IS NOT NULL THEN CONCAT(N'/api/messages/attachments/', MessageId)
                            ELSE NULL
                        END
                    ) AS AttachmentUrl,
                    AttachmentContentType AS AttachmentContentType,
                    AttachmentFileName AS AttachmentFileName,
                    SentAt AS SentAt,
                    IsDeleted AS IsDeleted
                FROM dbo.MessagingMessages
                WHERE ConversationId IN
                (
                    SELECT related.ConversationId
                    FROM dbo.MessagingConversations currentConversation
                    INNER JOIN dbo.MessagingConversations related
                        ON related.BuyerUserId = currentConversation.BuyerUserId
                       AND related.SellerUserId = currentConversation.SellerUserId
                    WHERE currentConversation.ConversationId = @ConversationID
                )
                  AND (@Before IS NULL OR SentAt < @Before)
                ORDER BY SentAt DESC, MessageId DESC;
                """;
            command.CommandType = CommandType.Text;

            AddParameter(command, "@ConversationID", conversationId, DbType.Int32);
            AddParameter(command, "@Before", before, DbType.DateTime2);
            AddParameter(command, "@PageSize", pageSize, DbType.Int32);

            var items = new List<MessageItem>();
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                items.Add(MapMessage(reader));
            }

            items.Reverse();
            return (IReadOnlyList<MessageItem>)items;
        }, cancellationToken);

    public Task<bool> MarkReadAsync(int conversationId, MessageActorContext actor, CancellationToken cancellationToken)
        => WithOpenConnectionAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE dbo.MessagingConversations
                SET BuyerLastReadAt = CASE
                        WHEN BuyerUserId = @ActorConsumerID THEN SYSDATETIME()
                        ELSE BuyerLastReadAt
                    END,
                    SellerLastReadAt = CASE
                        WHEN SellerUserId = @ActorSellerID THEN SYSDATETIME()
                        ELSE SellerLastReadAt
                    END,
                    UpdatedAt = SYSDATETIME()
                WHERE ConversationId IN
                (
                    SELECT related.ConversationId
                    FROM dbo.MessagingConversations currentConversation
                    INNER JOIN dbo.MessagingConversations related
                        ON related.BuyerUserId = currentConversation.BuyerUserId
                       AND related.SellerUserId = currentConversation.SellerUserId
                    WHERE currentConversation.ConversationId = @ConversationID
                )
                  AND (
                        BuyerUserId = @ActorConsumerID
                     OR SellerUserId = @ActorSellerID
                  );
                """;
            command.CommandType = CommandType.Text;

            AddParameter(command, "@ConversationID", conversationId, DbType.Int32);
            AddParameter(command, "@ActorUserID", actor.UserId, DbType.Int32);
            AddParameter(command, "@ActorConsumerID", actor.ConsumerId, DbType.Int32);
            AddParameter(command, "@ActorSellerID", ResolveActorSellerId(actor), DbType.Int32);

            return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        }, cancellationToken);

    public Task<bool> SoftDeleteMessageAsync(long messageId, int userId, CancellationToken cancellationToken)
        => WithOpenConnectionAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE dbo.MessagingMessages
                SET IsDeleted = 1
                WHERE MessageId = @MessageID
                  AND SenderUserId = @UserID
                  AND IsDeleted = 0;
                """;
            command.CommandType = CommandType.Text;

            AddParameter(command, "@MessageID", messageId, DbType.Int64);
            AddParameter(command, "@UserID", userId, DbType.Int32);

            return await command.ExecuteNonQueryAsync(cancellationToken) > 0;
        }, cancellationToken);

    private async Task<int> GetOrCreateConversationIdAsync(
        DbConnection connection,
        int buyerUserId,
        int sellerUserId,
        ConversationContextType contextType,
        int? orderId,
        CancellationToken cancellationToken)
    {
        await using var lookupCommand = connection.CreateCommand();
        lookupCommand.CommandText = contextType == ConversationContextType.General
            ? """
              SELECT TOP (1) ConversationId
              FROM dbo.MessagingConversations
              WHERE BuyerUserId = @BuyerUserID
                AND SellerUserId = @SellerUserID
                AND ContextType = 1;
              """
            : """
              SELECT TOP (1) ConversationId
              FROM dbo.MessagingConversations
              WHERE OrderId = @OrderID
                AND ContextType = 2;
              """;
        lookupCommand.CommandType = CommandType.Text;

        AddParameter(lookupCommand, "@BuyerUserID", buyerUserId, DbType.Int32);
        AddParameter(lookupCommand, "@SellerUserID", sellerUserId, DbType.Int32);
        AddParameter(lookupCommand, "@OrderID", orderId, DbType.Int32);

        var existingId = await lookupCommand.ExecuteScalarAsync(cancellationToken);
        if (existingId is not null && existingId != DBNull.Value)
        {
            return Convert.ToInt32(existingId);
        }

        await using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText =
            """
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
            OUTPUT INSERTED.ConversationId
            VALUES
            (
                @BuyerUserID,
                @SellerUserID,
                @ContextType,
                @OrderID,
                NULL,
                NULL,
                NULL,
                SYSDATETIME(),
                SYSDATETIME()
            );
            """;
        insertCommand.CommandType = CommandType.Text;

        AddParameter(insertCommand, "@BuyerUserID", buyerUserId, DbType.Int32);
        AddParameter(insertCommand, "@SellerUserID", sellerUserId, DbType.Int32);
        AddParameter(insertCommand, "@ContextType", (byte)contextType, DbType.Byte);
        AddParameter(insertCommand, "@OrderID", orderId, DbType.Int32);

        var insertedId = await insertCommand.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(insertedId);
    }

    private async Task<MessageConversationSummary?> GetConversationForActorAsync(
        DbConnection connection,
        int conversationId,
        MessageActorContext actor,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT TOP (1)
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
                    WHEN NULLIF(LTRIM(RTRIM(lm.Body)), N'') IS NULL
                         AND (lm.AttachmentUrl IS NOT NULL OR lm.AttachmentData IS NOT NULL) THEN N'Attachment sent'
                    WHEN LEN(lm.Body) > 120 THEN LEFT(lm.Body, 117) + N'...'
                    ELSE lm.Body
                END AS LastMessagePreview,
                (
                    SELECT COUNT(1)
                    FROM dbo.MessagingMessages m
                    WHERE m.ConversationId = c.ConversationId
                      AND m.IsDeleted = 0
                      AND m.SenderUserId <> @ActorUserID
                      AND m.SentAt > COALESCE(
                            CASE
                                WHEN c.BuyerUserId = @ActorConsumerID THEN c.BuyerLastReadAt
                                WHEN c.SellerUserId = @ActorSellerID THEN c.SellerLastReadAt
                                ELSE NULL
                            END,
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
                    m.AttachmentData,
                    m.AttachmentUrl,
                    m.IsDeleted
                FROM dbo.MessagingMessages m
                WHERE m.ConversationId = c.ConversationId
                ORDER BY m.SentAt DESC, m.MessageId DESC
            ) lm
            WHERE c.ConversationId = @ConversationID
              AND (
                    c.BuyerUserId = @ActorConsumerID
                 OR c.SellerUserId = @ActorSellerID
              );
            """;
        command.CommandType = CommandType.Text;

        AddParameter(command, "@ConversationID", conversationId, DbType.Int32);
        AddParameter(command, "@ActorUserID", actor.UserId, DbType.Int32);
        AddParameter(command, "@ActorConsumerID", actor.ConsumerId, DbType.Int32);
        AddParameter(command, "@ActorSellerID", ResolveActorSellerId(actor), DbType.Int32);

        return await ReadSingleConversationAsync(command, cancellationToken);
    }

    private async Task<MessageConversationSummary?> GetConversationSnapshotAsync(
        DbConnection connection,
        int conversationId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT TOP (1)
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
                    WHEN NULLIF(LTRIM(RTRIM(lm.Body)), N'') IS NULL
                         AND (lm.AttachmentUrl IS NOT NULL OR lm.AttachmentData IS NOT NULL) THEN N'Attachment sent'
                    WHEN LEN(lm.Body) > 120 THEN LEFT(lm.Body, 117) + N'...'
                    ELSE lm.Body
                END AS LastMessagePreview,
                0 AS UnreadCount,
                c.CreatedAt AS CreatedAt,
                c.UpdatedAt AS UpdatedAt
            FROM dbo.MessagingConversations c
            OUTER APPLY
            (
                SELECT TOP (1)
                    m.MessageId,
                    m.Body,
                    m.AttachmentData,
                    m.AttachmentUrl,
                    m.IsDeleted
                FROM dbo.MessagingMessages m
                WHERE m.ConversationId = c.ConversationId
                ORDER BY m.SentAt DESC, m.MessageId DESC
            ) lm
            WHERE c.ConversationId = @ConversationID;
            """;
        command.CommandType = CommandType.Text;
        AddParameter(command, "@ConversationID", conversationId, DbType.Int32);
        return await ReadSingleConversationAsync(command, cancellationToken);
    }

    private static async Task<MessageConversationSummary?> ReadSingleConversationAsync(DbCommand command, CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? MapConversation(reader)
            : null;
    }

    private async Task<int> ResolveCanonicalConversationIdAsync(DbConnection connection, int conversationId, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT TOP (1) canonical.ConversationId
            FROM dbo.MessagingConversations currentConversation
            INNER JOIN dbo.MessagingConversations canonical
                ON canonical.BuyerUserId = currentConversation.BuyerUserId
               AND canonical.SellerUserId = currentConversation.SellerUserId
            WHERE currentConversation.ConversationId = @ConversationID
            ORDER BY
                CASE WHEN canonical.ContextType = 1 THEN 0 ELSE 1 END,
                canonical.ConversationId ASC;
            """;
        command.CommandType = CommandType.Text;

        AddParameter(command, "@ConversationID", conversationId, DbType.Int32);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is null || result == DBNull.Value ? conversationId : Convert.ToInt32(result);
    }

    private async Task<bool> IsConversationParticipantAsync(DbConnection connection, int conversationId, MessageActorContext actor, CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT COUNT(1)
            FROM dbo.MessagingConversations
            WHERE ConversationId = @ConversationID
              AND (
                    BuyerUserId = @ActorConsumerID
                 OR SellerUserId = @ActorSellerID
              );
            """;
        command.CommandType = CommandType.Text;

        AddParameter(command, "@ConversationID", conversationId, DbType.Int32);
        AddParameter(command, "@ActorUserID", actor.UserId, DbType.Int32);
        AddParameter(command, "@ActorConsumerID", actor.ConsumerId, DbType.Int32);
        AddParameter(command, "@ActorSellerID", ResolveActorSellerId(actor), DbType.Int32);

        var scalar = await command.ExecuteScalarAsync(cancellationToken);
        return scalar is not null && scalar != DBNull.Value && Convert.ToInt32(scalar) > 0;
    }

    private static int? ResolveActorSellerId(MessageActorContext actor)
        => actor.SellerId ?? actor.UserId;

    private static MessageConversationSummary MapConversation(DbDataReader reader)
        => new(
            reader.GetInt32(reader.GetOrdinal("ConversationID")),
            reader.GetInt32(reader.GetOrdinal("BuyerUserID")),
            reader.GetInt32(reader.GetOrdinal("SellerUserID")),
            (ConversationContextType)reader.GetByte(reader.GetOrdinal("ContextType")),
            reader.IsDBNull(reader.GetOrdinal("OrderID")) ? null : reader.GetInt32(reader.GetOrdinal("OrderID")),
            reader.IsDBNull(reader.GetOrdinal("LastMessageAt")) ? null : reader.GetDateTime(reader.GetOrdinal("LastMessageAt")),
            reader.IsDBNull(reader.GetOrdinal("BuyerLastReadAt")) ? null : reader.GetDateTime(reader.GetOrdinal("BuyerLastReadAt")),
            reader.IsDBNull(reader.GetOrdinal("SellerLastReadAt")) ? null : reader.GetDateTime(reader.GetOrdinal("SellerLastReadAt")),
            reader.IsDBNull(reader.GetOrdinal("LastMessagePreview")) ? null : reader.GetString(reader.GetOrdinal("LastMessagePreview")),
            reader.GetInt32(reader.GetOrdinal("UnreadCount")),
            reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            reader.GetDateTime(reader.GetOrdinal("UpdatedAt")));

    private static MessageItem MapMessage(DbDataReader reader)
        => new(
            reader.GetInt64(reader.GetOrdinal("MessageID")),
            reader.GetInt32(reader.GetOrdinal("ConversationID")),
            reader.GetInt32(reader.GetOrdinal("SenderUserID")),
            reader.IsDBNull(reader.GetOrdinal("Body")) ? null : reader.GetString(reader.GetOrdinal("Body")),
            reader.IsDBNull(reader.GetOrdinal("AttachmentUrl")) ? null : reader.GetString(reader.GetOrdinal("AttachmentUrl")),
            reader.IsDBNull(reader.GetOrdinal("AttachmentContentType")) ? null : reader.GetString(reader.GetOrdinal("AttachmentContentType")),
            reader.IsDBNull(reader.GetOrdinal("AttachmentFileName")) ? null : reader.GetString(reader.GetOrdinal("AttachmentFileName")),
            reader.GetDateTime(reader.GetOrdinal("SentAt")),
            reader.GetBoolean(reader.GetOrdinal("IsDeleted")));

    private async Task<T> WithOpenConnectionAsync<T>(
        Func<DbConnection, Task<T>> action,
        CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;
        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            return await action(connection);
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static void AddParameter(DbCommand command, string name, object? value, DbType dbType)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }
}
