using CTSHIPDashboard.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CTSHIPDashboard.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260714103000_AddReferralVerificationCodes")]
    public partial class AddReferralVerificationCodes : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH(N'Referrals', N'ReferralVerificationCode') IS NULL
                BEGIN
                    ALTER TABLE [Referrals] ADD [ReferralVerificationCode] nvarchar(30) NULL;
                END

                IF COL_LENGTH(N'Referrals', N'ReferralVerificationCodeIssuedAt') IS NULL
                BEGIN
                    ALTER TABLE [Referrals] ADD [ReferralVerificationCodeIssuedAt] datetime2 NULL;
                END

                IF COL_LENGTH(N'Referrals', N'ReferralVerificationCodeExpiresAt') IS NULL
                BEGIN
                    ALTER TABLE [Referrals] ADD [ReferralVerificationCodeExpiresAt] datetime2 NULL;
                END

                IF COL_LENGTH(N'Referrals', N'ReferralVerificationCodeIssuedByUserId') IS NULL
                BEGIN
                    ALTER TABLE [Referrals] ADD [ReferralVerificationCodeIssuedByUserId] nvarchar(450) NULL;
                END

                IF COL_LENGTH(N'Referrals', N'ReferralVerificationCodeIssuedByName') IS NULL
                BEGIN
                    ALTER TABLE [Referrals] ADD [ReferralVerificationCodeIssuedByName] nvarchar(200) NULL;
                END

                IF COL_LENGTH(N'Referrals', N'ReferralVerificationCodeVerifiedAt') IS NULL
                BEGIN
                    ALTER TABLE [Referrals] ADD [ReferralVerificationCodeVerifiedAt] datetime2 NULL;
                END

                IF COL_LENGTH(N'Referrals', N'ReferralVerificationCodeVerifiedByUserId') IS NULL
                BEGIN
                    ALTER TABLE [Referrals] ADD [ReferralVerificationCodeVerifiedByUserId] nvarchar(450) NULL;
                END

                IF COL_LENGTH(N'Referrals', N'ReferralVerificationCodeVerifiedByName') IS NULL
                BEGIN
                    ALTER TABLE [Referrals] ADD [ReferralVerificationCodeVerifiedByName] nvarchar(200) NULL;
                END

                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE [name] = N'IX_Referrals_ReferralVerificationCode'
                        AND [object_id] = OBJECT_ID(N'[Referrals]')
                )
                BEGIN
                    CREATE UNIQUE INDEX [IX_Referrals_ReferralVerificationCode]
                        ON [Referrals] ([ReferralVerificationCode])
                        WHERE [ReferralVerificationCode] IS NOT NULL;
                END
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE [name] = N'IX_Referrals_ReferralVerificationCode'
                        AND [object_id] = OBJECT_ID(N'[Referrals]')
                )
                BEGIN
                    DROP INDEX [IX_Referrals_ReferralVerificationCode] ON [Referrals];
                END

                IF COL_LENGTH(N'Referrals', N'ReferralVerificationCodeVerifiedByName') IS NOT NULL
                BEGIN
                    ALTER TABLE [Referrals] DROP COLUMN [ReferralVerificationCodeVerifiedByName];
                END

                IF COL_LENGTH(N'Referrals', N'ReferralVerificationCodeVerifiedByUserId') IS NOT NULL
                BEGIN
                    ALTER TABLE [Referrals] DROP COLUMN [ReferralVerificationCodeVerifiedByUserId];
                END

                IF COL_LENGTH(N'Referrals', N'ReferralVerificationCodeVerifiedAt') IS NOT NULL
                BEGIN
                    ALTER TABLE [Referrals] DROP COLUMN [ReferralVerificationCodeVerifiedAt];
                END

                IF COL_LENGTH(N'Referrals', N'ReferralVerificationCodeIssuedByName') IS NOT NULL
                BEGIN
                    ALTER TABLE [Referrals] DROP COLUMN [ReferralVerificationCodeIssuedByName];
                END

                IF COL_LENGTH(N'Referrals', N'ReferralVerificationCodeIssuedByUserId') IS NOT NULL
                BEGIN
                    ALTER TABLE [Referrals] DROP COLUMN [ReferralVerificationCodeIssuedByUserId];
                END

                IF COL_LENGTH(N'Referrals', N'ReferralVerificationCodeExpiresAt') IS NOT NULL
                BEGIN
                    ALTER TABLE [Referrals] DROP COLUMN [ReferralVerificationCodeExpiresAt];
                END

                IF COL_LENGTH(N'Referrals', N'ReferralVerificationCodeIssuedAt') IS NOT NULL
                BEGIN
                    ALTER TABLE [Referrals] DROP COLUMN [ReferralVerificationCodeIssuedAt];
                END

                IF COL_LENGTH(N'Referrals', N'ReferralVerificationCode') IS NOT NULL
                BEGIN
                    ALTER TABLE [Referrals] DROP COLUMN [ReferralVerificationCode];
                END
                """);
        }
    }
}
