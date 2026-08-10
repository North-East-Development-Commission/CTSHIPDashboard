using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CTSHIPDashboard.Migrations
{
    /// <inheritdoc />
    public partial class SecondaryProviderClaimReporting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ClarificationNote",
                table: "Claims",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HmoCertificationNote",
                table: "Claims",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HmoCertificationStatus",
                table: "Claims",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Not Certified");

            migrationBuilder.AddColumn<DateTime>(
                name: "HmoCertifiedAt",
                table: "Claims",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HmoCertifiedBy",
                table: "Claims",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IhsaVerificationNote",
                table: "Claims",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IhsaVerificationStatus",
                table: "Claims",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Not Ready");

            migrationBuilder.AddColumn<DateTime>(
                name: "IhsaVerifiedAt",
                table: "Claims",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IhsaVerifiedBy",
                table: "Claims",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "OriginalProviderDataJson",
                table: "Claims",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReturnedForClarificationAt",
                table: "Claims",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnedForClarificationBy",
                table: "Claims",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ClaimAuditTrails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClaimId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    PerformedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PerformedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Summary = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OriginalValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValuesJson = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimAuditTrails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimAuditTrails_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ClaimQueries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ClaimId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_ClaimQueries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimQueries_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Claims_HmoCertificationStatus",
                table: "Claims",
                column: "HmoCertificationStatus");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_IhsaVerificationStatus",
                table: "Claims",
                column: "IhsaVerificationStatus");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimAuditTrails_ClaimId",
                table: "ClaimAuditTrails",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimQueries_ClaimId",
                table: "ClaimQueries",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimQueries_Status",
                table: "ClaimQueries",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClaimAuditTrails");

            migrationBuilder.DropTable(
                name: "ClaimQueries");

            migrationBuilder.DropIndex(
                name: "IX_Claims_HmoCertificationStatus",
                table: "Claims");

            migrationBuilder.DropIndex(
                name: "IX_Claims_IhsaVerificationStatus",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "ClarificationNote",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "HmoCertificationNote",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "HmoCertificationStatus",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "HmoCertifiedAt",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "HmoCertifiedBy",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "IhsaVerificationNote",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "IhsaVerificationStatus",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "IhsaVerifiedAt",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "IhsaVerifiedBy",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "OriginalProviderDataJson",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "ReturnedForClarificationAt",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "ReturnedForClarificationBy",
                table: "Claims");
        }
    }
}
