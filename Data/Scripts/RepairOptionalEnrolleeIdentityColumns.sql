-- Run as the database owner or a migration account with ALTER permission.
-- Completes 20260916232522_MakeEnrolleeNinAndPhoneOptional without
-- granting the web application additional permissions or changing enrollee rows.
SET NOCOUNT ON;
SET XACT_ABORT ON;

BEGIN TRY
    BEGIN TRANSACTION;

    DECLARE @lockResult int;
    EXEC @lockResult = sys.sp_getapplock
        @Resource = N'__EFMigrationsLock',
        @LockOwner = N'Transaction',
        @LockMode = N'Exclusive',
        @LockTimeout = 60000;
    IF @lockResult < 0
        THROW 51000, 'Could not acquire the EF migration lock. Retry when startup migrations finish.', 1;

    IF OBJECT_ID(N'dbo.Enrollees', N'U') IS NULL
        THROW 51001, 'dbo.Enrollees was not found. Select the application database.', 1;
    IF OBJECT_ID(N'dbo.__EFMigrationsHistory', N'U') IS NULL
        THROW 51002, 'Migration history was not found. Use EF migrations to initialise this database.', 1;
    IF NOT EXISTS (SELECT 1 FROM sys.columns
                   WHERE object_id = OBJECT_ID(N'dbo.Enrollees') AND name = N'Phone'
                     AND system_type_id = TYPE_ID(N'nvarchar') AND max_length = 40)
        THROW 51003, 'Phone has an unexpected type or length. Review the schema before changing it.', 1;
    IF NOT EXISTS (SELECT 1 FROM sys.columns
                   WHERE object_id = OBJECT_ID(N'dbo.Enrollees') AND name = N'NIN'
                     AND system_type_id = TYPE_ID(N'bigint'))
        THROW 51004, 'NIN has an unexpected type. Review the schema before changing it.', 1;

    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Enrollees')
               AND name = N'Phone' AND is_nullable = 0)
        ALTER TABLE dbo.Enrollees ALTER COLUMN Phone nvarchar(20) NULL;
    IF EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'dbo.Enrollees')
               AND name = N'NIN' AND is_nullable = 0)
        ALTER TABLE dbo.Enrollees ALTER COLUMN NIN bigint NULL;

    -- Both changes from this migration have completed in the same transaction.
    IF NOT EXISTS (SELECT 1 FROM dbo.__EFMigrationsHistory
                   WHERE MigrationId = N'20260916232522_MakeEnrolleeNinAndPhoneOptional')
        INSERT INTO dbo.__EFMigrationsHistory (MigrationId, ProductVersion)
        VALUES (N'20260916232522_MakeEnrolleeNinAndPhoneOptional', N'9.0.11');

    COMMIT TRANSACTION;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
    THROW;
END CATCH;

SELECT name AS ColumnName, is_nullable AS AllowsNull
FROM sys.columns
WHERE object_id = OBJECT_ID(N'dbo.Enrollees') AND name IN (N'Phone', N'NIN');
