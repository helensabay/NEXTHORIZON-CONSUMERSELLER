IF COL_LENGTH('dbo.Reviews', 'SellerReply') IS NULL
BEGIN
    ALTER TABLE dbo.Reviews
    ADD SellerReply NVARCHAR(MAX) NULL;
END
GO

IF COL_LENGTH('dbo.Reviews', 'SellerReplyDate') IS NULL
BEGIN
    ALTER TABLE dbo.Reviews
    ADD SellerReplyDate DATETIME2 NULL;
END
GO
