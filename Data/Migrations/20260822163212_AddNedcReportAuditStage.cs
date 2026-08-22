using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CTSHIPDashboard.Migrations
{
    /// <inheritdoc />
    public partial class AddNedcReportAuditStage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NedcAuditNote",
                table: "StateOfficeMonthlyReports",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NedcAuditStatus",
                table: "StateOfficeMonthlyReports",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.AddColumn<DateTime>(
                name: "NedcAuditedAt",
                table: "StateOfficeMonthlyReports",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NedcAuditedByName",
                table: "StateOfficeMonthlyReports",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NedcAuditedByUserId",
                table: "StateOfficeMonthlyReports",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);


            migrationBuilder.CreateIndex(
                name: "IX_StateOfficeMonthlyReports_NedcAuditStatus",
                table: "StateOfficeMonthlyReports",
                column: "NedcAuditStatus");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StateOfficeMonthlyReports_NedcAuditStatus",
                table: "StateOfficeMonthlyReports");

            migrationBuilder.DropColumn(
                name: "NedcAuditNote",
                table: "StateOfficeMonthlyReports");

            migrationBuilder.DropColumn(
                name: "NedcAuditStatus",
                table: "StateOfficeMonthlyReports");

            migrationBuilder.DropColumn(
                name: "NedcAuditedAt",
                table: "StateOfficeMonthlyReports");

            migrationBuilder.DropColumn(
                name: "NedcAuditedByName",
                table: "StateOfficeMonthlyReports");

            migrationBuilder.DropColumn(
                name: "NedcAuditedByUserId",
                table: "StateOfficeMonthlyReports");

        }
    }
}
