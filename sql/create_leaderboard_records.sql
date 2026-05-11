SET ANSI_NULLS ON;
GO
SET QUOTED_IDENTIFIER ON;
GO

IF OBJECT_ID(N'dbo.trg_memberuploads_sync_leaderboard_records', N'TR') IS NOT NULL
BEGIN
    DROP TRIGGER dbo.trg_memberuploads_sync_leaderboard_records;
END;
GO

IF OBJECT_ID(N'dbo.leaderboard_records', N'V') IS NOT NULL
BEGIN
    DROP VIEW dbo.leaderboard_records;
END;
GO

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
        Scope NVARCHAR(50) NOT NULL CONSTRAINT DF_leaderboard_records_Scope DEFAULT N'National',
        CategoryLabel NVARCHAR(80) NOT NULL CONSTRAINT DF_leaderboard_records_CategoryLabel DEFAULT N'Member Uploads',
        RankChange INT NOT NULL CONSTRAINT DF_leaderboard_records_RankChange DEFAULT 0,
        IsVerified BIT NOT NULL CONSTRAINT DF_leaderboard_records_IsVerified DEFAULT 1,
        IsActive BIT NOT NULL CONSTRAINT DF_leaderboard_records_IsActive DEFAULT 1,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_leaderboard_records_CreatedAtUtc DEFAULT SYSUTCDATETIME(),
        ActivityDate DATETIME2 NULL
    );
END;
GO

IF COL_LENGTH('dbo.leaderboard_records', 'UploadId') IS NULL
    ALTER TABLE dbo.leaderboard_records ADD UploadId INT NULL;
GO
IF COL_LENGTH('dbo.leaderboard_records', 'UserId') IS NULL
    ALTER TABLE dbo.leaderboard_records ADD UserId INT NULL;
GO
IF COL_LENGTH('dbo.leaderboard_records', 'ActivityDate') IS NULL
    ALTER TABLE dbo.leaderboard_records ADD ActivityDate DATETIME2 NULL;
GO

IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'UX_leaderboard_records_UploadId' AND object_id = OBJECT_ID('dbo.leaderboard_records'))
BEGIN
    CREATE UNIQUE INDEX UX_leaderboard_records_UploadId
        ON dbo.leaderboard_records(UploadId)
        WHERE UploadId IS NOT NULL;
END;
GO

IF NOT EXISTS (SELECT 1 FROM sys.foreign_keys WHERE name = 'FK_leaderboard_records_MemberUploads_UploadId')
BEGIN
    ALTER TABLE dbo.leaderboard_records
    ADD CONSTRAINT FK_leaderboard_records_MemberUploads_UploadId
        FOREIGN KEY (UploadId) REFERENCES dbo.MemberUploads(UploadId)
        ON DELETE CASCADE;
END;
GO

;WITH source_rows AS
(
    SELECT
        mu.UploadId,
        mu.UserId,
        COALESCE(
            NULLIF(LTRIM(RTRIM(c.username)), ''),
            NULLIF(
                LTRIM(RTRIM(
                    CONCAT(
                        COALESCE(c.first_name, ''),
                        CASE
                            WHEN NULLIF(LTRIM(RTRIM(c.last_name)), '') IS NULL THEN ''
                            ELSE CASE
                                WHEN NULLIF(LTRIM(RTRIM(c.first_name)), '') IS NULL THEN ''
                                ELSE ' '
                            END + LTRIM(RTRIM(c.last_name))
                        END
                    )
                )),
                ''
            ),
            CONCAT('Runner ', mu.UserId)
        ) AS AthleteName,
        CAST(NULL AS NVARCHAR(500)) AS AvatarUrl,
        CAST(NULL AS NVARCHAR(500)) AS CoverImageUrl,
        CAST(COALESCE(mu.DistanceKm, 0) AS DECIMAL(8,2)) AS DistanceKm,
        COALESCE(mu.MovingTimeSec, 0) AS DurationSeconds,
        N'National' AS Scope,
        N'Member Uploads' AS CategoryLabel,
        0 AS RankChange,
        CAST(1 AS bit) AS IsVerified,
        CAST(1 AS bit) AS IsActive,
        COALESCE(mu.CreatedAt, mu.ActivityDate, SYSUTCDATETIME()) AS CreatedAtUtc,
        mu.ActivityDate
    FROM dbo.MemberUploads mu
    LEFT JOIN dbo.Consumers c
        ON c.user_id = mu.UserId
    LEFT JOIN dbo.Users u
        ON u.user_id = mu.UserId
)
MERGE dbo.leaderboard_records AS target
USING source_rows AS source
    ON target.UploadId = source.UploadId
WHEN MATCHED THEN
    UPDATE SET
        target.UserId = source.UserId,
        target.AthleteName = source.AthleteName,
        target.AvatarUrl = source.AvatarUrl,
        target.CoverImageUrl = source.CoverImageUrl,
        target.DistanceKm = source.DistanceKm,
        target.DurationSeconds = source.DurationSeconds,
        target.Scope = source.Scope,
        target.CategoryLabel = source.CategoryLabel,
        target.RankChange = source.RankChange,
        target.IsVerified = source.IsVerified,
        target.IsActive = source.IsActive,
        target.CreatedAtUtc = source.CreatedAtUtc,
        target.ActivityDate = source.ActivityDate
