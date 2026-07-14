using CTSHIPDashboard.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CTSHIPDashboard.Migrations
{
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20260714090000_AddCapitationPayments")]
    public partial class AddCapitationPayments : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[CapitationPayments]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [CapitationPayments] (
                        [Id] int NOT NULL IDENTITY,
                        [HmoId] int NOT NULL,
                        [ProviderId] int NOT NULL,
                        [ReportingMonth] date NOT NULL,
                        [EnrolleeCount] int NOT NULL,
                        [CapitationPerEnrollee] decimal(18,2) NOT NULL,
                        [UtilizationRate] decimal(18,2) NOT NULL,
                        [PaymentStatus] nvarchar(50) NOT NULL CONSTRAINT [DF_CapitationPayments_PaymentStatus] DEFAULT N'Pending',
                        [PaymentReference] nvarchar(100) NULL,
                        [ProofOfPaymentPath] nvarchar(500) NULL,
                        [CreatedAt] datetime2 NOT NULL,
                        [UpdatedAt] datetime2 NULL,
                        CONSTRAINT [PK_CapitationPayments] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_CapitationPayments_Hmos_HmoId] FOREIGN KEY ([HmoId]) REFERENCES [Hmos] ([Id]),
                        CONSTRAINT [FK_CapitationPayments_Providers_ProviderId] FOREIGN KEY ([ProviderId]) REFERENCES [Providers] ([Id])
                    );

                    CREATE INDEX [IX_CapitationPayments_HmoId] ON [CapitationPayments] ([HmoId]);
                    CREATE INDEX [IX_CapitationPayments_ProviderId] ON [CapitationPayments] ([ProviderId]);
                    CREATE UNIQUE INDEX [IX_CapitationPayments_HmoId_ProviderId_ReportingMonth]
                        ON [CapitationPayments] ([HmoId], [ProviderId], [ReportingMonth]);
                END
                """);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF OBJECT_ID(N'[CapitationPayments]', N'U') IS NOT NULL
                BEGIN
                    DROP TABLE [CapitationPayments];
                END
                """);
        }
    }
}
