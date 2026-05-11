CREATE OR ALTER PROCEDURE [dbo].[sp_GetLatestCustomers]
    @Top INT = 10
AS
BEGIN
    SET NOCOUNT ON;

    SELECT TOP (@Top)
        [Id],
        [FullName],
        [Email],
        [CreatedUtc]
    FROM [dbo].[Customers]
    ORDER BY [CreatedUtc] DESC;
END;
GO
