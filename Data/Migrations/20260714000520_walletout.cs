using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CTSHIPDashboard.Migrations
{
    /// <inheritdoc />
    public partial class walletout : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProviderWalletTransactions");

            migrationBuilder.DropTable(
                name: "WalletTransactions");

            migrationBuilder.DropTable(
                name: "ProviderWallets");

            migrationBuilder.DropTable(
                name: "EnrolleeWallets");

            migrationBuilder.DropColumn(
                name: "WalletSource",
                table: "Providers");

            migrationBuilder.DropColumn(
                name: "WalletSource",
                table: "Encounters");

            migrationBuilder.CreateTable(
                name: "CapitationPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    HmoId = table.Column<int>(type: "int", nullable: false),
                    ProviderId = table.Column<int>(type: "int", nullable: false),
                    ReportingMonth = table.Column<DateTime>(type: "date", nullable: false),
                    EnrolleeCount = table.Column<int>(type: "int", nullable: false),
                    CapitationPerEnrollee = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    UtilizationRate = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PaymentStatus = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false, defaultValue: "Pending"),
                    PaymentReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ProofOfPaymentPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CapitationPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CapitationPayments_Hmos_HmoId",
                        column: x => x.HmoId,
                        principalTable: "Hmos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CapitationPayments_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CapitationPayments_HmoId",
                table: "CapitationPayments",
                column: "HmoId");

            migrationBuilder.CreateIndex(
                name: "IX_CapitationPayments_HmoId_ProviderId_ReportingMonth",
                table: "CapitationPayments",
                columns: new[] { "HmoId", "ProviderId", "ReportingMonth" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CapitationPayments_ProviderId",
                table: "CapitationPayments",
                column: "ProviderId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CapitationPayments");

            migrationBuilder.AddColumn<string>(
                name: "WalletSource",
                table: "Providers",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WalletSource",
                table: "Encounters",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "EnrolleeWallets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnrolleeId = table.Column<int>(type: "int", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LastDisbursedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    MonthlyAllocation = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EnrolleeWallets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EnrolleeWallets_Enrollees_EnrolleeId",
                        column: x => x.EnrolleeId,
                        principalTable: "Enrollees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProviderWallets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProviderId = table.Column<int>(type: "int", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LastDisbursedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalDisbursed = table.Column<decimal>(type: "decimal(18,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderWallets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderWallets_Providers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "Providers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WalletTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    EnrolleeWalletId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WalletTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WalletTransactions_EnrolleeWallets_EnrolleeWalletId",
                        column: x => x.EnrolleeWalletId,
                        principalTable: "EnrolleeWallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProviderWalletTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProviderWalletId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProviderWalletTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProviderWalletTransactions_ProviderWallets_ProviderWalletId",
                        column: x => x.ProviderWalletId,
                        principalTable: "ProviderWallets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EnrolleeWallets_EnrolleeId",
                table: "EnrolleeWallets",
                column: "EnrolleeId");

            migrationBuilder.CreateIndex(
                name: "IX_ProviderWallets_ProviderId",
                table: "ProviderWallets",
                column: "ProviderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProviderWalletTransactions_ProviderWalletId",
                table: "ProviderWalletTransactions",
                column: "ProviderWalletId");

            migrationBuilder.CreateIndex(
                name: "IX_WalletTransactions_EnrolleeWalletId",
                table: "WalletTransactions",
                column: "EnrolleeWalletId");
        }
    }
}
