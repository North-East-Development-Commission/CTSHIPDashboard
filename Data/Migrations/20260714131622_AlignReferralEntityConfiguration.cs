using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CTSHIPDashboard.Migrations
{
    /// <inheritdoc />
    public partial class AlignReferralEntityConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            CreateIndexIfMissing(migrationBuilder, "IX_Referrals_CreatedAt", "Referrals", "CreatedAt");
            CreateIndexIfMissing(migrationBuilder, "IX_Referrals_EnrolleeNumber", "Referrals", "EnrolleeNumber");
            CreateIndexIfMissing(migrationBuilder, "IX_Referrals_FromProviderId", "Referrals", "FromProviderId");
            CreateIndexIfMissing(migrationBuilder, "IX_Referrals_HmoCode", "Referrals", "HmoCode");
            CreateIndexIfMissing(migrationBuilder, "IX_Referrals_Status", "Referrals", "Status");
            CreateIndexIfMissing(migrationBuilder, "IX_ReferralHospitals_Name", "ReferralHospitals", "Name");
            CreateIndexIfMissing(migrationBuilder, "IX_ReferralHospitals_State", "ReferralHospitals", "State");
            CreateIndexIfMissing(migrationBuilder, "IX_ReferralAuditLogs_CreatedAt", "ReferralAuditLogs", "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            DropIndexIfExists(migrationBuilder, "IX_ReferralAuditLogs_CreatedAt", "ReferralAuditLogs");
            DropIndexIfExists(migrationBuilder, "IX_ReferralHospitals_State", "ReferralHospitals");
            DropIndexIfExists(migrationBuilder, "IX_ReferralHospitals_Name", "ReferralHospitals");
            DropIndexIfExists(migrationBuilder, "IX_Referrals_Status", "Referrals");
            DropIndexIfExists(migrationBuilder, "IX_Referrals_HmoCode", "Referrals");
            DropIndexIfExists(migrationBuilder, "IX_Referrals_FromProviderId", "Referrals");
            DropIndexIfExists(migrationBuilder, "IX_Referrals_EnrolleeNumber", "Referrals");
            DropIndexIfExists(migrationBuilder, "IX_Referrals_CreatedAt", "Referrals");
        }

        private static void CreateIndexIfMissing(
            MigrationBuilder migrationBuilder,
            string indexName,
            string tableName,
            string columnName)
        {
            migrationBuilder.Sql($"""
                IF NOT EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE [name] = N'{indexName}'
                        AND [object_id] = OBJECT_ID(N'[{tableName}]')
                )
                    EXEC(N'CREATE INDEX [{indexName}] ON [{tableName}] ([{columnName}]);');
                """);
        }

        private static void DropIndexIfExists(
            MigrationBuilder migrationBuilder,
            string indexName,
            string tableName)
        {
            migrationBuilder.Sql($"""
                IF EXISTS (
                    SELECT 1
                    FROM sys.indexes
                    WHERE [name] = N'{indexName}'
                        AND [object_id] = OBJECT_ID(N'[{tableName}]')
                )
                    EXEC(N'DROP INDEX [{indexName}] ON [{tableName}];');
                """);
        }
    }
}
