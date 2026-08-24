using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CTSHIPDashboard.Data.Migrations
{
    /// <inheritdoc />
    public partial class PaymentClaimsComplaintsExpansion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Claims_EnrolleeId",
                table: "Claims");

            migrationBuilder.AddColumn<string>(
                name: "ActionTaken",
                table: "Complaints",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AgreedResolutionDueAt",
                table: "Complaints",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommunicationChannel",
                table: "Complaints",
                type: "nvarchar(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "In person");

            migrationBuilder.AddColumn<string>(
                name: "ComplainantCategory",
                table: "Complaints",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Enrollee");

            migrationBuilder.AddColumn<string>(
                name: "ComplainantFeedback",
                table: "Complaints",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateReceived",
                table: "Complaints",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "EscalationDetails",
                table: "Complaints",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Lga",
                table: "Complaints",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponsibleOrganization",
                table: "Complaints",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AmountApproved",
                table: "Claims",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "AmountPaid",
                table: "Claims",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ApprovedTariff",
                table: "Claims",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "AuthorizationNumber",
                table: "Claims",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateOfService",
                table: "Claims",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "DeductionAmount",
                table: "Claims",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "DeductionReason",
                table: "Claims",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "EncounterId",
                table: "Claims",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReferralFacility",
                table: "Claims",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServiceCategory",
                table: "Claims",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServiceProcedure",
                table: "Claims",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ActualPaymentMade",
                table: "CapitationPayments",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "DueDate",
                table: "CapitationPayments",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentPeriod",
                table: "CapitationPayments",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Monthly");

            migrationBuilder.AddColumn<DateTime>(
                name: "ProviderPaymentReceivedDate",
                table: "CapitationPayments",
                type: "date",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_Category",
                table: "Complaints",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_DateReceived",
                table: "Complaints",
                column: "DateReceived");

            migrationBuilder.CreateIndex(
                name: "IX_Complaints_ResolvedAt",
                table: "Complaints",
                column: "ResolvedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_AuthorizationNumber",
                table: "Claims",
                column: "AuthorizationNumber");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_EncounterId",
                table: "Claims",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_EnrolleeId_DateOfService_ServiceProcedure",
                table: "Claims",
                columns: new[] { "EnrolleeId", "DateOfService", "ServiceProcedure" });

            migrationBuilder.CreateIndex(
                name: "IX_CapitationPayments_PaymentStatus",
                table: "CapitationPayments",
                column: "PaymentStatus");

            migrationBuilder.CreateIndex(
                name: "IX_CapitationPayments_ProviderPaymentReceivedDate",
                table: "CapitationPayments",
                column: "ProviderPaymentReceivedDate");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Complaints_Category",
                table: "Complaints");

            migrationBuilder.DropIndex(
                name: "IX_Complaints_DateReceived",
                table: "Complaints");

            migrationBuilder.DropIndex(
                name: "IX_Complaints_ResolvedAt",
                table: "Complaints");

            migrationBuilder.DropIndex(
                name: "IX_Claims_AuthorizationNumber",
                table: "Claims");

            migrationBuilder.DropIndex(
                name: "IX_Claims_EncounterId",
                table: "Claims");

            migrationBuilder.DropIndex(
                name: "IX_Claims_EnrolleeId_DateOfService_ServiceProcedure",
                table: "Claims");

            migrationBuilder.DropIndex(
                name: "IX_CapitationPayments_PaymentStatus",
                table: "CapitationPayments");

            migrationBuilder.DropIndex(
                name: "IX_CapitationPayments_ProviderPaymentReceivedDate",
                table: "CapitationPayments");

            migrationBuilder.DropColumn(
                name: "ActionTaken",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "AgreedResolutionDueAt",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "CommunicationChannel",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "ComplainantCategory",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "ComplainantFeedback",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "DateReceived",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "EscalationDetails",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "Lga",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "ResponsibleOrganization",
                table: "Complaints");

            migrationBuilder.DropColumn(
                name: "AmountApproved",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "AmountPaid",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "ApprovedTariff",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "AuthorizationNumber",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "DateOfService",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "DeductionAmount",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "DeductionReason",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "EncounterId",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "ReferralFacility",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "ServiceCategory",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "ServiceProcedure",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "ActualPaymentMade",
                table: "CapitationPayments");

            migrationBuilder.DropColumn(
                name: "DueDate",
                table: "CapitationPayments");

            migrationBuilder.DropColumn(
                name: "PaymentPeriod",
                table: "CapitationPayments");

            migrationBuilder.DropColumn(
                name: "ProviderPaymentReceivedDate",
                table: "CapitationPayments");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_EnrolleeId",
                table: "Claims",
                column: "EnrolleeId");
        }
    }
}
