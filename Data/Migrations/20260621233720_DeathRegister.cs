using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CTSHIPDashboard.Migrations
{
    /// <inheritdoc />
    public partial class DeathRegister : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DeathRegisters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EnrolleeId = table.Column<int>(type: "int", nullable: true),
                    EnrolleeNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EnrolleeFullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Gender = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PhoneNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    HmoCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    HmoName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ProviderId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProviderName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DateOfDeath = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TimeOfDeath = table.Column<TimeSpan>(type: "time", nullable: true),
                    PlaceOfDeath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    CauseOfDeath = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CauseCategory = table.Column<int>(type: "int", nullable: false),
                    DeathConfirmedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    DeathConfirmedByDesignation = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DeathConfirmedByPhone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DeathCertificateNumber = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DeathCertificateFilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ProviderRemarks = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedToHmoAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SubmittedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("PK_DeathRegisters", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DeathRegisterAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeathRegisterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Action = table.Column<int>(type: "int", nullable: false),
                    ActionByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ActionByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ActionAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeathRegisterAuditLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeathRegisterAuditLogs_DeathRegisters_DeathRegisterId",
                        column: x => x.DeathRegisterId,
                        principalTable: "DeathRegisters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DeathRegisterAuditLogs_ActionAt",
                table: "DeathRegisterAuditLogs",
                column: "ActionAt");

            migrationBuilder.CreateIndex(
                name: "IX_DeathRegisterAuditLogs_DeathRegisterId",
                table: "DeathRegisterAuditLogs",
                column: "DeathRegisterId");

            migrationBuilder.CreateIndex(
                name: "IX_DeathRegisters_DateOfDeath",
                table: "DeathRegisters",
                column: "DateOfDeath");

            migrationBuilder.CreateIndex(
                name: "IX_DeathRegisters_EnrolleeId",
                table: "DeathRegisters",
                column: "EnrolleeId");

            migrationBuilder.CreateIndex(
                name: "IX_DeathRegisters_EnrolleeNumber",
                table: "DeathRegisters",
                column: "EnrolleeNumber");

            migrationBuilder.CreateIndex(
                name: "IX_DeathRegisters_EnrolleeNumber_Status",
                table: "DeathRegisters",
                columns: new[] { "EnrolleeNumber", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_DeathRegisters_HmoCode",
                table: "DeathRegisters",
                column: "HmoCode");

            migrationBuilder.CreateIndex(
                name: "IX_DeathRegisters_ProviderId",
                table: "DeathRegisters",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_DeathRegisters_Status",
                table: "DeathRegisters",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DeathRegisterAuditLogs");

            migrationBuilder.DropTable(
                name: "DeathRegisters");
        }
    }
}
