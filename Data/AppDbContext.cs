using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using MyAspNetApp.Models;
using MyAspNetApp.Models.HelpCenter;
using MyAspNetApp.Models.Messaging;

namespace MyAspNetApp.Data
{
    public class AppDbContext : DbContext
    {
        private readonly IConfiguration? _configuration;
        private readonly IHostEnvironment? _environment;

        public AppDbContext(
            DbContextOptions<AppDbContext> options,
            IConfiguration? configuration = null,
            IHostEnvironment? environment = null) : base(options)
        {
            _configuration = configuration;
            _environment = environment;
        }

        public DbSet<DbProduct> Products { get; set; }
        public DbSet<DbProductVariant> ProductVariants { get; set; }
        public DbSet<DbProductColorImage> ProductColorImages { get; set; }
        public DbSet<DbReview> Reviews { get; set; }
        public DbSet<DbReviewImage> ReviewImages { get; set; }
        public DbSet<DbPromotion> Promotions { get; set; }
        public DbSet<DbChallenge> Challenges { get; set; }
        public DbSet<DbChallengeRegistration> ChallengeRegistrations { get; set; }
        public DbSet<DbChallengeParticipant> ChallengeParticipants { get; set; }
        public DbSet<DbChallengeActivity> ChallengeActivities { get; set; }
        public DbSet<DbChallengeNotification> ChallengeNotifications { get; set; }
        public DbSet<DbLeaderboardRecord> LeaderboardRecords { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Consumer> Consumers { get; set; }
        public DbSet<SellerProfile> Sellers { get; set; }
        public DbSet<PasswordOtp> PasswordOtps { get; set; }
        public DbSet<SellerFollower> SellerFollowers { get; set; }
        public DbSet<MessageConversation> MessageConversations { get; set; }
        public DbSet<ConversationMessage> ConversationMessages { get; set; }
        public DbSet<FaqRecord> FaqRecords { get; set; }
        public DbSet<SupportFaqRecord> SupportFaqRecords { get; set; }
        public DbSet<SupportContactChannel> SupportContactChannels { get; set; }
        public DbSet<SupportTicket> SupportTickets { get; set; }
        public DbSet<SupportMessage> SupportMessages { get; set; }
        public DbSet<LiveAgentSession> LiveAgentSessions { get; set; }
        public DbSet<SupportAgentRecord> SupportAgents { get; set; }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                var fallbackConnectionString =
                    _configuration != null && _environment != null
                        ? DbConnectionStringResolver.ResolveRequiredConnectionString(_configuration, _environment)
                        : _configuration?.GetConnectionString("DefaultConnection");

                if (string.IsNullOrWhiteSpace(fallbackConnectionString))
                {
                    throw new InvalidOperationException(
                        "AppDbContext could not resolve the 'DefaultConnection' connection string.");
                }

                if (string.IsNullOrWhiteSpace(fallbackConnectionString))
                {
                    throw new InvalidOperationException(
                        "AppDbContext could not resolve a non-empty 'DefaultConnection' connection string.");
                }

                var connectionStringBuilder = new SqlConnectionStringBuilder(fallbackConnectionString)
                {
                    Encrypt = true,
                    TrustServerCertificate = true,
                    Pooling = true,
                    MinPoolSize = 0
                };

                if (string.IsNullOrWhiteSpace(connectionStringBuilder.ConnectionString))
                {
                    throw new InvalidOperationException(
                        "AppDbContext resolved an empty 'DefaultConnection' connection string.");
                }

                if (connectionStringBuilder.ConnectTimeout <= 0 || connectionStringBuilder.ConnectTimeout > 10)
                {
                    connectionStringBuilder.ConnectTimeout = 10;
                }

                if (connectionStringBuilder.MaxPoolSize < 200)
                {
                    connectionStringBuilder.MaxPoolSize = 200;
                }

                optionsBuilder.UseSqlServer(
                    connectionStringBuilder.ConnectionString,
                    sqlOptions =>
                    {
                        sqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 2,
                            maxRetryDelay: TimeSpan.FromSeconds(5),
                            errorNumbersToAdd: new[] { -2, 4060, 40197, 40501, 40613, 49918, 49919, 49920 });
                        sqlOptions.CommandTimeout(60);
                    });
            }

