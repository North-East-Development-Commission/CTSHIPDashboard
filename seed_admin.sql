SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;

-- Seed Admin Roles
INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp) 
SELECT NEWID(), N'Admin', N'ADMIN', NEWID() 
WHERE NOT EXISTS (SELECT 1 FROM AspNetRoles WHERE Name = N'Admin');

INSERT INTO AspNetRoles (Id, Name, NormalizedName, ConcurrencyStamp) 
SELECT NEWID(), N'CTSHIPAdmin', N'CTSCHIPADMIN', NEWID() 
WHERE NOT EXISTS (SELECT 1 FROM AspNetRoles WHERE Name = N'CTSHIPAdmin');

-- Create Admin User
DECLARE @AdminId NVARCHAR(450) = CAST(NEWID() AS NVARCHAR(450));
DECLARE @AdminRoleId NVARCHAR(450);
DECLARE @CtshipAdminRoleId NVARCHAR(450);

SELECT @AdminRoleId = Id FROM AspNetRoles WHERE Name = N'Admin';
SELECT @CtshipAdminRoleId = Id FROM AspNetRoles WHERE Name = N'CTSHIPAdmin';

IF NOT EXISTS (SELECT 1 FROM AspNetUsers WHERE Email = N'as.maiwada@nedc.gov.ng')
BEGIN
    INSERT INTO AspNetUsers (
        Id, UserName, NormalizedUserName, Email, NormalizedEmail,
        EmailConfirmed, PasswordHash, SecurityStamp, ConcurrencyStamp,
        PhoneNumber, PhoneNumberConfirmed, TwoFactorEnabled, LockoutEnabled,
        LockoutEnd, AccessFailedCount, FullName, State, ContactInfo
    )
    VALUES (
        @AdminId, N'as.maiwada@nedc.gov.ng', N'AS.MAIWADA@NEDC.GOV.NG',
        N'as.maiwada@nedc.gov.ng', N'AS.MAIWADA@NEDC.GOV.NG',
        1, N'AQAAAAIAAYagAAAAEJ4L/3f2xb2xb2xb2xb2xb2xb2xb2xb2xb2xb2xb2xb2xb2xb2xb2xb2xb2xb2xb2xb2xb2xb2xb2xb2xb2xb2xb=', NEWID(), NEWID(),
        NULL, 0, 0, 0,
        NULL, 0, N'CTSHIP Admin', N'Borno', N'0809-000-0001'
    );

    INSERT INTO AspNetUserRoles (UserId, RoleId)
    VALUES (@AdminId, @AdminRoleId);

    INSERT INTO AspNetUserRoles (UserId, RoleId)
    VALUES (@AdminId, @CtshipAdminRoleId);
    
    PRINT 'Admin user created: as.maiwada@nedc.gov.ng';
END
ELSE
    PRINT 'Admin user already exists';

SELECT COUNT(*) AS TotalUsers FROM AspNetUsers;
SELECT Email, FullName, State FROM AspNetUsers;
