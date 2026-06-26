using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CTSHIPDashboard.Migrations
{
    /// <inheritdoc />
    public partial class AddEncounterServiceAnalytics : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ServiceSetting",
                table: "Encounters",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Outpatient");

            migrationBuilder.CreateTable(
                name: "EncounterServices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EncounterId = table.Column<int>(type: "int", nullable: false),
                    ServiceSetting = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ServiceName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncounterServices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EncounterServices_Encounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "Encounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EncounterServices_EncounterId",
                table: "EncounterServices",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_EncounterServices_EncounterId_ServiceName",
                table: "EncounterServices",
                columns: new[] { "EncounterId", "ServiceName" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EncounterServices_ServiceName",
                table: "EncounterServices",
                column: "ServiceName");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EncounterServices");

            migrationBuilder.DropColumn(
                name: "ServiceSetting",
                table: "Encounters");
        }
    }
}
