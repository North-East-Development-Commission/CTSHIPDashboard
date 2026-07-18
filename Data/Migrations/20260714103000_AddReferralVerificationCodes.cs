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
                    EXEC(N'ALTER TABLE [Referrals] ADD [ReferralVerificationCode] nvarchar(30) NULL;');
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'Referrals', N'ReferralVerificationCodeIssuedAt') IS NULL
                    EXEC(N'ALTER TABLE [Referrals] ADD [ReferralVerificationCodeIssuedAt] datetime2 NULL;');
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'Referrals', N'ReferralVerificationCodeExpiresAt') IS NULL
                    EXEC(N'ALTER TABLE [Referrals] ADD [ReferralVerificationCodeExpiresAt] datetime2 NULL;');
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'Referrals', N'ReferralVerificationCodeIssuedByUserId') IS NULL
                    EXEC(N'ALTER TABLE [Referrals] ADD [ReferralVerificationCodeIssuedByUserId] nvarchar(450) NULL;');
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'Referrals', N'ReferralVerificationCodeIssuedByName') IS NULL
                    EXEC(N'ALTER TABLE [Referrals] ADD [ReferralVerificationCodeIssuedByName] nvarchar(200) NULL;');
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'Referrals', N'ReferralVerificationCodeVerifiedAt') IS NULL
                    EXEC(N'ALTER TABLE [Referrals] ADD [ReferralVerificationCodeVerifiedAt] datetime2 NULL;');
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'Referrals', N'ReferralVerificationCodeVerifiedByUserId') IS NULL
                    EXEC(N'ALTER TABLE [Referrals] ADD [ReferralVerificationCodeVerifiedByUserId] nvarchar(450) NULL;');
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'Referrals', N'ReferralVerificationCodeVerifiedByName') IS NULL
                    EXEC(N'ALTER TABLE [Referrals] ADD [ReferralVerificationCodeVerifiedByName] nvarchar(200) NULL;');
                """);

            migrationBuilder.Sql("""
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE [name] = N'IX_Referrals_ReferralVerificationCode'
                        AND [object_id] = OBJECT_ID(N'[Referrals]')
                )
                    EXEC(N'CREATE UNIQUE INDEX [IX_Referrals_ReferralVerificationCode]
                        ON [Referrals] ([ReferralVerificationCode])
                        WHERE [ReferralVerificationCode] IS NOT NULL;');
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
                    EXEC(N'DROP INDEX [IX_Referrals_ReferralVerificationCode] ON [Referrals];');
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'Referrals', N'ReferralVerificationCodeVerifiedByName') IS NOT NULL
                    EXEC(N'ALTER TABLE [Referrals] DROP COLUMN [ReferralVerificationCodeVerifiedByName];');
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'Referrals', N'ReferralVerificationCodeVerifiedByUserId') IS NOT NULL
                    EXEC(N'ALTER TABLE [Referrals] DROP COLUMN [ReferralVerificationCodeVerifiedByUserId];');
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'Referrals', N'ReferralVerificationCodeVerifiedAt') IS NOT NULL
                    EXEC(N'ALTER TABLE [Referrals] DROP COLUMN [ReferralVerificationCodeVerifiedAt];');
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'Referrals', N'ReferralVerificationCodeIssuedByName') IS NOT NULL
                    EXEC(N'ALTER TABLE [Referrals] DROP COLUMN [ReferralVerificationCodeIssuedByName];');
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'Referrals', N'ReferralVerificationCodeIssuedByUserId') IS NOT NULL
                    EXEC(N'ALTER TABLE [Referrals] DROP COLUMN [ReferralVerificationCodeIssuedByUserId];');
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'Referrals', N'ReferralVerificationCodeExpiresAt') IS NOT NULL
                    EXEC(N'ALTER TABLE [Referrals] DROP COLUMN [ReferralVerificationCodeExpiresAt];');
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'Referrals', N'ReferralVerificationCodeIssuedAt') IS NOT NULL
                    EXEC(N'ALTER TABLE [Referrals] DROP COLUMN [ReferralVerificationCodeIssuedAt];');
                """);

            migrationBuilder.Sql("""
                IF COL_LENGTH(N'Referrals', N'ReferralVerificationCode') IS NOT NULL
                    EXEC(N'ALTER TABLE [Referrals] DROP COLUMN [ReferralVerificationCode];');
                """);
        }
    }
}
