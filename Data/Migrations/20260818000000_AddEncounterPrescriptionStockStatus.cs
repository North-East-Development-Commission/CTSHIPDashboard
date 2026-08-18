using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CTSHIPDashboard.Migrations
{
    /// <inheritdoc />
    public partial class AddEncounterPrescriptionStockStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StockStatus",
                table: "EncounterPrescriptions",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Instock");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StockStatus",
                table: "EncounterPrescriptions");
        }
    }
}
