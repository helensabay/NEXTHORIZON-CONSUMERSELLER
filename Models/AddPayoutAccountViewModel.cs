namespace NextHorizon.Models
{
    public class AddPayoutAccountViewModel
    {
        public string AccountType { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public bool IsDefault { get; set; }

        public string? CardNumber { get; set; }
        public string? ExpiryDate { get; set; }
        public string? CVV { get; set; }

        public string? Region { get; set; }
        public string? Province { get; set; }
        public string? City { get; set; }
        public string? Barangay { get; set; }
        public string? StreetName { get; set; }
        public string? Building { get; set; }
        public string? HouseNo { get; set; }
        public string? PostalCode { get; set; }

        public string? EWalletType { get; set; }
        public string? EwalletAccountName { get; set; }
        public string? EwalletAccountNumber { get; set; }

        public string? BankAccountName { get; set; }
        public string? BankAccountNumber { get; set; }
        public string? BankName { get; set; }
        public string? AccountHolderName { get; set; }
    }

    public class SetDefaultAccountRequest
    {
        public int AccountId { get; set; }
    }

    public class RemoveAccountRequest
    {
        public int AccountId { get; set; }
    }
}
