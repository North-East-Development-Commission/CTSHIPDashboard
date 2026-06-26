using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CTSHIPDashboard.Migrations
{
    /// <inheritdoc />
    public partial class AddStateOfficeMonthlyReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "StateOfficeMonthlyReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReportingMonth = table.Column<DateTime>(type: "date", nullable: false),
                    State = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Lga = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Ward = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ProviderId = table.Column<int>(type: "int", nullable: false),
                    FacilityName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FacilityCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ReportingOfficerName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Designation = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    PhoneNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DateSubmitted = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SubmittedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SubmittedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StateOfficeMonthlyReports", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StateOfficeMonthlyReports_ProviderId",
                table: "StateOfficeMonthlyReports",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_StateOfficeMonthlyReports_ReportingMonth",
                table: "StateOfficeMonthlyReports",
                column: "ReportingMonth");

            migrationBuilder.CreateIndex(
                name: "IX_StateOfficeMonthlyReports_State",
                table: "StateOfficeMonthlyReports",
                column: "State");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StateOfficeMonthlyReports");
        }
    }
}
