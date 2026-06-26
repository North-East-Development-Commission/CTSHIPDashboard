using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CTSHIPDashboard.Migrations
{
    /// <inheritdoc />
    public partial class AddMonitoringIndicators : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HasDisability",
                table: "Enrollees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsIdp",
                table: "Enrollees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsPregnant",
                table: "Enrollees",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "OtherVulnerableCategory",
                table: "Enrollees",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ProgramMonitoringTargets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Scope = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    TargetEnrollees = table.Column<int>(type: "int", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProgramMonitoringTargets", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProgramMonitoringTargets_Scope",
                table: "ProgramMonitoringTargets",
                column: "Scope",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProgramMonitoringTargets");

            migrationBuilder.DropColumn(
                name: "HasDisability",
                table: "Enrollees");

            migrationBuilder.DropColumn(
                name: "IsIdp",
                table: "Enrollees");

            migrationBuilder.DropColumn(
                name: "IsPregnant",
                table: "Enrollees");

            migrationBuilder.DropColumn(
                name: "OtherVulnerableCategory",
                table: "Enrollees");
        }
    }
}
