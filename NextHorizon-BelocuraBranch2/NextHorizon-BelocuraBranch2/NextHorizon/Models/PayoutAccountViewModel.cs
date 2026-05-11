namespace NextHorizon.Models
{
    public class PayoutAccountViewModel
    {
        public int AccountId { get; set; }
        public int UserId { get; set; } // Add this field
        public string AccountType { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string? BankName { get; set; }
        public string? CardNumber { get; set; }
        public DateTime? LastUsed { get; set; }
        public bool IsDefault { get; set; }
        
        // Billing address fields
        public string? Region { get; set; }
        public string? Province { get; set; }
        public string? City { get; set; }
        public string? Barangay { get; set; }
        public string? StreetName { get; set; }
        public string? Building { get; set; }
        public string? HouseNo { get; set; }
        public int? PostalCode { get; set; }

        public string DisplayName
        {
            get
            {
                if (!string.IsNullOrEmpty(BankName))
                    return $"{BankName} - {AccountNumber}";
                if (AccountType?.ToLower() == "ewallet")
                    return $"E-Wallet - {AccountNumber}";
                if (AccountType?.ToLower() == "card")
                    return $"Card - ****{(AccountNumber?.Length >= 4 ? AccountNumber.Substring(AccountNumber.Length - 4) : AccountNumber)}";
                return $"{AccountType} - {AccountNumber}";
            }
        }
    }

    
}