namespace NextHorizon.Messaging.Models;

public enum ConversationContextType : byte
{
    General = 1,
    Direct = General,
    Order = 2,
}