using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CTSHIPDashboard.Migrations
{
    /// <inheritdoc />
    public partial class PrimaryProviderEncounterCapitationFlow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "CapitationCharge",
                table: "Encounters",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "ClarificationNote",
                table: "Encounters",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HmoVerificationNote",
                table: "Encounters",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HmoVerificationStatus",
                table: "Encounters",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Submitted");

            migrationBuilder.AddColumn<DateTime>(
                name: "HmoVerifiedAt",
                table: "Encounters",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HmoVerifiedBy",
                table: "Encounters",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IhsaVerificationNote",
                table: "Encounters",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IhsaVerificationStatus",
                table: "Encounters",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Not Ready");

            migrationBuilder.AddColumn<DateTime>(
                name: "IhsaVerifiedAt",
                table: "Encounters",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IhsaVerifiedBy",
                table: "Encounters",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalFacilityDataJson",
                table: "Encounters",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReturnedForClarificationAt",
                table: "Encounters",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnedForClarificationBy",
                table: "Encounters",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedToHmoAt",
                table: "Encounters",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EncounterAuditTrails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EncounterId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PerformedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PerformedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OriginalValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncounterAuditTrails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EncounterAuditTrails_Encounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "Encounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EncounterQueries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EncounterId = table.Column<int>(type: "int", nullable: false),
                    QueryNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Open"),
                    QueryRaised = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    ResponsiblePerson = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Response = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Resolution = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ClosureNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RaisedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RaisedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RespondedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RespondedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResolvedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ClosedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncounterQueries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EncounterQueries_Encounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "Encounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Encounters_HmoVerificationStatus",
                table: "Encounters",
                column: "HmoVerificationStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Encounters_IhsaVerificationStatus",
                table: "Encounters",
                column: "IhsaVerificationStatus");

            migrationBuilder.CreateIndex(
                name: "IX_EncounterAuditTrails_EncounterId",
                table: "EncounterAuditTrails",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_EncounterQueries_EncounterId",
                table: "EncounterQueries",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_EncounterQueries_Status",
                table: "EncounterQueries",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EncounterAuditTrails");

            migrationBuilder.DropTable(
                name: "EncounterQueries");

            migrationBuilder.DropIndex(
                name: "IX_Encounters_HmoVerificationStatus",
                table: "Encounters");

            migrationBuilder.DropIndex(
                name: "IX_Encounters_IhsaVerificationStatus",
                table: "Encounters");

            migrationBuilder.DropColumn(
                name: "CapitationCharge",
                table: "Encounters");

            migrationBuilder.DropColumn(
                name: "ClarificationNote",
                table: "Encounters");

            migrationBuilder.DropColumn(
                name: "HmoVerificationNote",
                table: "Encounters");

            migrationBuilder.DropColumn(
                name: "HmoVerificationStatus",
                table: "Encounters");

            migrationBuilder.DropColumn(
                name: "HmoVerifiedAt",
                table: "Encounters");

            migrationBuilder.DropColumn(
                name: "HmoVerifiedBy",
                table: "Encounters");

            migrationBuilder.DropColumn(
                name: "IhsaVerificationNote",
                table: "Encounters");

            migrationBuilder.DropColumn(
                name: "IhsaVerificationStatus",
                table: "Encounters");

            migrationBuilder.DropColumn(
                name: "IhsaVerifiedAt",
                table: "Encounters");

            migrationBuilder.DropColumn(
                name: "IhsaVerifiedBy",
                table: "Encounters");

            migrationBuilder.DropColumn(
                name: "OriginalFacilityDataJson",
                table: "Encounters");

            migrationBuilder.DropColumn(
                name: "ReturnedForClarificationAt",
                table: "Encounters");

            migrationBuilder.DropColumn(
                name: "ReturnedForClarificationBy",
                table: "Encounters");

            migrationBuilder.DropColumn(
                name: "SubmittedToHmoAt",
                table: "Encounters");
        }
    }
}