            base.OnConfiguring(optionsBuilder);
            // Suppress the pending model changes warning to allow migrations that drop columns
            optionsBuilder.ConfigureWarnings(w => 
                w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning));
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.ToTable("Users");
                entity.HasKey(e => e.UserId);
                entity.Property(e => e.UserId).HasColumnName("user_id");
                entity.Property(e => e.Email).HasColumnName("email");
                entity.Property(e => e.PasswordHash).HasColumnName("password_hash");
                entity.Property(e => e.UserType).HasColumnName("user_type");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
                entity.Property(e => e.ProfilePicture).HasColumnName("profile_picture");
                entity.Property(e => e.ProfilePictureContentType).HasColumnName("profile_picture_content_type");

                entity.HasOne(e => e.Consumer)
                    .WithOne(e => e.User)
                    .HasForeignKey<Consumer>(e => e.UserId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<Consumer>(entity =>
            {
                entity.ToTable("Consumers");
                entity.HasKey(e => e.ConsumerId);
                entity.Property(e => e.ConsumerId).HasColumnName("consumer_id");
                entity.Property(e => e.UserId).HasColumnName("user_id");
                entity.Property(e => e.FirstName).HasColumnName("first_name");
                entity.Property(e => e.MiddleName).HasColumnName("middle_name");
                entity.Property(e => e.LastName).HasColumnName("last_name");
                entity.Property(e => e.Username).HasColumnName("username");
                entity.Property(e => e.Address).HasColumnName("address");
                entity.Property(e => e.PhoneNumber).HasColumnName("phone_number");
                entity.Property(e => e.Gender).HasColumnName("gender");
                entity.Property(e => e.Birthday).HasColumnName("birthday");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.HasIndex(e => e.UserId).IsUnique();
            });

            modelBuilder.Entity<SellerProfile>(entity =>
            {
                entity.ToTable("Sellers");
                entity.HasKey(e => e.SellerId);
                entity.Property(e => e.SellerId).HasColumnName("seller_id");
                entity.Property(e => e.UserId).HasColumnName("user_id");
                entity.Property(e => e.BusinessType).HasColumnName("business_type");
                entity.Property(e => e.BusinessName).HasColumnName("business_name");
                entity.Property(e => e.BusinessEmail).HasColumnName("business_email");
                entity.Property(e => e.BusinessPhone).HasColumnName("business_phone");
                entity.Property(e => e.TaxId).HasColumnName("tax_id");
                entity.Property(e => e.BusinessAddress).HasColumnName("business_address");
                entity.Property(e => e.LogoPath).HasColumnName("logo_path");
                entity.Property(e => e.LogoData).HasColumnName("logo_data").HasColumnType("varbinary(max)");
                entity.Property(e => e.LogoContentType).HasColumnName("logo_content_type");
                entity.Property(e => e.LogoMimeType).HasColumnName("logo_mime_type");
                entity.Property(e => e.DocumentPath).HasColumnName("document_path");
                entity.Property(e => e.SellerStatus).HasColumnName("seller_status");
                entity.Property(e => e.CreatedAt).HasColumnName("created_at");
                entity.Property(e => e.FollowersCount).HasColumnName("followers_count");
                entity.HasIndex(e => e.UserId).IsUnique(false);
            });

            modelBuilder.Entity<PasswordOtp>(entity =>
            {
                entity.ToTable("PasswordOtps");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.user_id).HasColumnName("user_id");
                entity.HasOne(e => e.User)
                    .WithMany()
                    .HasForeignKey(e => e.user_id)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SellerFollower>(entity =>
            {
                entity.ToTable("SellerFollowers");
                entity.HasKey(e => e.SellerFollowerId);
                entity.Property(e => e.SellerFollowerId).HasColumnName("seller_follower_id");
                entity.Property(e => e.SellerId).HasColumnName("seller_id");
                entity.Property(e => e.FollowerUserId).HasColumnName("follower_user_id");
                entity.Property(e => e.FollowedAt).HasColumnName("followed_at").HasColumnType("datetime2");
                entity.HasIndex(e => new { e.SellerId, e.FollowerUserId }).IsUnique();
                entity.HasIndex(e => e.SellerId);
                entity.HasIndex(e => e.FollowerUserId);
            });

            modelBuilder.Entity<DbLeaderboardRecord>(entity =>
            {
                entity.ToTable("leaderboard_records");
                entity.Property(x => x.UploadId);
                entity.Property(x => x.UserId);
                entity.HasIndex(x => x.UploadId).IsUnique();
                entity.Property(x => x.DistanceKm).HasPrecision(8, 2);
            });

            modelBuilder.Entity<MessageConversation>(entity =>
            {
                entity.ToTable("MessagingConversations", table =>
                {
                    table.ExcludeFromMigrations();
                });

                entity.HasKey(x => x.ConversationId);
                entity.Property(x => x.ContextType).HasConversion<byte>();
                entity.Property(x => x.LastMessageAt).HasColumnType("datetime2");
                entity.Property(x => x.BuyerLastReadAt).HasColumnType("datetime2");
                entity.Property(x => x.SellerLastReadAt).HasColumnType("datetime2");
                entity.Property(x => x.CreatedAt).HasColumnType("datetime2");
                entity.Property(x => x.UpdatedAt).HasColumnType("datetime2");
            });

            modelBuilder.Entity<ConversationMessage>(entity =>
            {
                entity.ToTable("MessagingMessages", table =>
                {
                    table.ExcludeFromMigrations();
                });

                entity.HasKey(x => x.MessageId);
                entity.Property(x => x.Body).HasMaxLength(2000);
                entity.Property(x => x.AttachmentUrl).HasMaxLength(400);
                entity.Property(x => x.AttachmentContentType).HasMaxLength(200);
                entity.Property(x => x.AttachmentFileName).HasMaxLength(510);
                entity.Property(x => x.SentAt).HasColumnType("datetime2");
                entity.HasOne(x => x.Conversation)
                    .WithMany(x => x.Messages)
                    .HasForeignKey(x => x.ConversationId)
                    .OnDelete(DeleteBehavior.Cascade);
            });

            modelBuilder.Entity<SupportAgentRecord>(entity =>
            {
                entity.ToTable("Agents", table =>
                {
                    table.ExcludeFromMigrations();
                });

                entity.HasKey(x => x.ChatId);
                entity.Property(x => x.ChatId)
                    .HasColumnName("ChatID")
                    .ValueGeneratedNever();
                entity.Property(x => x.AgentName)
                    .HasColumnName("AgentName");
                entity.Property(x => x.AgentStatus)
                    .HasColumnName("AgentStatus");
                entity.Property(x => x.UserId)
                    .HasColumnName("UserID")
                    .IsRequired();
            });

            modelBuilder.Entity<SupportContactChannel>(entity =>
            {
                entity.ToTable("SupportContactChannels");
                entity.HasKey(x => x.SupportContactChannelId);
                entity.Property(x => x.ChannelType).IsRequired().HasMaxLength(40);
                entity.Property(x => x.Label).IsRequired().HasMaxLength(80);
                entity.Property(x => x.Value).IsRequired().HasMaxLength(320);
                entity.Property(x => x.DisplayText).IsRequired().HasMaxLength(320);
                entity.Property(x => x.ActionHref).IsRequired().HasMaxLength(400);
                entity.Property(x => x.IsActive).HasDefaultValue(true);
                entity.HasIndex(x => new { x.IsActive, x.DisplayOrder });
            });

            modelBuilder.Entity<SupportFaqRecord>(entity =>
            {
                entity.ToTable("SupportFAQs", table => table.HasTrigger("SupportFAQs_Trigger"));
                entity.HasKey(x => x.Id);
                entity.Property(x => x.Category).IsRequired();
                entity.Property(x => x.Question).IsRequired();
                entity.Property(x => x.Status).IsRequired();
                entity.Property(x => x.UserType).IsRequired();
                entity.Property(x => x.CreatedAt).IsRequired().HasColumnType("datetime2");
                entity.Property(x => x.EndTime).HasColumnType("datetime2");
                entity.Property(x => x.StartTime).HasColumnType("datetime2");
            });

            modelBuilder.Entity<SupportTicket>(entity =>
            {
                entity.ToTable("SupportTickets");
                entity.HasKey(x => x.SupportTicketId);
                entity.Property(x => x.ReferenceCode).IsRequired().HasMaxLength(40);
                entity.Property(x => x.FaqCategory).HasMaxLength(120);
                entity.Property(x => x.Subject).IsRequired().HasMaxLength(160);
                entity.Property(x => x.Body).IsRequired().HasMaxLength(4000);
                entity.Property(x => x.Status).IsRequired().HasConversion<byte>();
                entity.Property(x => x.CreatedAt).IsRequired().HasColumnType("datetime2");
                entity.Property(x => x.UpdatedAt).IsRequired().HasColumnType("datetime2");
                entity.HasIndex(x => x.ReferenceCode).IsUnique();
            });

            modelBuilder.Entity<LiveAgentSession>(entity =>
            {
                entity.ToTable("LiveAgentSessions");
                entity.HasKey(x => x.LiveAgentSessionId);
                entity.Property(x => x.CategorySlug).IsRequired().HasMaxLength(80);
                entity.Property(x => x.CategoryTitle).IsRequired().HasMaxLength(120);
                entity.Property(x => x.FirstQuestion).IsRequired().HasMaxLength(4000).HasDefaultValue(string.Empty);
                entity.Property(x => x.Status).IsRequired().HasConversion<byte>();
                entity.Property(x => x.EndedReason).IsRequired().HasConversion<byte>();
                entity.Property(x => x.CreatedAt).IsRequired().HasColumnType("datetime2");
                entity.Property(x => x.UpdatedAt).IsRequired().HasColumnType("datetime2");
                entity.HasIndex(x => x.SupportFaqId).IsUnique();
            });
        }
    }
}
