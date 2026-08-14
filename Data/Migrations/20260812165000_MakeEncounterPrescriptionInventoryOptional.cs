using CTSHIPDashboard.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CTSHIPDashboard.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260812165000_MakeEncounterPrescriptionInventoryOptional")]
    public partial class MakeEncounterPrescriptionInventoryOptional : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EncounterPrescriptions_DrugInventoryItems_DrugInventoryItemId",
                table: "EncounterPrescriptions");

            migrationBuilder.AlterColumn<int>(
                name: "DrugInventoryItemId",
                table: "EncounterPrescriptions",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_EncounterPrescriptions_DrugInventoryItems_DrugInventoryItemId",
                table: "EncounterPrescriptions",
                column: "DrugInventoryItemId",
                principalTable: "DrugInventoryItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EncounterPrescriptions_DrugInventoryItems_DrugInventoryItemId",
                table: "EncounterPrescriptions");

            migrationBuilder.AlterColumn<int>(
                name: "DrugInventoryItemId",
                table: "EncounterPrescriptions",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_EncounterPrescriptions_DrugInventoryItems_DrugInventoryItemId",
                table: "EncounterPrescriptions",
                column: "DrugInventoryItemId",
                principalTable: "DrugInventoryItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}