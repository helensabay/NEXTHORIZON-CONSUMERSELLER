using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyAspNetApp.Models
{
    [Table("Users")]
    public class User
    {
        [Key]
        [Column("user_id")]
        public int UserId { get; set; }

        [Column("email")]
        public string? Email { get; set; }

        [Column("password_hash")]
        public string? PasswordHash { get; set; }

        [Column("user_type")]
        public string? UserType { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("updated_at")]
        public DateTime? UpdatedAt { get; set; }

        [Column("profile_picture")]
        public byte[]? ProfilePicture { get; set; }

        [Column("profile_picture_content_type")]
        public string? ProfilePictureContentType { get; set; }

        public Consumer? Consumer { get; set; }
    }

    [Table("Consumers")]
    public class Consumer
    {
        [Key]
        [Column("consumer_id")]
        public int ConsumerId { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("first_name")]
        public string? FirstName { get; set; }

        [Column("middle_name")]
        public string? MiddleName { get; set; }

        [Column("last_name")]
        public string? LastName { get; set; }

        [Column("username")]
        public string? Username { get; set; }

        [Column("address")]
        public string? Address { get; set; }

        [Column("phone_number")]
        public string? PhoneNumber { get; set; }

        [Column("gender")]
        public string? Gender { get; set; }

        [Column("birthday")]
        public DateTime? Birthday { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [ForeignKey(nameof(UserId))]
        public User? User { get; set; }

        [NotMapped]
        public string FullName
        {
            get
            {
                var parts = new[] { FirstName, MiddleName, LastName }
                    .Where(x => !string.IsNullOrWhiteSpace(x));
                return string.Join(" ", parts);
            }
        }
    }

    [Table("Sellers")]
    public class SellerProfile
    {
        [Key]
        [Column("seller_id")]
        public int SellerId { get; set; }

        [Column("user_id")]
        public int UserId { get; set; }

        [Column("business_type")]
        public string? BusinessType { get; set; }

        [Column("business_name")]
        public string? BusinessName { get; set; }

        [Column("business_email")]
        public string? BusinessEmail { get; set; }

        [Column("business_phone")]
        public string? BusinessPhone { get; set; }

        [Column("tax_id")]
        public string? TaxId { get; set; }

        [Column("business_address")]
        public string? BusinessAddress { get; set; }

        [Column("logo_path")]
        public string? LogoPath { get; set; }

        [Column("logo_data", TypeName = "varbinary(max)")]
        public byte[]? LogoData { get; set; }

        [Column("logo_content_type")]
        public string? LogoContentType { get; set; }

        [Column("logo_mime_type")]
        public string? LogoMimeType { get; set; }

        [Column("document_path")]
        public string? DocumentPath { get; set; }

        [Column("seller_status")]
        public string? SellerStatus { get; set; }

        [Column("created_at")]
        public DateTime? CreatedAt { get; set; }

        [Column("followers_count")]
        public int FollowersCount { get; set; }
    }
}
