namespace NextHorizon.Data.Messaging;

public interface IOrderConversationResolver
{
    Task<OrderConversationContext?> ResolveAsync(int orderId, MessageActorContext actor, CancellationToken cancellationToken);
}

public sealed record OrderConversationContext(
    int OrderId,
    int BuyerConsumerId,
    int SellerId,
    bool CanRequestUserAccess);