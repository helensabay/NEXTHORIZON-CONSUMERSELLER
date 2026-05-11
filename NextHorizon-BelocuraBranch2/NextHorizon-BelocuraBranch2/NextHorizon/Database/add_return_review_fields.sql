IF COL_LENGTH('dbo.returns', 'SellerDecisionReason') IS NULL
BEGIN
    ALTER TABLE dbo.returns
    ADD SellerDecisionReason NVARCHAR(150) NULL;
END
GO

IF COL_LENGTH('dbo.returns', 'SellerDecisionNote') IS NULL
BEGIN
    ALTER TABLE dbo.returns
    ADD SellerDecisionNote NVARCHAR(MAX) NULL;
END
GO

IF COL_LENGTH('dbo.returns', 'ReviewedAt') IS NULL
BEGIN
    ALTER TABLE dbo.returns
    ADD ReviewedAt DATETIME2 NULL;
END
GO
