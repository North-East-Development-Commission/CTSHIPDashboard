using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CTSHIPDashboard.Migrations
{
    /// <inheritdoc />
    public partial class AddDrugInventoryAndAppNotifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReasonForEncounter",
                table: "Encounters",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "Acute illness");

            migrationBuilder.CreateTable(
                name: "AppNotifications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TargetGroup = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EventName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Url = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Icon = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppNotifications", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DrugInventoryItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProviderId = table.Column<int>(type: "int", nullable: false),
                    DrugName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Strength = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DosageForm = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    QuantityOnHand = table.Column<int>(type: "int", nullable: false),
                    ReorderLevel = table.Column<int>(type: "int", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrugInventoryItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DrugInventoryItems_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AppNotificationReads",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AppNotificationId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AppNotificationReads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AppNotificationReads_AppNotifications_AppNotificationId",
                        column: x => x.AppNotificationId,
                        principalTable: "AppNotifications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "EncounterPrescriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EncounterId = table.Column<int>(type: "int", nullable: false),
                    DrugInventoryItemId = table.Column<int>(type: "int", nullable: false),
                    DrugName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Strength = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DosageForm = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UnitOfMeasure = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    QuantityDispensed = table.Column<int>(type: "int", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    InventoryDeducted = table.Column<bool>(type: "bit", nullable: false),
                    DispensedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncounterPrescriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EncounterPrescriptions_DrugInventoryItems_DrugInventoryItemId",
                        column: x => x.DrugInventoryItemId,
                        principalTable: "DrugInventoryItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EncounterPrescriptions_Encounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "Encounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AppNotificationReads_AppNotificationId_UserId",
                table: "AppNotificationReads",
                columns: new[] { "AppNotificationId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AppNotificationReads_UserId",
                table: "AppNotificationReads",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AppNotifications_CreatedAt",
                table: "AppNotifications",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_AppNotifications_TargetGroup",
                table: "AppNotifications",
                column: "TargetGroup");

            migrationBuilder.CreateIndex(
                name: "IX_DrugInventoryItems_IsActive",
                table: "DrugInventoryItems",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_DrugInventoryItems_ProviderId",
                table: "DrugInventoryItems",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_DrugInventoryItems_ProviderId_DrugName_Strength_DosageForm",
                table: "DrugInventoryItems",
                columns: new[] { "ProviderId", "DrugName", "Strength", "DosageForm" },
                unique: true,
                filter: "[Strength] IS NOT NULL AND [DosageForm] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EncounterPrescriptions_DrugInventoryItemId",
                table: "EncounterPrescriptions",
                column: "DrugInventoryItemId");

            migrationBuilder.CreateIndex(
                name: "IX_EncounterPrescriptions_EncounterId",
                table: "EncounterPrescriptions",
                column: "EncounterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AppNotificationReads");

            migrationBuilder.DropTable(
                name: "EncounterPrescriptions");

            migrationBuilder.DropTable(
                name: "AppNotifications");

            migrationBuilder.DropTable(
                name: "DrugInventoryItems");

            migrationBuilder.DropColumn(
                name: "ReasonForEncounter",
                table: "Encounters");
        }
    }
}
