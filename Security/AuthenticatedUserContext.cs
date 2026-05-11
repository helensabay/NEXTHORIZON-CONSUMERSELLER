namespace MyAspNetApp.Security;

public sealed record AuthenticatedUserContext(
    int UserId,
    int? ConsumerId,
    int? ConsumerAccountUserId,
    int? SellerId,
    int? SellerAccountUserId)
{
    public bool HasConsumerRole => ConsumerId.HasValue;

    public bool HasSellerRole => SellerId.HasValue;

    public bool HasMessagingRole => HasConsumerRole || HasSellerRole;
}
