using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CTSHIPDashboard.Migrations
{
    /// <inheritdoc />
    public partial class referalMhhhpoii : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                table: "AspNetRoleClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                table: "AspNetUserClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                table: "AspNetUserLogins");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                table: "AspNetUserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                table: "AspNetUserRoles");

            DropForeignKeyIfExists(migrationBuilder, "AspNetUsers", "FK_AspNetUsers_Hmos_HmoId");
            DropForeignKeyIfExists(migrationBuilder, "AspNetUsers", "FK_AspNetUsers_Organizations_OrganizationId");
            DropForeignKeyIfExists(migrationBuilder, "AspNetUsers", "FK_AspNetUsers_Providers_ProviderId");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                table: "AspNetUserTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_Claims_Hmos_HmoId",
                table: "Claims");

            migrationBuilder.DropForeignKey(
                name: "FK_Complaints_Enrollees_EnrolleeId",
                table: "Complaints");

            migrationBuilder.DropForeignKey(
                name: "FK_Complaints_Hmos_HmoId",
                table: "Complaints");

            migrationBuilder.DropForeignKey(
                name: "FK_Complaints_Providers_ProviderId",
                table: "Complaints");

            migrationBuilder.DropForeignKey(
                name: "FK_DeathRegisterAuditLogs_DeathRegisters_DeathRegisterId",
                table: "DeathRegisterAuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Doctors_Providers_ProviderId",
                table: "Doctors");

            migrationBuilder.DropForeignKey(
                name: "FK_Encounters_Claims_ClaimId",
                table: "Encounters");

            migrationBuilder.DropForeignKey(
                name: "FK_EncounterServices_Encounters_EncounterId",
                table: "EncounterServices");

            migrationBuilder.DropForeignKey(
                name: "FK_Enrollees_Providers_ProviderId",
                table: "Enrollees");

            migrationBuilder.DropForeignKey(
                name: "FK_EnrolleeWallets_Enrollees_EnrolleeId",
                table: "EnrolleeWallets");

            migrationBuilder.DropForeignKey(
                name: "FK_Feedbacks_Enrollees_EnrolleeId",
                table: "Feedbacks");

            migrationBuilder.DropForeignKey(
                name: "FK_Feedbacks_Providers_ProviderId",
                table: "Feedbacks");

            migrationBuilder.DropForeignKey(
                name: "FK_MedicalHistories_Enrollees_EnrolleeId",
                table: "MedicalHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_Providers_Hmos_HmoId",
                table: "Providers");

            migrationBuilder.DropForeignKey(
                name: "FK_ReferralAuditLogs_Referrals_ReferralId",
                table: "ReferralAuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Referrals_ReferralHospitals_ReferredHospitalId",
                table: "Referrals");

            migrationBuilder.DropForeignKey(
                name: "FK_UserActivities_AspNetUsers_UserId",
                table: "UserActivities");

            migrationBuilder.DropForeignKey(
                name: "FK_WalletTransactions_EnrolleeWallets_EnrolleeWalletId",
                table: "WalletTransactions");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                table: "AspNetUserClaims",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                table: "AspNetUserLogins",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                table: "AspNetUserRoles",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Hmos_HmoId",
                table: "AspNetUsers",
                column: "HmoId",
                principalTable: "Hmos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Organizations_OrganizeId",
                table: "AspNetUsers",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Providers_ProviderId",
                table: "AspNetUsers",
                column: "ProviderId",
                principalTable: "Providers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                table: "AspNetUserTokens",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Claims_Hmos_HmoId",
                table: "Claims",
                column: "HmoId",
                principalTable: "Hmos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Complaints_Enrollees_EnrolleeId",
                table: "Complaints",
                column: "EnrolleeId",
                principalTable: "Enrollees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Complaints_Hmos_HmoId",
                table: "Complaints",
                column: "HmoId",
                principalTable: "Hmos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Complaints_Providers_ProviderId",
                table: "Complaints",
                column: "ProviderId",
                principalTable: "Providers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeathRegisterAuditLogs_DeathRegisters_DeathRegisterId",
                table: "DeathRegisterAuditLogs",
                column: "DeathRegisterId",
                principalTable: "DeathRegisters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Doctors_Providers_ProviderId",
                table: "Doctors",
                column: "ProviderId",
                principalTable: "Providers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Encounters_Claims_ClaimId",
                table: "Encounters",
                column: "ClaimId",
                principalTable: "Claims",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EncounterServices_Encounters_EncounterId",
                table: "EncounterServices",
                column: "EncounterId",
                principalTable: "Encounters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Enrollees_Providers_ProviderId",
                table: "Enrollees",
                column: "ProviderId",
                principalTable: "Providers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EnrolleeWallets_Enrollees_EnrolleeId",
                table: "EnrolleeWallets",
                column: "EnrolleeId",
                principalTable: "Enrollees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Feedbacks_Enrollees_EnrolleeId",
                table: "Feedbacks",
                column: "EnrolleeId",
                principalTable: "Enrollees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Feedbacks_Providers_ProviderId",
                table: "Feedbacks",
                column: "ProviderId",
                principalTable: "Providers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MedicalHistories_Enrollees_EnrolleeId",
                table: "MedicalHistories",
                column: "EnrolleeId",
                principalTable: "Enrollees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Providers_Hmos_HmoId",
                table: "Providers",
                column: "HmoId",
                principalTable: "Hmos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReferralAuditLogs_Referrals_ReferralId",
                table: "ReferralAuditLogs",
                column: "ReferralId",
                principalTable: "Referrals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Referrals_ReferralHospitals_ReferredHospitalId",
                table: "Referrals",
                column: "ReferredHospitalId",
                principalTable: "ReferralHospitals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserActivities_AspNetUsers_UserId",
                table: "UserActivities",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WalletTransactions_EnrolleeWallets_EnrolleeWalletId",
                table: "WalletTransactions",
                column: "EnrolleeWalletId",
                principalTable: "EnrolleeWallets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                table: "AspNetRoleClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                table: "AspNetUserClaims");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                table: "AspNetUserLogins");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                table: "AspNetUserRoles");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                table: "AspNetUserRoles");

            DropForeignKeyIfExists(migrationBuilder, "AspNetUsers", "FK_AspNetUsers_Hmos_HmoId");
            DropForeignKeyIfExists(migrationBuilder, "AspNetUsers", "FK_AspNetUsers_Organizations_OrganizeId");
            DropForeignKeyIfExists(migrationBuilder, "AspNetUsers", "FK_AspNetUsers_Providers_ProviderId");

            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                table: "AspNetUserTokens");

            migrationBuilder.DropForeignKey(
                name: "FK_Claims_Hmos_HmoId",
                table: "Claims");

            migrationBuilder.DropForeignKey(
                name: "FK_Complaints_Enrollees_EnrolleeId",
                table: "Complaints");

            migrationBuilder.DropForeignKey(
                name: "FK_Complaints_Hmos_HmoId",
                table: "Complaints");

            migrationBuilder.DropForeignKey(
                name: "FK_Complaints_Providers_ProviderId",
                table: "Complaints");

            migrationBuilder.DropForeignKey(
                name: "FK_DeathRegisterAuditLogs_DeathRegisters_DeathRegisterId",
                table: "DeathRegisterAuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Doctors_Providers_ProviderId",
                table: "Doctors");

            migrationBuilder.DropForeignKey(
                name: "FK_Encounters_Claims_ClaimId",
                table: "Encounters");

            migrationBuilder.DropForeignKey(
                name: "FK_EncounterServices_Encounters_EncounterId",
                table: "EncounterServices");

            migrationBuilder.DropForeignKey(
                name: "FK_Enrollees_Providers_ProviderId",
                table: "Enrollees");

            migrationBuilder.DropForeignKey(
                name: "FK_EnrolleeWallets_Enrollees_EnrolleeId",
                table: "EnrolleeWallets");

            migrationBuilder.DropForeignKey(
                name: "FK_Feedbacks_Enrollees_EnrolleeId",
                table: "Feedbacks");

            migrationBuilder.DropForeignKey(
                name: "FK_Feedbacks_Providers_ProviderId",
                table: "Feedbacks");

            migrationBuilder.DropForeignKey(
                name: "FK_MedicalHistories_Enrollees_EnrolleeId",
                table: "MedicalHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_Providers_Hmos_HmoId",
                table: "Providers");

            migrationBuilder.DropForeignKey(
                name: "FK_ReferralAuditLogs_Referrals_ReferralId",
                table: "ReferralAuditLogs");

            migrationBuilder.DropForeignKey(
                name: "FK_Referrals_ReferralHospitals_ReferredHospitalId",
                table: "Referrals");

            migrationBuilder.DropForeignKey(
                name: "FK_UserActivities_AspNetUsers_UserId",
                table: "UserActivities");

            migrationBuilder.DropForeignKey(
                name: "FK_WalletTransactions_EnrolleeWallets_EnrolleeWalletId",
                table: "WalletTransactions");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserClaims_AspNetUsers_UserId",
                table: "AspNetUserClaims",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserLogins_AspNetUsers_UserId",
                table: "AspNetUserLogins",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserRoles_AspNetRoles_RoleId",
                table: "AspNetUserRoles",
                column: "RoleId",
                principalTable: "AspNetRoles",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserRoles_AspNetUsers_UserId",
                table: "AspNetUserRoles",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Hmos_HmoId",
                table: "AspNetUsers",
                column: "HmoId",
                principalTable: "Hmos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict); migrationBuilder.Sql(@"