WHEN NOT MATCHED BY TARGET THEN
    INSERT (UploadId, UserId, AthleteName, AvatarUrl, CoverImageUrl, DistanceKm, DurationSeconds, Scope, CategoryLabel, RankChange, IsVerified, IsActive, CreatedAtUtc, ActivityDate)
    VALUES (source.UploadId, source.UserId, source.AthleteName, source.AvatarUrl, source.CoverImageUrl, source.DistanceKm, source.DurationSeconds, source.Scope, source.CategoryLabel, source.RankChange, source.IsVerified, source.IsActive, source.CreatedAtUtc, source.ActivityDate)
WHEN NOT MATCHED BY SOURCE THEN
    DELETE;
GO

CREATE TRIGGER dbo.trg_memberuploads_sync_leaderboard_records
ON dbo.MemberUploads
AFTER INSERT, UPDATE, DELETE
AS
BEGIN
    SET NOCOUNT ON;

    ;WITH changed_uploads AS
    (
        SELECT UploadId FROM inserted
        UNION
        SELECT UploadId FROM deleted
    ),
    source_rows AS
    (
        SELECT
            mu.UploadId,
            mu.UserId,
            COALESCE(
                NULLIF(LTRIM(RTRIM(c.username)), ''),
                NULLIF(
                    LTRIM(RTRIM(
                        CONCAT(
                            COALESCE(c.first_name, ''),
                            CASE
                                WHEN NULLIF(LTRIM(RTRIM(c.last_name)), '') IS NULL THEN ''
                                ELSE CASE
                                    WHEN NULLIF(LTRIM(RTRIM(c.first_name)), '') IS NULL THEN ''
                                    ELSE ' '
                                END + LTRIM(RTRIM(c.last_name))
                            END
                        )
                    )),
                    ''
                ),
                CONCAT('Runner ', mu.UserId)
            ) AS AthleteName,
            CAST(NULL AS NVARCHAR(500)) AS AvatarUrl,
            CAST(NULL AS NVARCHAR(500)) AS CoverImageUrl,
            CAST(COALESCE(mu.DistanceKm, 0) AS DECIMAL(8,2)) AS DistanceKm,
            COALESCE(mu.MovingTimeSec, 0) AS DurationSeconds,
            N'National' AS Scope,
            N'Member Uploads' AS CategoryLabel,
            0 AS RankChange,
            CAST(1 AS bit) AS IsVerified,
            CAST(1 AS bit) AS IsActive,
            COALESCE(mu.CreatedAt, mu.ActivityDate, SYSUTCDATETIME()) AS CreatedAtUtc,
            mu.ActivityDate
        FROM dbo.MemberUploads mu
        INNER JOIN changed_uploads cu
            ON cu.UploadId = mu.UploadId
        LEFT JOIN dbo.Consumers c
            ON c.user_id = mu.UserId
        LEFT JOIN dbo.Users u
            ON u.user_id = mu.UserId
    )
    MERGE dbo.leaderboard_records AS target
    USING source_rows AS source
        ON target.UploadId = source.UploadId
    WHEN MATCHED THEN
        UPDATE SET
            target.UserId = source.UserId,
            target.AthleteName = source.AthleteName,
            target.AvatarUrl = source.AvatarUrl,
            target.CoverImageUrl = source.CoverImageUrl,
            target.DistanceKm = source.DistanceKm,
            target.DurationSeconds = source.DurationSeconds,
            target.Scope = source.Scope,
            target.CategoryLabel = source.CategoryLabel,
            target.RankChange = source.RankChange,
            target.IsVerified = source.IsVerified,
            target.IsActive = source.IsActive,
            target.CreatedAtUtc = source.CreatedAtUtc,
            target.ActivityDate = source.ActivityDate
    WHEN NOT MATCHED BY TARGET THEN
        INSERT (UploadId, UserId, AthleteName, AvatarUrl, CoverImageUrl, DistanceKm, DurationSeconds, Scope, CategoryLabel, RankChange, IsVerified, IsActive, CreatedAtUtc, ActivityDate)
        VALUES (source.UploadId, source.UserId, source.AthleteName, source.AvatarUrl, source.CoverImageUrl, source.DistanceKm, source.DurationSeconds, source.Scope, source.CategoryLabel, source.RankChange, source.IsVerified, source.IsActive, source.CreatedAtUtc, source.ActivityDate);

    DELETE lr
    FROM dbo.leaderboard_records lr
    INNER JOIN deleted d
        ON d.UploadId = lr.UploadId
    LEFT JOIN inserted i
        ON i.UploadId = d.UploadId
    WHERE i.UploadId IS NULL;
END;
GO
