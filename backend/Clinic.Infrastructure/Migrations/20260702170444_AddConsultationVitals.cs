using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddConsultationVitals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BloodPressure",
                schema: "clinic",
                table: "Consultations",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PulseBpm",
                schema: "clinic",
                table: "Consultations",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TemperatureCelsius",
                schema: "clinic",
                table: "Consultations",
                type: "numeric(4,1)",
                precision: 4,
                scale: 1,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "WeightKg",
                schema: "clinic",
                table: "Consultations",
                type: "numeric(5,1)",
                precision: 5,
                scale: 1,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BloodPressure",
                schema: "clinic",
                table: "Consultations");

            migrationBuilder.DropColumn(
                name: "PulseBpm",
                schema: "clinic",
                table: "Consultations");

            migrationBuilder.DropColumn(
                name: "TemperatureCelsius",
                schema: "clinic",
                table: "Consultations");

            migrationBuilder.DropColumn(
                name: "WeightKg",
                schema: "clinic",
                table: "Consultations");
        }
    }
}
