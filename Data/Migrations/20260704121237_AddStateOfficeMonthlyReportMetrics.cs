using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CTSHIPDashboard.Migrations
{
    /// <inheritdoc />
    public partial class AddStateOfficeMonthlyReportMetrics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "AmountCapitationPaid",
                table: "StateOfficeMonthlyReports",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "AuditNote",
                table: "StateOfficeMonthlyReports",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuditStatus",
                table: "StateOfficeMonthlyReports",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<DateTime>(
                name: "AuditedAt",
                table: "StateOfficeMonthlyReports",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuditedByName",
                table: "StateOfficeMonthlyReports",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AuditedByUserId",
                table: "StateOfficeMonthlyReports",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "CapitationToUtilizationRatio",
                table: "StateOfficeMonthlyReports",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "CompletedReferrals",
                table: "StateOfficeMonthlyReports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "EnrolleesAccessingCare",
                table: "StateOfficeMonthlyReports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "PaidClaims",
                table: "StateOfficeMonthlyReports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "PaidClaimsAmount",
                table: "StateOfficeMonthlyReports",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ReferralCompletionRate",
                table: "StateOfficeMonthlyReports",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "ServiceUtilization",
                table: "StateOfficeMonthlyReports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalActiveEnrollees",
                table: "StateOfficeMonthlyReports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalClaims",
                table: "StateOfficeMonthlyReports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalClaimsAmount",
                table: "StateOfficeMonthlyReports",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "TotalEncounters",
                table: "StateOfficeMonthlyReports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalReferrals",
                table: "StateOfficeMonthlyReports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TotalVisits",
                table: "StateOfficeMonthlyReports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_StateOfficeMonthlyReports_AuditStatus",
                table: "StateOfficeMonthlyReports",
                column: "AuditStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StateOfficeMonthlyReports_AuditStatus",
                table: "StateOfficeMonthlyReports");

            migrationBuilder.DropColumn(
                name: "AmountCapitationPaid",
                table: "StateOfficeMonthlyReports");

            migrationBuilder.DropColumn(
                name: "AuditNote",
                table: "StateOfficeMonthlyReports");

            migrationBuilder.DropColumn(
                name: "AuditStatus",
                table: "StateOfficeMonthlyReports");

            migrationBuilder.DropColumn(
                name: "AuditedAt",
                table: "StateOfficeMonthlyReports");

            migrationBuilder.DropColumn(
                name: "AuditedByName",
                table: "StateOfficeMonthlyReports");

            migrationBuilder.DropColumn(
                name: "AuditedByUserId",
                table: "StateOfficeMonthlyReports");

            migrationBuilder.DropColumn(
                name: "CapitationToUtilizationRatio",
                table: "StateOfficeMonthlyReports");

            migrationBuilder.DropColumn(
                name: "CompletedReferrals",
                table: "StateOfficeMonthlyReports");

            migrationBuilder.DropColumn(
                name: "EnrolleesAccessingCare",
                table: "StateOfficeMonthlyReports");

            migrationBuilder.DropColumn(
                name: "PaidClaims",
                table: "StateOfficeMonthlyReports");

            migrationBuilder.DropColumn(
                name: "PaidClaimsAmount",
                table: "StateOfficeMonthlyReports");

            migrationBuilder.DropColumn(
                name: "ReferralCompletionRate",
                table: "StateOfficeMonthlyReports");

            migrationBuilder.DropColumn(
                name: "ServiceUtilization",
                table: "StateOfficeMonthlyReports");

            migrationBuilder.DropColumn(
                name: "TotalActiveEnrollees",
                table: "StateOfficeMonthlyReports");

            migrationBuilder.DropColumn(
                name: "TotalClaims",
                table: "StateOfficeMonthlyReports");

            migrationBuilder.DropColumn(
                name: "TotalClaimsAmount",
                table: "StateOfficeMonthlyReports");

            migrationBuilder.DropColumn(
                name: "TotalEncounters",
                table: "StateOfficeMonthlyReports");

            migrationBuilder.DropColumn(
                name: "TotalReferrals",
                table: "StateOfficeMonthlyReports");

            migrationBuilder.DropColumn(
                name: "TotalVisits",
                table: "StateOfficeMonthlyReports");
        }
    }
}
