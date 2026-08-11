using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CTSHIPDashboard.Migrations
{
    /// <inheritdoc />
    public partial class wallet : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('Providers', 'WalletSource') IS NULL
                BEGIN
                    ALTER TABLE [Providers] ADD [WalletSource] nvarchar(max) NULL;
                END
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF COL_LENGTH('Providers', 'WalletSource') IS NOT NULL
                BEGIN
                    ALTER TABLE [Providers] DROP COLUMN [WalletSource];
                END
                """);
        }
    }
}
