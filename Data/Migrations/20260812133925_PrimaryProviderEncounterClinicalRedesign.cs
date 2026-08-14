using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CTSHIPDashboard.Migrations
{
    /// <inheritdoc />
    public partial class PrimaryProviderEncounterClinicalRedesign : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DiagnosisOther",
                table: "Encounters",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ImmunizationsData",
                table: "Encounters",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LaboratoryOther",
                table: "Encounters",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MedicinesOther",
                table: "Encounters",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PatientOutcome",
                table: "Encounters",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Treated");

            migrationBuilder.AddColumn<string>(
                name: "PreventiveServicesData",
                table: "Encounters",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PreventiveServicesOther",
                table: "Encounters",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScreeningTestsData",
                table: "Encounters",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SelectedMedicinesData",
                table: "Encounters",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ServicesProvidedOther",
                table: "Encounters",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiagnosisOther",
                table: "Encounters");

            migrationBuilder.DropColumn(
                name: "ImmunizationsData",
                table: "Encounters");

            migrationBuilder.DropColumn(
                name: "LaboratoryOther",
                table: "Encounters");

            migrationBuilder.DropColumn(
                name: "MedicinesOther",
                table: "Encounters");

            migrationBuilder.DropColumn(
                name: "PatientOutcome",
                table: "Encounters");

            migrationBuilder.DropColumn(
                name: "PreventiveServicesData",
                table: "Encounters");

            migrationBuilder.DropColumn(
                name: "PreventiveServicesOther",
                table: "Encounters");

            migrationBuilder.DropColumn(
                name: "ScreeningTestsData",
                table: "Encounters");

            migrationBuilder.DropColumn(
                name: "SelectedMedicinesData",
                table: "Encounters");

            migrationBuilder.DropColumn(
                name: "ServicesProvidedOther",
                table: "Encounters");
        }
    }
}
