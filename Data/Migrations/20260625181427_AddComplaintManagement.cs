using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CTSHIPDashboard.Migrations
{
    /// <inheritdoc />
    public partial class AddComplaintManagement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Complaints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReferenceNumber = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Subject = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(3000)", maxLength: 3000, nullable: false),
                    Category = table.Column<int>(type: "int", nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    State = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    HmoId = table.Column<int>(type: "int", nullable: true),
                    ProviderId = table.Column<int>(type: "int", nullable: true),
                    EnrolleeId = table.Column<int>(type: "int", nullable: true),
                    SubmittedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    SubmittedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SubmittedByRole = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AssignedToUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    AssignedToName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ResolutionNote = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Complaints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Complaints_Enrollees_EnrolleeId",
                        column: x => x.EnrolleeId,
                        principalTable: "Enrollees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Complaints_Hmos_HmoId",
                        column: x => x.HmoId,
                        principalTable: "Hmos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Complaints_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_EnrolleeId",
                table: "Complaints",
                column: "EnrolleeId");

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_HmoId",
                table: "Complaints",
                column: "HmoId");

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_Priority",
                table: "Complaints",
                column: "Priority");

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_ProviderId",
                table: "Complaints",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_ReferenceNumber",
                table: "Complaints",
                column: "ReferenceNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_State",
                table: "Complaints",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_Status",
                table: "Complaints",
                column: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Complaints");
        }
    }
}
