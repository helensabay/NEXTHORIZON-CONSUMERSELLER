-- Check recent products and variants
SELECT TOP 5 ProductId, ProductName, Stock, Sizes FROM Products ORDER BY ProductId DESC;

-- Check ProductVariants
SELECT TOP 10 * FROM ProductVariants ORDER BY Id DESC;

-- Check if there are constraint issues
EXEC sp_helpconstraint 'ProductVariants';
