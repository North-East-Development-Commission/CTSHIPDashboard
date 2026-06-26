using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CTSHIPDashboard.Migrations
{
    /// <inheritdoc />
    public partial class Refferral : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReferralHospitals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    State = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Lga = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    ContactPerson = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferralHospitals", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Referrals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EncounterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EncounterReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    EnrolleeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EnrolleeNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EnrolleeFullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    HmoCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HmoName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    FromProviderId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FromProviderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReferredHospitalId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Diagnosis = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReasonForReferral = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    ClinicalSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    TreatmentGiven = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    InvestigationSummary = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedToHmoAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    VerifiedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    VerifiedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HmoVerificationNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AuditedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AuditedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AuditedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    AuditNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Referrals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Referrals_ReferralHospitals_ReferredHospitalId",
                        column: x => x.ReferredHospitalId,
                        principalTable: "ReferralHospitals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ReferralAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReferralId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    PerformedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    PerformedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReferralAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReferralAuditLogs_Referrals_ReferralId",
                        column: x => x.ReferralId,
                        principalTable: "Referrals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReferralAuditLogs_ReferralId",
                table: "ReferralAuditLogs",
                column: "ReferralId");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_ReferredHospitalId",
                table: "Referrals",
                column: "ReferredHospitalId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReferralAuditLogs");

            migrationBuilder.DropTable(
                name: "Referrals");

            migrationBuilder.DropTable(
                name: "ReferralHospitals");
        }
    }
}
