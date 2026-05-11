using System.Data;
using System.Data.Common;
using NextHorizon.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace NextHorizon.Data.Messaging;

public sealed class SimulatedOrderConversationResolver : IOrderConversationResolver
{
    private readonly AppDbContext _dbContext;
    private readonly IReadOnlyDictionary<int, SimulatedOrderEntry> _knownOrders;

    public SimulatedOrderConversationResolver(AppDbContext dbContext, IConfiguration configuration)
    {
        _dbContext = dbContext;

        var configuredEntries = configuration
            .GetSection("Messaging:SimulatedOrders")
            .Get<List<SimulatedOrderEntry>>() ?? [];

        _knownOrders = configuredEntries
            .Where(entry =>
                entry.OrderId > 0 &&
                int.TryParse(entry.BuyerConsumerId, out _) &&
                int.TryParse(entry.SellerId, out _))
            .GroupBy(entry => entry.OrderId)
            .ToDictionary(group => group.Key, group => group.Last());
    }

    public async Task<OrderConversationContext?> ResolveAsync(int orderId, MessageActorContext actor, CancellationToken cancellationToken)
    {
        if (orderId <= 0 || actor.UserId <= 0)
        {
            return null;
        }

        var liveOrder = await ResolveFromDatabaseAsync(orderId, cancellationToken);
        if (liveOrder is not null)
        {
            return new OrderConversationContext(
                liveOrder.Value.OrderId,
                liveOrder.Value.BuyerConsumerId,
                liveOrder.Value.SellerId,
                CanActorAccess(actor, liveOrder.Value.BuyerConsumerId, liveOrder.Value.SellerId));
        }

        if (_knownOrders.TryGetValue(orderId, out var configuredOrder))
        {
            var buyerConsumerId = int.Parse(configuredOrder.BuyerConsumerId);
            var sellerId = int.Parse(configuredOrder.SellerId);

            return new OrderConversationContext(
                configuredOrder.OrderId,
                buyerConsumerId,
                sellerId,
                CanActorAccess(actor, buyerConsumerId, sellerId));
        }

        if (actor.ConsumerId.HasValue)
        {
            var generatedSellerId = CreateDeterministicId($"simulated-seller-{orderId}");
            return new OrderConversationContext(orderId, actor.ConsumerId.Value, generatedSellerId, true);
        }

        return null;
    }

    private async Task<(int OrderId, int BuyerConsumerId, int SellerId)?> ResolveFromDatabaseAsync(int orderId, CancellationToken cancellationToken)
    {
        var connection = _dbContext.Database.GetDbConnection();
        var shouldClose = connection.State != ConnectionState.Open;

        if (shouldClose)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                SELECT TOP (1)
                    OrderID,
                    ConsumerID,
                    seller_id
                FROM dbo.Orders
                WHERE OrderID = @OrderID
                  AND ConsumerID IS NOT NULL
                  AND seller_id IS NOT NULL;
                """;
            command.CommandType = CommandType.Text;

            AddParameter(command, "@OrderID", orderId, DbType.Int32);

            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (!await reader.ReadAsync(cancellationToken))
            {
                return null;
            }

            return (
                reader.GetInt32(reader.GetOrdinal("OrderID")),
                reader.GetInt32(reader.GetOrdinal("ConsumerID")),
                reader.GetInt32(reader.GetOrdinal("seller_id")));
        }
        finally
        {
            if (shouldClose)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static bool CanActorAccess(MessageActorContext actor, int buyerConsumerId, int sellerId)
        => (actor.ConsumerId.HasValue && actor.ConsumerId.Value == buyerConsumerId)
            || (actor.SellerId.HasValue && actor.SellerId.Value == sellerId);

    private static int CreateDeterministicId(string value)
    {
        var bytes = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(value));
        return (int)(BitConverter.ToUInt32(bytes, 0) % int.MaxValue) + 1;
    }

    private static void AddParameter(DbCommand command, string name, object? value, DbType dbType)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.DbType = dbType;
        parameter.Value = value ?? DBNull.Value;
        command.Parameters.Add(parameter);
    }

    public sealed class SimulatedOrderEntry
    {
        public int OrderId { get; init; }

        public string BuyerConsumerId { get; init; } = string.Empty;

        public string SellerId { get; init; } = string.Empty;
    }
}