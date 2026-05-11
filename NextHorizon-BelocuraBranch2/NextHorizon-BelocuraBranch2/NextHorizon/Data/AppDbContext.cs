using Microsoft.EntityFrameworkCore;
using NextHorizon.Models;           
using NextHorizon.Messaging.Models;
namespace NextHorizon.Data;

public sealed class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }
    public DbSet<Message> Messages { get; set; } = null!;
    public DbSet<Faq> FAQs { get; set; }
    public DbSet<SupportConversation> SupportConversations { get; set; } = null!;
    public DbSet<SupportMessage> SupportMessages { get; set; } = null!;
    public DbSet<SupportFAQ> SupportFAQs {get; set;} = null!;
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Consumer> Consumers => Set<Consumer>();
    public DbSet<SellerAccount> SellerAccounts => Set<SellerAccount>();
    public DbSet<MessageConversation> MessageConversations => Set<MessageConversation>();
    public DbSet<ConversationMessage> ConversationMessages => Set<ConversationMessage>();
    public DbSet<SellerNotification> SellerNotifications => Set<SellerNotification>();
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OrderItem> OrderItems => Set<OrderItem>();
    public DbSet<ReturnRequest> ReturnRequests => Set<ReturnRequest>();
    public DbSet<DbProduct> Products => Set<DbProduct>();
    public DbSet<DbProductVariant> ProductVariants => Set<DbProductVariant>();
    public DbSet<Logistics> Logistics => Set<Logistics>();
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);
        // Removed the temporary warning suppressor so your app is clean for production!
    }

    public DbSet<DbProductColorImage> ProductColorImages => Set<DbProductColorImage>();
    public DbSet<DbReview> Reviews => Set<DbReview>();
    public DbSet<DbReviewImage> ReviewImages => Set<DbReviewImage>();
    public DbSet<DbSizeGuide> SizeGuides => Set<DbSizeGuide>();
    public DbSet<DbSizeGuideImage> SizeGuideImages => Set<DbSizeGuideImage>();
    public DbSet<DbPromotion> Promotions => Set<DbPromotion>();
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);  
        modelBuilder.Ignore<MessagingMessage>();
        
        // ============== DECIMAL & TRIGGER FIXES ==============
        modelBuilder.Entity<Order>(entity =>
        {
            entity.ToTable(tb => tb.HasTrigger("SomeTriggerName")); 
            entity.Property(o => o.TotalAmount).HasColumnType("decimal(18,2)");
            entity.Property(o => o.ShippingFee).HasColumnType("decimal(18,2)");
            entity.Property(o => o.Subtotal).HasColumnType("decimal(18,2)");
        });

        modelBuilder.Entity<ReturnRequest>(entity =>
        {
            entity.ToTable("returns");
            entity.HasKey(r => r.ReturnId);
            entity.Property(r => r.ReturnId).HasColumnName("ReturnId");
            entity.Property(r => r.OrderId).HasColumnName("OrderId");
            entity.Property(r => r.UserId).HasColumnName("UserId");
            entity.Property(r => r.SellerId).HasColumnName("SellerId");
            entity.Property(r => r.Reason).HasColumnName("Reason").HasMaxLength(150).IsRequired();
            entity.Property(r => r.Message).HasColumnName("Message").HasColumnType("nvarchar(max)");
            entity.Property(r => r.FileName).HasColumnName("FileName").HasMaxLength(260);
            entity.Property(r => r.ContentType).HasColumnName("ContentType").HasMaxLength(100);
            entity.Property(r => r.ImageData).HasColumnName("ImageData").HasColumnType("varbinary(max)");
            entity.Property(r => r.Status).HasColumnName("Status").HasMaxLength(50).IsRequired();
            entity.Property(r => r.SellerDecisionReason).HasColumnName("SellerDecisionReason").HasMaxLength(150);
            entity.Property(r => r.SellerDecisionNote).HasColumnName("SellerDecisionNote").HasColumnType("nvarchar(max)");
            entity.Property(r => r.ReviewedAt).HasColumnName("ReviewedAt").HasColumnType("datetime2");
            entity.Property(r => r.CreatedAt).HasColumnName("CreatedAt").HasColumnType("datetime2");
            entity.Property(r => r.UpdatedAt).HasColumnName("UpdatedAt").HasColumnType("datetime2");
            entity.Ignore(r => r.BuyerName);
            entity.Ignore(r => r.OrderDate);
            entity.Ignore(r => r.ImageUrl);
            entity.Ignore(r => r.HasImage);
            entity.HasIndex(r => new { r.SellerId, r.Status });
            entity.HasIndex(r => r.OrderId);
        });

        modelBuilder.Entity<OrderItem>()
            .Property(o => o.UnitPrice)
            .HasColumnType("decimal(18,2)");

        modelBuilder.Entity<DbProduct>()
            .Property(p => p.Price)
            .HasColumnType("decimal(18,2)");

        // ============== EXISTING CONFIGURATIONS ==============
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.ToTable("Customers");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FullName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(320).IsRequired();
            entity.HasIndex(x => x.Email).IsUnique();
            entity.Property(x => x.CreatedUtc).HasDefaultValueSql("SYSUTCDATETIME()");
        });

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
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });

        modelBuilder.Entity<SellerAccount>(entity =>
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
            entity.Property(e => e.DocumentPath).HasColumnName("document_path");
            entity.Property(e => e.SellerStatus).HasColumnName("seller_status");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
        });

        // ============== REFERENCE ENTITIES (EXCLUDED FROM MIGRATIONS) ==============
        
        var platformUser = modelBuilder.Entity<PlatformUser>();
        platformUser.ToTable("Users", "dbo", table => table.ExcludeFromMigrations());
        platformUser.HasKey(x => x.UserId);
        platformUser.Property(x => x.UserId)
            .HasColumnName("user_id")
            .ValueGeneratedNever();
        platformUser.Property(x => x.IsActive)
            .HasColumnName("is_active")
            .IsRequired();

        var consumerRef = modelBuilder.Entity<ConsumerRef>();
        consumerRef.ToTable("Consumers", "dbo", table => table.ExcludeFromMigrations());
        consumerRef.HasKey(x => x.ConsumerId);
        consumerRef.Property(x => x.ConsumerId)
            .HasColumnName("consumer_id")
            .ValueGeneratedNever();
        consumerRef.Property(x => x.UserId)
            .HasColumnName("user_id")
            .IsRequired();
        consumerRef.Property(x => x.FirstName)
            .HasColumnName("first_name");
        consumerRef.Property(x => x.MiddleName)
            .HasColumnName("middle_name");
        consumerRef.Property(x => x.LastName)
            .HasColumnName("last_name");
        consumerRef.Property(x => x.Username)
            .HasColumnName("username");

        var sellerRef = modelBuilder.Entity<SellerRef>();
        sellerRef.ToTable("Sellers", "dbo", table => table.ExcludeFromMigrations());
        sellerRef.HasKey(x => x.SellerId);
        sellerRef.Property(x => x.SellerId)
            .HasColumnName("seller_id")
            .ValueGeneratedNever();
        sellerRef.Property(x => x.UserId)
            .HasColumnName("user_id")
            .IsRequired();


        // ============== MESSAGING CONVERSATIONS ==============
        
        modelBuilder.Entity<MessageConversation>(entity =>
        {
            entity.ToTable("MessagingConversations", table =>
            {
                table.HasCheckConstraint("CK_MessagingConversations_ContextType", "[ContextType] IN (1, 2)");
                table.HasCheckConstraint(
                    "CK_MessagingConversations_ContextType_Order",
                    "([ContextType] = 1 AND [OrderId] IS NULL) OR ([ContextType] = 2 AND [OrderId] IS NOT NULL)");
            });

            entity.HasKey(x => x.ConversationId);

            entity.Property(x => x.BuyerUserId)
                .IsRequired();

            entity.Property(x => x.SellerUserId)
                .IsRequired();

            entity.Property(x => x.ContextType)
                .IsRequired()
                .HasConversion<byte>();

            entity.Property(x => x.OrderId);

            entity.Property(x => x.LastMessageAt)
                .HasColumnType("datetime2");

            entity.Property(x => x.BuyerLastReadAt)
                .HasColumnType("datetime2");

            entity.Property(x => x.SellerLastReadAt)
                .HasColumnType("datetime2");

            entity.Property(x => x.CreatedAt)
                .IsRequired()
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.Property(x => x.UpdatedAt)
                .IsRequired()
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.HasOne<ConsumerRef>()
                .WithMany()
                .HasForeignKey(x => x.BuyerUserId)
                .HasPrincipalKey(x => x.ConsumerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasOne<SellerRef>()
                .WithMany()
                .HasForeignKey(x => x.SellerUserId)
                .HasPrincipalKey(x => x.SellerId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.BuyerUserId, x.SellerUserId, x.ContextType })
                .IsUnique()
                .HasFilter("[ContextType] = 1");

            entity.HasIndex(x => new { x.OrderId, x.ContextType })
                .IsUnique()
                .HasFilter("[ContextType] = 2");

            entity.HasIndex(x => x.BuyerUserId);
            entity.HasIndex(x => x.SellerUserId);
            entity.HasIndex(x => x.LastMessageAt);
        });

        // ============== CONVERSATION MESSAGES ==============
        
        modelBuilder.Entity<ConversationMessage>(entity =>
        {
            entity.ToTable("MessagingMessages");
            entity.HasKey(x => x.MessageId);

            entity.Property(x => x.SenderUserId)
                .IsRequired();

            entity.Property(x => x.Body)
                .IsRequired()
                .HasMaxLength(2000);

            entity.Property(x => x.AttachmentUrl)
                .HasMaxLength(400);

            entity.Property(x => x.SentAt)
                .IsRequired()
                .HasDefaultValueSql("SYSUTCDATETIME()");

            entity.Property(x => x.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            entity.HasOne(x => x.Conversation)
                .WithMany(x => x.Messages)
                .HasForeignKey(x => x.ConversationId)
                .OnDelete(DeleteBehavior.Cascade);

            entity.HasOne<PlatformUser>()
                .WithMany()
                .HasForeignKey(x => x.SenderUserId)
                .HasPrincipalKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            entity.HasIndex(x => new { x.ConversationId, x.SentAt })
                .IsDescending(false, true);
        });

        modelBuilder.Entity<SellerNotification>(entity =>
        {
            entity.ToTable("Notifications");
            entity.HasKey(x => x.NotificationId);

            entity.Property(x => x.RecipientType)
                .IsRequired()
                .HasMaxLength(32)
                .HasColumnName("RecipientType");

            entity.Property(x => x.RecipientId)
                .IsRequired()
                .HasMaxLength(64)
                .HasColumnName("RecipientId");

            entity.Ignore(x => x.SellerId);

            entity.Property(x => x.Category)
                .IsRequired()
                .HasMaxLength(64);
            entity.Property(x => x.Category)
                .HasColumnName("category");

            entity.Ignore(x => x.Type);
            entity.Ignore(x => x.Title);

            entity.Property(x => x.Message)
                .IsRequired()
                .HasMaxLength(2000)
                .HasColumnName("Message");

            entity.Property(x => x.CreatedAt)
                .HasColumnType("datetime2")
                .HasDefaultValueSql("SYSUTCDATETIME()")
                .HasColumnName("CreatedAt");

            entity.Property(x => x.OrderId)
                .HasColumnName("OrderId");

            entity.Property(x => x.IsRead)
                .HasColumnName("IsRead");

            entity.Ignore(x => x.ReadAt);
            entity.Ignore(x => x.Priority);
            entity.Ignore(x => x.DeliveryMode);
            entity.Ignore(x => x.ActionRequired);
            entity.Ignore(x => x.LinkType);
            entity.Ignore(x => x.LinkTarget);
            entity.Ignore(x => x.DeduplicationKey);
            entity.Ignore(x => x.MetadataJson);

            entity.HasIndex(x => new { x.RecipientType, x.RecipientId, x.IsRead, x.CreatedAt });
            entity.HasIndex(x => new { x.RecipientType, x.RecipientId, x.Category, x.CreatedAt });
        });
    }
}







