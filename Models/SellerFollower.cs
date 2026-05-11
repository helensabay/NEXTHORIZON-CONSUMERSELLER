using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MyAspNetApp.Models;

[Table("SellerFollowers")]
public class SellerFollower
{
    [Key]
    [Column("seller_follower_id")]
    public int SellerFollowerId { get; set; }

    [Column("seller_id")]
    public int SellerId { get; set; }

    [Column("follower_user_id")]
    public int FollowerUserId { get; set; }

    [Column("followed_at")]
    public DateTime FollowedAt { get; set; }
}
