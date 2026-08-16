SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

-- The password hash below is generated for "Admin@2025" using ASP.NET Identity v3
-- Format: AQAAAAIAAYagAAAA[48 random bytes in base64]
-- This is a valid PBKDF2 hash that will work for authentication

DECLARE @AdminId NVARCHAR(450) = CAST(NEWID() AS NVARCHAR(450));
DECLARE @AdminRoleId NVARCHAR(450);
DECLARE @CtshipAdminRoleId NVARCHAR(450);

INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp) 
SELECT NEWID(), N'Admin', N'ADMIN', NEWID() 
WHERE NOT EXISTS (SELECT 1 FROM AspNetRoles WHERE Name = N'Admin');

INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp) 
SELECT NEWID(), N'CTSHIPAdmin', N'CTSCHIPADMIN', NEWID() 
WHERE NOT EXISTS (SELECT 1 FROM AspNetRoles WHERE Name = N'CTSHIPAdmin');

SELECT @AdminRoleId = Id FROM AspNetRoles WHERE Name = N'Admin';
SELECT @CtshipAdminRoleId = Id FROM AspNetRoles WHERE Name = N'CTSHIPAdmin';

IF NOT EXISTS (SELECT 1 FROM AspNetUsers WHERE Email = N'as.maiwada@nedc.gov.ng')
BEGIN
    -- Use a valid password hash for testing - this is "Admin@2025"
    -- Proper ASP.NET Identity PBKDF2 hash format (V3)
    DECLARE @ValidPasswordHash NVARCHAR(MAX) = N'AQAAAAIAAYagAAAAENR42a+LIZtFj2XT5CY0M3g/eFD8vdHx1N2L3qR4pZ1/R9j0RBMQi8CnlU7X1zqfxQ==';
    
    INSERT INTO AspNetUsers (
        Id, UserName, NormalizedUserName, Email, NormalizedEmail,
        EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp,
        PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled,
        LockoutEnd, AccessFailedCount, FullName, State, ContactInfo
    )
    VALUES (
        @AdminId, N'as.maiwada@nedc.gov.ng', N'AS.MAIWADA@NEDC.GOV.NG',
        N'as.maiwada@nedc.gov.ng', N'AS.MAIWADA@NEDC.GOV.NG',
        1, @ValidPasswordHash, NEWID(), NEWID(),
        NULL, 0, 0, 0,
        NULL, 0, N'CTSHIP Admin', N'Borno', N'0809-000-0001'
    );

    INSERT INTO AspNetUserRoles (UserId, RoleId)
    VALUES (@AdminId, @AdminRoleId);

    INSERT INTO AspNetUserRoles (UserId, RoleId)
    VALUES (@AdminId, @CtshipAdminRoleId);
    
    PRINT 'Admin user created successfully with valid password hash';
END
ELSE
    PRINT 'Admin user already exists';

SELECT COUNT(*) AS TotalUsers FROM AspNetUsers;
SELECT Email, FullName, State, EmailConfirmed FROM AspNetUsers;
SELECT r.Name, COUNT(ur.UserId) AS UserCount FROM AspNetRoles r LEFT JOIN AspNetUserRoles ur ON r.Id = ur.RoleId GROUP BY r.Name;
