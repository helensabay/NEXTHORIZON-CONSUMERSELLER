namespace NextHorizon.Security;

public sealed record AuthenticatedUserContext(
    int UserId,
    string Email,
    string UserType,
    int? ConsumerId,
    int? ConsumerAccountUserId,
    int? SellerId,
    int? SellerAccountUserId)
{
    public bool HasConsumerRole => ConsumerId.HasValue;
    public bool HasSellerRole => SellerId.HasValue;
    public bool HasMessagingRole => HasConsumerRole || HasSellerRole;
}