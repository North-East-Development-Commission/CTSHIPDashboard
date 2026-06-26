-- Manual SQL migration script
-- WARNING: Altering column types can cause data loss. Run in a safe environment and backup database.
-- 1) Create wallet tables

IF OBJECT_ID('dbo.EnrolleeWallets', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.EnrolleeWallets (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        EnrolleeId INT NOT NULL,
        Balance DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        MonthlyAllocation DECIMAL(18,2) NOT NULL DEFAULT 0.00,
        LastDisbursedAt DATETIME NULL
    );
END

IF OBJECT_ID('dbo.WalletTransactions', 'U') IS NULL
BEGIN
    CREATE TABLE dbo.WalletTransactions (
        Id INT IDENTITY(1,1) PRIMARY KEY,
        EnrolleeWalletId INT NOT NULL,
        Amount DECIMAL(18,2) NOT NULL,
        Type NVARCHAR(100) NULL,
        Reference NVARCHAR(200) NULL,
        Timestamp DATETIME NOT NULL DEFAULT GETUTCDATE(),
        CONSTRAINT FK_WalletTransactions_EnrolleeWallet FOREIGN KEY (EnrolleeWalletId) REFERENCES dbo.EnrolleeWallets(Id)
    );
END

-- 2) Alter DeathRegisters.EnrolleeId from uniqueidentifier to int (if safe)
-- Note: This operation is potentially destructive. If your DeathRegisters currently store GUIDs representing Enrollee identifiers, you must map them to integer Enrollee.Id values before running the ALTER.
-- Example approach:
-- a) Add a new temporary column EnrolleeIdInt INT NULL
-- b) Populate EnrolleeIdInt by joining Enrollees on EnrollmentNumber or other mapping
-- c) Verify data
-- d) Drop foreign constraint(s) if any; then ALTER COLUMN
-- e) Drop old GUID column if desired

IF COL_LENGTH('dbo.DeathRegisters', 'EnrolleeIdInt') IS NULL
BEGIN
    ALTER TABLE dbo.DeathRegisters ADD EnrolleeIdInt INT NULL;
    -- Example population (customize mapping):
    -- UPDATE D SET EnrolleeIdInt = E.Id FROM dbo.DeathRegisters D JOIN dbo.Enrollees E ON D.EnrolleeNumber = E.EnrollmentNumber;
END

PRINT 'Manual migration script created. Review and run the population SQL before altering column types.'
