using System.Data;
using System.Data.Common;
using NextHorizon.Data;
using NextHorizon.Models;
using NextHorizon.Messaging.Models;
using Microsoft.EntityFrameworkCore;

namespace NextHorizon.Data.Messaging;

public sealed class MessagingStoredProcedureRepository : IMessagingRepository
{
    private readonly AppDbContext _dbContext;

    public MessagingStoredProcedureRepository(AppDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<MessageConversationSummary> CreateOrGetGeneralAsync(int buyerConsumerId, int sellerId, CancellationToken cancellationToken)
        => WithOpenConnectionAsync(async connection =>
        {
            var conversationId = await GetOrCreateConversationIdAsync(
                connection,
                buyerConsumerId,
                sellerId,
                ConversationContextType.General,
                null,
                cancellationToken);

            var summary = await GetConversationSnapshotAsync(connection, conversationId, cancellationToken);
            return summary ?? throw new InvalidOperationException("Conversation was created but could not be reloaded.");
        }, cancellationToken);

    public Task<MessageConversationSummary> CreateOrGetOrderAsync(int orderId, int buyerConsumerId, int sellerId, CancellationToken cancellationToken)
        => WithOpenConnectionAsync(async connection =>
        {
            var conversationId = await GetOrCreateConversationIdAsync(
                connection,
                buyerConsumerId,
                sellerId,
                ConversationContextType.Order,
                orderId,
                cancellationToken);

            var summary = await GetConversationSnapshotAsync(connection, conversationId, cancellationToken);
            return summary ?? throw new InvalidOperationException("Conversation was created but could not be reloaded.");
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
                            WHEN @ActorConsumerID IS NOT NULL AND c.BuyerUserId = @ActorConsumerID THEN CAST(1 AS TINYINT)
                            WHEN @ActorSellerID IS NOT NULL AND c.SellerUserId = @ActorSellerID THEN CAST(2 AS TINYINT)
                            ELSE CAST(0 AS TINYINT)
                        END AS ActorSide
                    FROM dbo.MessagingConversations c
                    WHERE (
                            @ActorScope = 2
                            AND @ActorSellerID IS NOT NULL
                            AND c.SellerUserId = @ActorSellerID
                        )
                       OR (
                            @ActorScope = 1
                            AND @ActorConsumerID IS NOT NULL
                            AND c.BuyerUserId = @ActorConsumerID
                        )
                       OR (
                            @ActorScope = 0
                            AND (
                                    (@ActorConsumerID IS NOT NULL AND c.BuyerUserId = @ActorConsumerID)
                                 OR (@ActorSellerID IS NOT NULL AND c.SellerUserId = @ActorSellerID)
                            )
                        )
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
                        m.IsDeleted
                    FROM dbo.MessagingMessages m
                    WHERE m.ConversationId = b.ConversationId
                    ORDER BY m.MessageId DESC
                ) lm
                ORDER BY COALESCE(b.LastMessageAt, b.CreatedAt) DESC, b.ConversationId DESC
                OFFSET (@Page - 1) * @PageSize ROWS
                FETCH NEXT @PageSize ROWS ONLY;

                ;WITH Base AS
                (
                    SELECT c.ConversationId
                    FROM dbo.MessagingConversations c
                    WHERE (
                            @ActorScope = 2
                            AND @ActorSellerID IS NOT NULL
                            AND c.SellerUserId = @ActorSellerID
                        )
                       OR (
                            @ActorScope = 1
                            AND @ActorConsumerID IS NOT NULL
                            AND c.BuyerUserId = @ActorConsumerID
                        )
                       OR (
                            @ActorScope = 0
                            AND (
                                    (@ActorConsumerID IS NOT NULL AND c.BuyerUserId = @ActorConsumerID)
                                 OR (@ActorSellerID IS NOT NULL AND c.SellerUserId = @ActorSellerID)
                            )
                        )
                )
                SELECT COUNT(1) AS TotalCount
                FROM Base;
                """;
            command.CommandType = CommandType.Text;

            AddActorParameters(command, actor);
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
        int? sellerId,
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
                                    WHEN @ActorConsumerID IS NOT NULL AND c.BuyerUserId = @ActorConsumerID THEN c.BuyerLastReadAt
                                    WHEN @ActorSellerID IS NOT NULL AND c.SellerUserId = @ActorSellerID THEN c.SellerLastReadAt
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
                        m.IsDeleted
                    FROM dbo.MessagingMessages m
                    WHERE m.ConversationId = c.ConversationId
                    ORDER BY m.MessageId DESC
                ) lm
                WHERE c.ContextType = @ContextType
                  AND (
                        (@ActorConsumerID IS NOT NULL AND c.BuyerUserId = @ActorConsumerID)
                     OR (@ActorSellerID IS NOT NULL AND c.SellerUserId = @ActorSellerID)
                  )
                  AND (@SellerID IS NULL OR c.SellerUserId = @SellerID)
                  AND (
                        (@ContextType = 1 AND c.OrderId IS NULL)
                     OR (@ContextType = 2 AND c.OrderId = @OrderID)
                  )
                ORDER BY c.ConversationId DESC;
                """;
            command.CommandType = CommandType.Text;

            AddActorParameters(command, actor);
            AddParameter(command, "@ContextType", (byte)contextType, DbType.Byte);
            AddParameter(command, "@SellerID", sellerId, DbType.Int32);
            AddParameter(command, "@OrderID", orderId, DbType.Int32);

            return await ReadSingleConversationAsync(command, cancellationToken);
        }, cancellationToken);

