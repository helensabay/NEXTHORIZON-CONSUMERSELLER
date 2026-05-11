-- Mark the NormalizeProductVariants migration as applied
IF NOT EXISTS (SELECT 1 FROM __EFMigrationsHistory WHERE MigrationId = '20260313032833_NormalizeProductVariants')
BEGIN
    INSERT INTO __EFMigrationsHistory (MigrationId, ProductVersion)
    VALUES ('20260313032833_NormalizeProductVariants', '10.0.3');
    PRINT 'Migration marked as applied';
END
ELSE
BEGIN
    PRINT 'Migration already applied';
END
