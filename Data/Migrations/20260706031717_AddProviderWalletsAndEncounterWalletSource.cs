using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CTSHIPDashboard.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderWalletsAndEncounterWalletSource : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "WalletSource",
                table: "Encounters",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "EnrolleeWallet");

            migrationBuilder.CreateTable(
                name: "ProviderWallets",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProviderId = table.Column<int>(type: "int", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    TotalDisbursed = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LastDisbursedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
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
                name: "ProviderWalletTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProviderWalletId = table.Column<int>(type: "int", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Reference = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false)
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
                name: "IX_ProviderWallets_ProviderId",
                table: "ProviderWallets",
                column: "ProviderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProviderWalletTransactions_ProviderWalletId",
                table: "ProviderWalletTransactions",
                column: "ProviderWalletId");

            migrationBuilder.Sql("""
                INSERT INTO ProviderWallets (ProviderId, Balance, TotalDisbursed, LastDisbursedAt)
                SELECT
                    e.ProviderId,
                    COALESCE(SUM(w.Balance), 0),
                    COALESCE(SUM(w.Balance), 0),
                    MAX(w.LastDisbursedAt)
                FROM EnrolleeWallets w
                INNER JOIN Enrollees e ON e.Id = w.EnrolleeId
                WHERE e.ProviderId IS NOT NULL
                GROUP BY e.ProviderId;
                """);

            migrationBuilder.Sql("""
                INSERT INTO ProviderWalletTransactions (ProviderWalletId, Amount, Type, Reference, Timestamp)
                SELECT
                    pw.Id,
                    pw.Balance,
                    'OpeningBalance',
                    'Initial provider wallet balance from enrollee wallets',
                    COALESCE(pw.LastDisbursedAt, GETUTCDATE())
                FROM ProviderWallets pw
                WHERE pw.Balance > 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ProviderWalletTransactions");

            migrationBuilder.DropTable(
                name: "ProviderWallets");

            migrationBuilder.DropColumn(
                name: "WalletSource",
                table: "Encounters");
        }
    }
}