    public Task<MessageConversationSummary?> GetConversationAsync(int conversationId, MessageActorContext actor, CancellationToken cancellationToken)
        => WithOpenConnectionAsync(connection => GetConversationForActorAsync(connection, conversationId, actor, cancellationToken), cancellationToken);

    public Task<MessageItem?> SendMessageAsync(int conversationId, MessageActorContext actor, string body, MessageAttachmentWriteModel? attachment, CancellationToken cancellationToken)
        => WithOpenConnectionAsync(async connection =>
        {
            if (!await IsConversationParticipantAsync(connection, conversationId, actor, cancellationToken))
            {
                return null;
            }

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
                    HasStoredAttachment BIT,
                    SentAt DATETIME2,
                    IsDeleted BIT
                );

                INSERT INTO dbo.MessagingMessages
                (
                    ConversationId,
                    SenderUserId,
                    Body,
                    AttachmentUrl,
                    AttachmentData,
                    AttachmentContentType,
                    AttachmentFileName,
                    SentAt,
                    IsDeleted
                )
                OUTPUT
                    INSERTED.MessageId,
                    INSERTED.ConversationId,
                    INSERTED.SenderUserId,
                    INSERTED.Body,
                    INSERTED.AttachmentUrl,
                    CASE WHEN INSERTED.AttachmentData IS NULL OR DATALENGTH(INSERTED.AttachmentData) = 0 THEN CAST(0 AS BIT) ELSE CAST(1 AS BIT) END,
                    INSERTED.SentAt,
                    INSERTED.IsDeleted
                INTO @Inserted
                VALUES
                (
                    @ConversationID,
                    @SenderUserID,
                    @Body,
                    NULL,
                    @AttachmentData,
                    @AttachmentContentType,
                    @AttachmentFileName,
                    SYSDATETIME(),
                    0
                );

                SELECT
                    MessageID,
                    ConversationID,
                    SenderUserID,
                    Body,
                    AttachmentUrl,
                    HasStoredAttachment,
                    SentAt,
                    IsDeleted
                FROM @Inserted;
                """;
            insertCommand.CommandType = CommandType.Text;

            AddParameter(insertCommand, "@ConversationID", conversationId, DbType.Int32);
            AddParameter(insertCommand, "@SenderUserID", actor.UserId, DbType.Int32);
            AddParameter(insertCommand, "@Body", body, DbType.String);
            AddParameter(insertCommand, "@AttachmentData", attachment?.Data, DbType.Binary);
            AddParameter(insertCommand, "@AttachmentContentType", attachment?.ContentType, DbType.String);
            AddParameter(insertCommand, "@AttachmentFileName", attachment?.FileName, DbType.String);

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

            AddParameter(updateCommand, "@ConversationID", conversationId, DbType.Int32);
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
                    AttachmentUrl AS AttachmentUrl,
                    CASE WHEN AttachmentData IS NULL OR DATALENGTH(AttachmentData) = 0 THEN CAST(0 AS BIT) ELSE CAST(1 AS BIT) END AS HasStoredAttachment,
                    SentAt AS SentAt,
                    IsDeleted AS IsDeleted
                FROM dbo.MessagingMessages
                WHERE ConversationId = @ConversationID
                  AND (@Before IS NULL OR SentAt < @Before)
                ORDER BY MessageId DESC;
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

    public Task<MessageAttachmentReadModel?> GetMessageAttachmentAsync(long messageId, MessageActorContext actor, CancellationToken cancellationToken)
        => WithOpenConnectionAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT TOP (1)
                    m.MessageId AS MessageID,
                    m.AttachmentContentType AS AttachmentContentType,
                    m.AttachmentFileName AS AttachmentFileName,
                    m.AttachmentData AS AttachmentData
                FROM dbo.MessagingMessages m
                INNER JOIN dbo.MessagingConversations c ON c.ConversationId = m.ConversationId
                WHERE m.MessageId = @MessageID
                  AND m.AttachmentData IS NOT NULL
                  AND DATALENGTH(m.AttachmentData) > 0
                  AND (
                        (@ActorConsumerID IS NOT NULL AND c.BuyerUserId = @ActorConsumerID)
                     OR (@ActorSellerID IS NOT NULL AND c.SellerUserId = @ActorSellerID)
                  );
                """;
            command.CommandType = CommandType.Text;

            AddParameter(command, "@MessageID", messageId, DbType.Int64);
            AddActorParameters(command, actor);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return MapAttachment(reader);
        }, cancellationToken);

    public Task<bool> MarkReadAsync(int conversationId, MessageActorContext actor, CancellationToken cancellationToken)
        => WithOpenConnectionAsync(async connection =>
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                UPDATE dbo.MessagingConversations
                SET BuyerLastReadAt = CASE
                        WHEN @ActorConsumerID IS NOT NULL AND BuyerUserId = @ActorConsumerID THEN SYSDATETIME()
                        ELSE BuyerLastReadAt
                    END,
                    SellerLastReadAt = CASE
                        WHEN @ActorSellerID IS NOT NULL AND SellerUserId = @ActorSellerID THEN SYSDATETIME()
                        ELSE SellerLastReadAt
                    END,
                    UpdatedAt = SYSDATETIME()
                WHERE ConversationId = @ConversationID
                  AND (
                        (@ActorConsumerID IS NOT NULL AND BuyerUserId = @ActorConsumerID)
                     OR (@ActorSellerID IS NOT NULL AND SellerUserId = @ActorSellerID)
                  );
                """;
            command.CommandType = CommandType.Text;

            AddParameter(command, "@ConversationID", conversationId, DbType.Int32);
            AddActorParameters(command, actor);

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
        int buyerConsumerId,
        int sellerId,
        ConversationContextType contextType,
        int? orderId,
        CancellationToken cancellationToken)
    {
        await using var lookupCommand = connection.CreateCommand();
        lookupCommand.CommandText = contextType == ConversationContextType.General
            ? """
              SELECT TOP (1) ConversationId
              FROM dbo.MessagingConversations
              WHERE BuyerUserId = @BuyerConsumerID
                AND SellerUserId = @SellerID
                AND ContextType = 1;
              """
            : """
              SELECT TOP (1) ConversationId
              FROM dbo.MessagingConversations
              WHERE OrderId = @OrderID
                AND ContextType = 2;
              """;
        lookupCommand.CommandType = CommandType.Text;

        AddParameter(lookupCommand, "@BuyerConsumerID", buyerConsumerId, DbType.Int32);
        AddParameter(lookupCommand, "@SellerID", sellerId, DbType.Int32);
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
                @BuyerConsumerID,
                @SellerID,
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

        AddParameter(insertCommand, "@BuyerConsumerID", buyerConsumerId, DbType.Int32);
        AddParameter(insertCommand, "@SellerID", sellerId, DbType.Int32);
        AddParameter(insertCommand, "@ContextType", (byte)contextType, DbType.Byte);
        AddParameter(insertCommand, "@OrderID", orderId, DbType.Int32);

        var insertedId = await insertCommand.ExecuteScalarAsync(cancellationToken);
        return insertedId is null || insertedId == DBNull.Value
            ? throw new InvalidOperationException("Failed to create a messaging conversation.")
            : Convert.ToInt32(insertedId);
    }

    private async Task<MessageConversationSummary?> GetConversationSnapshotAsync(DbConnection connection, int conversationId, CancellationToken cancellationToken)
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
                    WHEN LEN(lm.Body) > 120 THEN LEFT(lm.Body, 117) + N'...'
                    ELSE lm.Body
                END AS LastMessagePreview,
                CAST(0 AS INT) AS UnreadCount,
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
                ORDER BY m.MessageId DESC
            ) lm
            WHERE c.ConversationId = @ConversationID;
            """;
        command.CommandType = CommandType.Text;

        AddParameter(command, "@ConversationID", conversationId, DbType.Int32);
        return await ReadSingleConversationAsync(command, cancellationToken);
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
                                WHEN @ActorConsumerID IS NOT NULL AND c.BuyerUserId = @ActorConsumerID THEN c.BuyerLastReadAt
                                WHEN @ActorSellerID IS NOT NULL AND c.SellerUserId = @ActorSellerID THEN c.SellerLastReadAt
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
                    m.IsDeleted
                FROM dbo.MessagingMessages m
                WHERE m.ConversationId = c.ConversationId
                ORDER BY m.MessageId DESC
            ) lm
            WHERE c.ConversationId = @ConversationID
              AND (
                    (@ActorConsumerID IS NOT NULL AND c.BuyerUserId = @ActorConsumerID)
                 OR (@ActorSellerID IS NOT NULL AND c.SellerUserId = @ActorSellerID)
              );
            """;
        command.CommandType = CommandType.Text;

        AddParameter(command, "@ConversationID", conversationId, DbType.Int32);
        AddActorParameters(command, actor);

        return await ReadSingleConversationAsync(command, cancellationToken);
    }

    private async Task<bool> IsConversationParticipantAsync(
        DbConnection connection,
        int conversationId,
        MessageActorContext actor,
        CancellationToken cancellationToken)
    {
        if (!actor.HasConversationRole)
        {
            return false;
        }

        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT CASE
                WHEN EXISTS
                (
                    SELECT 1
                    FROM dbo.MessagingConversations c
                    WHERE c.ConversationId = @ConversationID
                      AND (
                            (@ActorConsumerID IS NOT NULL AND c.BuyerUserId = @ActorConsumerID)
                         OR (@ActorSellerID IS NOT NULL AND c.SellerUserId = @ActorSellerID)
                      )
                )
                THEN CAST(1 AS BIT)
                ELSE CAST(0 AS BIT)
            END AS IsMember;
            """;
        command.CommandType = CommandType.Text;

        AddParameter(command, "@ConversationID", conversationId, DbType.Int32);
        AddActorParameters(command, actor);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is bool isMember && isMember;
    }

    private async Task<MessageConversationSummary?> ReadSingleConversationAsync(DbCommand command, CancellationToken cancellationToken)
    {
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return MapConversation(reader);
    }

    private static MessageConversationSummary MapConversation(DbDataReader reader)
    {
        return new MessageConversationSummary(
            reader.GetInt32(reader.GetOrdinal("ConversationID")),
            reader.GetInt32(reader.GetOrdinal("BuyerUserID")),
            reader.GetInt32(reader.GetOrdinal("SellerUserID")),
            (ConversationContextType)reader.GetByte(reader.GetOrdinal("ContextType")),
            GetNullableInt(reader, "OrderID"),
            GetNullableDateTime(reader, "LastMessageAt"),
            GetNullableDateTime(reader, "BuyerLastReadAt"),
            GetNullableDateTime(reader, "SellerLastReadAt"),
            GetNullableString(reader, "LastMessagePreview"),
            GetNullableInt(reader, "UnreadCount") ?? 0,
            reader.GetDateTime(reader.GetOrdinal("CreatedAt")),
            reader.GetDateTime(reader.GetOrdinal("UpdatedAt")));
    }

    private static MessageItem MapMessage(DbDataReader reader)
    {
        var isDeleted = reader.GetBoolean(reader.GetOrdinal("IsDeleted"));
        var body = GetNullableString(reader, "Body");

        return new MessageItem(
            reader.GetInt64(reader.GetOrdinal("MessageID")),
            reader.GetInt32(reader.GetOrdinal("ConversationID")),
            reader.GetInt32(reader.GetOrdinal("SenderUserID")),
            isDeleted ? null : body,
            GetNullableString(reader, "AttachmentUrl"),
            GetBoolean(reader, "HasStoredAttachment"),
            reader.GetDateTime(reader.GetOrdinal("SentAt")),
            isDeleted);
    }

    private static MessageAttachmentReadModel MapAttachment(DbDataReader reader)
    {
        var fileName = GetNullableString(reader, "AttachmentFileName");
        if (string.IsNullOrWhiteSpace(fileName))
        {
            fileName = $"message-attachment-{reader.GetInt64(reader.GetOrdinal("MessageID"))}";
        }

        var contentType = GetNullableString(reader, "AttachmentContentType");
        if (string.IsNullOrWhiteSpace(contentType))
        {
            contentType = "application/octet-stream";
        }

        var dataOrdinal = reader.GetOrdinal("AttachmentData");
        var data = reader.IsDBNull(dataOrdinal) ? Array.Empty<byte>() : (byte[])reader.GetValue(dataOrdinal);

        return new MessageAttachmentReadModel(
            reader.GetInt64(reader.GetOrdinal("MessageID")),
            contentType,
            fileName,
            data);
    }

    private async Task<T> WithOpenConnectionAsync<T>(Func<DbConnection, Task<T>> action, CancellationToken cancellationToken)
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

    private static string? GetNullableString(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static int? GetNullableInt(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
    }

    private static bool GetBoolean(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return !reader.IsDBNull(ordinal) && reader.GetBoolean(ordinal);
    }

    private static DateTime? GetNullableDateTime(DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
    }

    private static void AddActorParameters(DbCommand command, MessageActorContext actor)
    {
        AddParameter(command, "@ActorUserID", actor.UserId, DbType.Int32);
        AddParameter(command, "@ActorConsumerID", actor.ConsumerId, DbType.Int32);
        AddParameter(command, "@ActorSellerID", actor.SellerId, DbType.Int32);
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



