namespace NextHorizon.Models
{
    public class UpdateBusinessProfileDto
    {
        public string BusinessName { get; set; } = string.Empty;
        public string BusinessType { get; set; } = string.Empty;
        public string BusinessEmail { get; set; } = string.Empty;
        public string BusinessPhone { get; set; } = string.Empty;
        /// <summary>
        /// Optional: Tax ID for the business
        /// </summary>
        public string? TaxId { get; set; }
        public string BusinessAddress { get; set; } = string.Empty;
    }
    public class ChangePasswordDto
    {
        public string CurrentPassword { get; set; } = string.Empty;
        public string NewPassword { get; set; } = string.Empty;
        public string ConfirmNewPassword { get; set; } = string.Empty;
    }
}