IF EXISTS (
    SELECT 1 
    FROM sys.foreign_keys 
    WHERE name = 'FK_AspNetUsers_Organizations_OrganizationId'
    AND parent_object_id = OBJECT_ID('AspNetUsers')
)
BEGIN
    ALTER TABLE [AspNetUsers] DROP CONSTRAINT [FK_AspNetUsers_Organizations_OrganizationId]
END
");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Organizations_OrganizeId",
                table: "AspNetUsers",
                column: "OrganizationId",
                principalTable: "Organizations",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_Providers_ProviderId",
                table: "AspNetUsers",
                column: "ProviderId",
                principalTable: "Providers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                table: "AspNetUserTokens",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Claims_Hmos_HmoId",
                table: "Claims",
                column: "HmoId",
                principalTable: "Hmos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Complaints_Enrollees_EnrolleeId",
                table: "Complaints",
                column: "EnrolleeId",
                principalTable: "Enrollees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Complaints_Hmos_HmoId",
                table: "Complaints",
                column: "HmoId",
                principalTable: "Hmos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Complaints_Providers_ProviderId",
                table: "Complaints",
                column: "ProviderId",
                principalTable: "Providers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DeathRegisterAuditLogs_DeathRegisters_DeathRegisterId",
                table: "DeathRegisterAuditLogs",
                column: "DeathRegisterId",
                principalTable: "DeathRegisters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Doctors_Providers_ProviderId",
                table: "Doctors",
                column: "ProviderId",
                principalTable: "Providers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Encounters_Claims_ClaimId",
                table: "Encounters",
                column: "ClaimId",
                principalTable: "Claims",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EncounterServices_Encounters_EncounterId",
                table: "EncounterServices",
                column: "EncounterId",
                principalTable: "Encounters",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Enrollees_Providers_ProviderId",
                table: "Enrollees",
                column: "ProviderId",
                principalTable: "Providers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EnrolleeWallets_Enrollees_EnrolleeId",
                table: "EnrolleeWallets",
                column: "EnrolleeId",
                principalTable: "Enrollees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Feedbacks_Enrollees_EnrolleeId",
                table: "Feedbacks",
                column: "EnrolleeId",
                principalTable: "Enrollees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Feedbacks_Providers_ProviderId",
                table: "Feedbacks",
                column: "ProviderId",
                principalTable: "Providers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MedicalHistories_Enrollees_EnrolleeId",
                table: "MedicalHistories",
                column: "EnrolleeId",
                principalTable: "Enrollees",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Providers_Hmos_HmoId",
                table: "Providers",
                column: "HmoId",
                principalTable: "Hmos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReferralAuditLogs_Referrals_ReferralId",
                table: "ReferralAuditLogs",
                column: "ReferralId",
                principalTable: "Referrals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Referrals_ReferralHospitals_ReferredHospitalId",
                table: "Referrals",
                column: "ReferredHospitalId",
                principalTable: "ReferralHospitals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_UserActivities_AspNetUsers_UserId",
                table: "UserActivities",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WalletTransactions_EnrolleeWallets_EnrolleeWalletId",
                table: "WalletTransactions",
                column: "EnrolleeWalletId",
                principalTable: "EnrolleeWallets",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        private static void DropForeignKeyIfExists(MigrationBuilder migrationBuilder, string tableName, string foreignKeyName)
        {
            migrationBuilder.Sql($@"
IF EXISTS (
    SELECT 1
    FROM sys.foreign_keys
    WHERE name = N'{foreignKeyName}'
    AND parent_object_id = OBJECT_ID(N'{tableName}')
)
BEGIN
    ALTER TABLE [{tableName}] DROP CONSTRAINT [{foreignKeyName}]
END");
        }
    }
}
