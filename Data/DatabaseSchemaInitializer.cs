using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Threading;

namespace MyAspNetApp.Data;

internal static class DatabaseSchemaInitializer
{
    public static async Task EnsureCommerceSchemaAsync(AppDbContext dbContext, ILogger logger, CancellationToken cancellationToken = default)
    {
        const string sql = """
            IF OBJECT_ID(N'dbo.ProductColorImages', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.ProductColorImages
                (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    ProductId INT NOT NULL,
                    ColorName NVARCHAR(100) NOT NULL,
                    ImagePath NVARCHAR(400) NOT NULL
                );
                CREATE INDEX IX_ProductColorImages_ProductId ON dbo.ProductColorImages(ProductId);
            END;

            IF OBJECT_ID(N'dbo.Consumers', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.Consumers', N'gender') IS NULL
            BEGIN
                ALTER TABLE dbo.Consumers ADD gender NVARCHAR(50) NULL;
            END;

            IF OBJECT_ID(N'dbo.Consumers', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.Consumers', N'birthday') IS NULL
            BEGIN
                ALTER TABLE dbo.Consumers ADD birthday DATE NULL;
            END;

            IF OBJECT_ID(N'dbo.ProductVariants', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.ProductVariants', N'ImageData') IS NULL
            BEGIN
                ALTER TABLE dbo.ProductVariants ADD ImageData VARBINARY(MAX) NULL;
            END;

            IF OBJECT_ID(N'dbo.ProductVariants', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.ProductVariants', N'ImageMimeType') IS NULL
            BEGIN
                ALTER TABLE dbo.ProductVariants ADD ImageMimeType NVARCHAR(200) NULL;
            END;

            IF OBJECT_ID(N'dbo.Sellers', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Sellers
                (
                    SellerId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    UserId INT NULL,
                    BusinessType NVARCHAR(50) NOT NULL DEFAULT(N'Retail'),
                    ShopName NVARCHAR(120) NOT NULL,
                    Location NVARCHAR(180) NULL,
                    CreatedAt DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME()
                );
            END;

            IF COL_LENGTH(N'dbo.Sellers', N'followers_count') IS NULL
            BEGIN
                ALTER TABLE dbo.Sellers
                    ADD followers_count INT NOT NULL
                        CONSTRAINT DF_Sellers_FollowersCount DEFAULT(0);
            END;

            IF COL_LENGTH(N'dbo.Sellers', N'logo_data') IS NULL
            BEGIN
                ALTER TABLE dbo.Sellers ADD logo_data VARBINARY(MAX) NULL;
            END;

            IF COL_LENGTH(N'dbo.Sellers', N'logo_content_type') IS NULL
            BEGIN
                ALTER TABLE dbo.Sellers ADD logo_content_type NVARCHAR(100) NULL;
            END;

            IF COL_LENGTH(N'dbo.Sellers', N'logo_mime_type') IS NULL
            BEGIN
                ALTER TABLE dbo.Sellers ADD logo_mime_type NVARCHAR(100) NULL;
            END;

            IF OBJECT_ID(N'dbo.SellerFollowers', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.SellerFollowers
                (
                    seller_follower_id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    seller_id INT NOT NULL,
                    follower_user_id INT NOT NULL,
                    followed_at DATETIME2 NOT NULL CONSTRAINT DF_SellerFollowers_FollowedAt DEFAULT SYSUTCDATETIME()
                );

                CREATE UNIQUE INDEX UX_SellerFollowers_SellerFollower
                    ON dbo.SellerFollowers(seller_id, follower_user_id);

                CREATE INDEX IX_SellerFollowers_SellerId
                    ON dbo.SellerFollowers(seller_id);

                CREATE INDEX IX_SellerFollowers_FollowerUserId
                    ON dbo.SellerFollowers(follower_user_id);
            END;

            IF OBJECT_ID(N'dbo.Orders', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Orders
                (
                    OrderId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    OrderNumber NVARCHAR(50) NOT NULL,
                    UserId INT NULL,
                    ConsumerId INT NULL,
                    SellerId INT NULL,
                    FullName NVARCHAR(200) NOT NULL,
                    Email NVARCHAR(200) NULL,
                    PhoneNumber NVARCHAR(50) NULL,
                    StreetAddress NVARCHAR(200) NULL,
                    City NVARCHAR(100) NULL,
                    PostalCode NVARCHAR(30) NULL,
                    DeliveryOption NVARCHAR(50) NULL,
                    PaymentMethod NVARCHAR(50) NULL,
                    Status NVARCHAR(50) NOT NULL DEFAULT(N'Pending'),
                    Subtotal DECIMAL(18,2) NOT NULL,
                    ShippingFee DECIMAL(18,2) NOT NULL,
                    TotalAmount DECIMAL(18,2) NOT NULL,
                    OrderDate DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
                    EstimatedDeliveryDate DATETIME2 NULL,
                    CancellationReason NVARCHAR(400) NULL
                );
                CREATE INDEX IX_Orders_SellerId ON dbo.Orders(SellerId);
                ALTER TABLE dbo.Orders
                    ADD CONSTRAINT FK_Orders_Sellers
                        FOREIGN KEY (SellerId) REFERENCES dbo.Sellers(SellerId)
                        ON DELETE SET NULL;
            END;

            IF OBJECT_ID(N'dbo.OrderItems', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.OrderItems
                (
                    OrderItemId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    OrderId INT NOT NULL,
                    ProductId INT NOT NULL,
                    SellerId INT NULL,
                    Size NVARCHAR(50) NULL,
                    Color NVARCHAR(50) NULL,
                    Quantity INT NOT NULL,
                    UnitPrice DECIMAL(18,2) NOT NULL,
                    CONSTRAINT FK_OrderItems_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders(OrderId) ON DELETE CASCADE
                );
                CREATE INDEX IX_OrderItems_OrderId ON dbo.OrderItems(OrderId);
            END;

            IF OBJECT_ID(N'dbo.MemberUploads', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.MemberUploads
                (
                    UploadId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    UserId INT NOT NULL,
                    Title NVARCHAR(100) NULL,
                    ActivityName NVARCHAR(80) NOT NULL,
                    ActivityDate DATE NOT NULL,
                    ProofUrl NVARCHAR(400) NULL,
                    DistanceKm DECIMAL(8,2) NOT NULL,
                    MovingTimeSec INT NOT NULL,
                    Steps INT NULL,
                    AvgPaceSecPerKm INT NULL,
                    CreatedAt DATETIME2 NOT NULL CONSTRAINT DF_MemberUploads_CreatedAt DEFAULT SYSUTCDATETIME(),
                    UpdatedAt DATETIME2 NULL
                );
                CREATE INDEX IX_MemberUploads_UserId_CreatedAt ON dbo.MemberUploads(UserId, CreatedAt DESC);
            END;

            IF OBJECT_ID(N'dbo.leaderboard_records', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.leaderboard_records
                (
                    Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    UploadId INT NOT NULL,
                    UserId INT NOT NULL,
                    AthleteName NVARCHAR(120) NOT NULL,
                    AvatarUrl NVARCHAR(500) NULL,
                    CoverImageUrl NVARCHAR(500) NULL,
                    DistanceKm DECIMAL(8,2) NOT NULL,
                    DurationSeconds INT NOT NULL,
                    Scope NVARCHAR(50) NOT NULL CONSTRAINT DF_leaderboard_records_Scope DEFAULT(N'National'),
                    CategoryLabel NVARCHAR(80) NOT NULL CONSTRAINT DF_leaderboard_records_Category DEFAULT(N'Current Season'),
                    RankChange INT NOT NULL CONSTRAINT DF_leaderboard_records_RankChange DEFAULT(0),
                    IsVerified BIT NOT NULL CONSTRAINT DF_leaderboard_records_IsVerified DEFAULT(1),
                    IsActive BIT NOT NULL CONSTRAINT DF_leaderboard_records_IsActive DEFAULT(1),
                    CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_leaderboard_records_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
                    ActivityDate DATETIME2 NULL
                );
                CREATE INDEX IX_leaderboard_records_UserId_CreatedAt ON dbo.leaderboard_records(UserId, CreatedAtUtc DESC);
            END;

            IF OBJECT_ID(N'dbo.Notifications', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.Notifications
                (
                    NotificationId INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    RecipientType NVARCHAR(20) NOT NULL,
                    RecipientId INT NULL,
                    OrderId INT NULL,
                    Message NVARCHAR(500) NOT NULL,
                    IsRead BIT NOT NULL CONSTRAINT DF_Notifications_IsRead DEFAULT(0),
                    CreatedAt DATETIME NOT NULL CONSTRAINT DF_Notifications_CreatedAt DEFAULT(GETDATE()),
                    Category NVARCHAR(255) NULL
                );
            END;

            IF NOT EXISTS
            (
                SELECT 1
                FROM sys.indexes
                WHERE object_id = OBJECT_ID(N'dbo.Notifications')
                  AND name = N'UX_Notifications_OrderStatusMessage'
            )
            BEGIN
                CREATE UNIQUE INDEX UX_Notifications_OrderStatusMessage
                    ON dbo.Notifications(RecipientType, RecipientId, OrderId, Message, Category)
                    WHERE OrderId IS NOT NULL
                      AND RecipientId IS NOT NULL
                      AND Category = N'order';
            END;

            EXEC(N'
            CREATE OR ALTER TRIGGER dbo.TR_Orders_OrderNotifications
            ON dbo.Orders
            AFTER INSERT, UPDATE
            AS
            BEGIN
                SET NOCOUNT ON;

                ;WITH ChangedOrders AS
                (
                    SELECT
                        i.OrderId,
                        i.ConsumerId,
                        LTRIM(RTRIM(ISNULL(i.Status, N''''))) AS StatusValue,
                        CASE
                            WHEN LOWER(LTRIM(RTRIM(ISNULL(i.Status, N'''')))) = N''pending''
                                THEN N''Order successfully placed. Order number: '' + CONVERT(NVARCHAR(20), i.OrderId)
                            WHEN LOWER(LTRIM(RTRIM(ISNULL(i.Status, N'''')))) = N''to ship''
                                THEN N''Your shipment is now ready for shipment''
                            WHEN LOWER(LTRIM(RTRIM(ISNULL(i.Status, N'''')))) = N''shipped''
                                THEN N''Your item has been shipped. Check your tracking number''
                            ELSE NULL
                        END AS NotificationMessage
                    FROM inserted AS i
                    LEFT JOIN deleted AS d
                        ON d.OrderId = i.OrderId
                    WHERE i.ConsumerId IS NOT NULL
                      AND
                      (
                          d.OrderId IS NULL
                          OR ISNULL(LTRIM(RTRIM(d.Status)), N'''') <> ISNULL(LTRIM(RTRIM(i.Status)), N'''')
                      )
                ),
                PendingNotifications AS
                (
                    SELECT
                        OrderId,
                        ConsumerId,
                        NotificationMessage
                    FROM ChangedOrders
                    WHERE NotificationMessage IS NOT NULL
                      AND ConsumerId IS NOT NULL
                      AND NOT EXISTS
                      (
                          SELECT 1
                          FROM dbo.Notifications AS n
                          WHERE n.RecipientType = N''User''
                            AND n.RecipientId = ChangedOrders.ConsumerId
                            AND n.OrderId = ChangedOrders.OrderId
                            AND n.Category = N''order''
                            AND n.Message = ChangedOrders.NotificationMessage
                      )
                )
                INSERT INTO dbo.Notifications
                (
                    RecipientType,
                    RecipientId,
                    OrderId,
                    Message,
                    IsRead,
                    CreatedAt,
                    Category
                )
                SELECT
                    N''User'',
                    p.ConsumerId,
                    p.OrderId,
                    p.NotificationMessage,
                    0,
                    GETDATE(),
                    N''order''
                FROM PendingNotifications AS p
                WHERE EXISTS (SELECT 1 FROM PendingNotifications);
            END;
            ');

            IF OBJECT_ID(N'dbo.challengeregistration', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.challengeregistration
                (
                    registration_id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    challenge_id INT NOT NULL,
                    user_id INT NULL,
                    full_name NVARCHAR(200) NOT NULL,
                    email NVARCHAR(200) NOT NULL,
                    phone_number NVARCHAR(50) NULL,
                    city NVARCHAR(120) NULL,
                    notes NVARCHAR(500) NULL,
                    status NVARCHAR(30) NOT NULL CONSTRAINT DF_challengeregistration_status DEFAULT(N'Pending'),
                    submitted_at DATETIME2 NOT NULL CONSTRAINT DF_challengeregistration_submitted_at DEFAULT SYSUTCDATETIME(),
                    approved_at DATETIME2 NULL,
                    approval_notified_at DATETIME2 NULL,
                    reviewed_by INT NULL,
                    admin_remarks NVARCHAR(500) NULL
                );
                CREATE INDEX IX_challengeregistration_challenge_id ON dbo.challengeregistration(challenge_id);
                CREATE INDEX IX_challengeregistration_user_id ON dbo.challengeregistration(user_id);
            END;

            IF OBJECT_ID(N'dbo.challengenotification', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.challengenotification
                (
                    notification_id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    user_id INT NOT NULL,
                    registration_id INT NULL,
                    title NVARCHAR(160) NOT NULL,
                    message NVARCHAR(400) NOT NULL,
                    notification_type NVARCHAR(40) NOT NULL CONSTRAINT DF_challengenotification_type DEFAULT(N'general'),
                    is_read BIT NOT NULL CONSTRAINT DF_challengenotification_is_read DEFAULT(0),
                    created_at DATETIME2 NOT NULL CONSTRAINT DF_challengenotification_created_at DEFAULT SYSUTCDATETIME(),
                    read_at DATETIME2 NULL
                );
                CREATE INDEX IX_challengenotification_user_id_created_at ON dbo.challengenotification(user_id, created_at DESC);
            END;

            IF OBJECT_ID(N'dbo.challenge_participants', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.challenge_participants
                (
                    participant_id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    challenge_id INT NOT NULL,
                    user_id INT NULL,
                    consumer_id INT NULL,
                    total_distance_km DECIMAL(10,2) NULL,
                    total_activities INT NULL,
                    total_time_seconds INT NULL,
                    average_pace DECIMAL(10,2) NULL,
                    rank INT NULL,
                    joined_at DATETIME2(7) NULL,
                    last_activity_date DATETIME2(7) NULL,
                    is_completed BIT NULL,
                    completed_at DATETIME2(7) NULL,
                    status NVARCHAR(50) NOT NULL DEFAULT(N'Pending')
                );
                CREATE INDEX IX_challenge_participants_challenge_id ON dbo.challenge_participants(challenge_id);
                CREATE INDEX IX_challenge_participants_user_id ON dbo.challenge_participants(user_id);
                CREATE INDEX IX_challenge_participants_consumer_id ON dbo.challenge_participants(consumer_id);
            END;

            IF OBJECT_ID(N'dbo.challenge_activities', N'U') IS NULL
            BEGIN
                CREATE TABLE dbo.challenge_activities
                (
                    activity_id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                    participant_id INT NOT NULL,
                    challenge_id INT NOT NULL,
                    user_id INT NULL,
                    activity_date DATETIME2(7) NOT NULL,
                    distance_km DECIMAL(10,2) NOT NULL,
                    duration_seconds INT NOT NULL,
                    average_pace DECIMAL(10,2) NULL,
                    activity_type NVARCHAR(50) NOT NULL,
                    is_verified BIT NULL,
                    verified_by INT NULL,
                    verified_at DATETIME2(7) NULL,
                    notes NVARCHAR(500) NULL,
                    created_at DATETIME2(7) NULL,
                    imageproof VARBINARY(MAX) NULL
                );
                CREATE INDEX IX_challenge_activities_participant_id ON dbo.challenge_activities(participant_id);
                CREATE INDEX IX_challenge_activities_challenge_id ON dbo.challenge_activities(challenge_id);
            END;

            IF OBJECT_ID(N'dbo.leaderboard_records', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH('dbo.leaderboard_records', 'UploadId') IS NULL AND COL_LENGTH('dbo.leaderboard_records', 'upload_id') IS NOT NULL
                    EXEC(N'ALTER TABLE dbo.leaderboard_records ADD UploadId AS CONVERT(int, [upload_id]);');
                IF COL_LENGTH('dbo.leaderboard_records', 'UserId') IS NULL AND COL_LENGTH('dbo.leaderboard_records', 'user_id') IS NOT NULL
                    EXEC(N'ALTER TABLE dbo.leaderboard_records ADD UserId AS CONVERT(int, [user_id]);');
                IF COL_LENGTH('dbo.leaderboard_records', 'AthleteName') IS NULL AND COL_LENGTH('dbo.leaderboard_records', 'athlete_name') IS NOT NULL
                    EXEC(N'ALTER TABLE dbo.leaderboard_records ADD AthleteName AS CONVERT(nvarchar(120), [athlete_name]);');
                IF COL_LENGTH('dbo.leaderboard_records', 'AvatarUrl') IS NULL AND COL_LENGTH('dbo.leaderboard_records', 'avatar_url') IS NOT NULL
                    EXEC(N'ALTER TABLE dbo.leaderboard_records ADD AvatarUrl AS CONVERT(nvarchar(500), [avatar_url]);');
                IF COL_LENGTH('dbo.leaderboard_records', 'CoverImageUrl') IS NULL AND COL_LENGTH('dbo.leaderboard_records', 'cover_image_url') IS NOT NULL
                    EXEC(N'ALTER TABLE dbo.leaderboard_records ADD CoverImageUrl AS CONVERT(nvarchar(500), [cover_image_url]);');
                IF COL_LENGTH('dbo.leaderboard_records', 'DistanceKm') IS NULL AND COL_LENGTH('dbo.leaderboard_records', 'distance_km') IS NOT NULL
                    EXEC(N'ALTER TABLE dbo.leaderboard_records ADD DistanceKm AS CONVERT(decimal(8,2), [distance_km]);');
                IF COL_LENGTH('dbo.leaderboard_records', 'DurationSeconds') IS NULL AND COL_LENGTH('dbo.leaderboard_records', 'duration_seconds') IS NOT NULL
                    EXEC(N'ALTER TABLE dbo.leaderboard_records ADD DurationSeconds AS CONVERT(int, [duration_seconds]);');
                IF COL_LENGTH('dbo.leaderboard_records', 'CreatedAtUtc') IS NULL AND COL_LENGTH('dbo.leaderboard_records', 'created_at_utc') IS NOT NULL
                    EXEC(N'ALTER TABLE dbo.leaderboard_records ADD CreatedAtUtc AS CONVERT(datetime2, [created_at_utc]);');
                IF COL_LENGTH('dbo.leaderboard_records', 'ActivityDate') IS NULL AND COL_LENGTH('dbo.leaderboard_records', 'activity_date') IS NOT NULL
                    EXEC(N'ALTER TABLE dbo.leaderboard_records ADD ActivityDate AS CONVERT(datetime2, [activity_date]);');
            END;

            IF OBJECT_ID(N'dbo.MemberUploads', N'U') IS NOT NULL
            BEGIN
                IF COL_LENGTH('dbo.MemberUploads', 'UploadId') IS NULL AND COL_LENGTH('dbo.MemberUploads', 'upload_id') IS NOT NULL
                    EXEC(N'ALTER TABLE dbo.MemberUploads ADD UploadId AS CONVERT(int, [upload_id]);');
                IF COL_LENGTH('dbo.MemberUploads', 'UserId') IS NULL AND COL_LENGTH('dbo.MemberUploads', 'user_id') IS NOT NULL
                    EXEC(N'ALTER TABLE dbo.MemberUploads ADD UserId AS CONVERT(int, [user_id]);');
                IF COL_LENGTH('dbo.MemberUploads', 'DistanceKm') IS NULL AND COL_LENGTH('dbo.MemberUploads', 'distance_km') IS NOT NULL
                    EXEC(N'ALTER TABLE dbo.MemberUploads ADD DistanceKm AS CONVERT(decimal(8,2), [distance_km]);');
                IF COL_LENGTH('dbo.MemberUploads', 'MovingTimeSec') IS NULL AND COL_LENGTH('dbo.MemberUploads', 'moving_time_sec') IS NOT NULL
                    EXEC(N'ALTER TABLE dbo.MemberUploads ADD MovingTimeSec AS CONVERT(int, [moving_time_sec]);');
                IF COL_LENGTH('dbo.MemberUploads', 'MovingTimeSec') IS NULL AND COL_LENGTH('dbo.MemberUploads', 'duration_seconds') IS NOT NULL
                    EXEC(N'ALTER TABLE dbo.MemberUploads ADD MovingTimeSec AS CONVERT(int, [duration_seconds]);');
                IF COL_LENGTH('dbo.MemberUploads', 'CreatedAt') IS NULL AND COL_LENGTH('dbo.MemberUploads', 'created_at') IS NOT NULL
                    EXEC(N'ALTER TABLE dbo.MemberUploads ADD CreatedAt AS CONVERT(datetime2, [created_at]);');
                IF COL_LENGTH('dbo.MemberUploads', 'ActivityDate') IS NULL AND COL_LENGTH('dbo.MemberUploads', 'activity_date') IS NOT NULL
                    EXEC(N'ALTER TABLE dbo.MemberUploads ADD ActivityDate AS CONVERT(datetime2, [activity_date]);');
                IF COL_LENGTH('dbo.MemberUploads', 'ProofUrl') IS NULL AND COL_LENGTH('dbo.MemberUploads', 'proof_url') IS NOT NULL
                    EXEC(N'ALTER TABLE dbo.MemberUploads ADD ProofUrl AS CONVERT(nvarchar(500), [proof_url]);');
            END;

            IF OBJECT_ID(N'dbo.returns', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.returns', N'ResolutionType') IS NULL
            BEGIN
                ALTER TABLE dbo.returns ADD ResolutionType NVARCHAR(50) NOT NULL CONSTRAINT DF_returns_ResolutionType DEFAULT(N'Refund');
            END;

            IF OBJECT_ID(N'dbo.returns', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.returns', N'ReplacementOrderId') IS NULL
            BEGIN
                ALTER TABLE dbo.returns ADD ReplacementOrderId INT NULL;
            END;

            IF OBJECT_ID(N'dbo.Returns', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.Returns', N'ResolutionType') IS NULL
            BEGIN
                ALTER TABLE dbo.Returns ADD ResolutionType NVARCHAR(50) NOT NULL CONSTRAINT DF_Returns_ResolutionType DEFAULT(N'Refund');
            END;

            IF OBJECT_ID(N'dbo.Returns', N'U') IS NOT NULL AND COL_LENGTH(N'dbo.Returns', N'ReplacementOrderId') IS NULL
            BEGIN
                ALTER TABLE dbo.Returns ADD ReplacementOrderId INT NULL;
            END;
            """;

        try
        {
            await dbContext.Database.ExecuteSqlRawAsync(sql, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unable to ensure commerce schema migrations.");
            throw;
        }
    }
}
