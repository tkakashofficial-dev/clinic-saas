using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientNumbers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PatientNumber",
                schema: "clinic",
                table: "Patients",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Backfill BEFORE the unique index: existing patients all default
            // to 0, which would violate uniqueness for any clinic with 2+
            // patients. Number them per clinic in registration order.
            migrationBuilder.Sql(
                """
                UPDATE clinic."Patients" p
                SET "PatientNumber" = numbered.rn
                FROM (
                    SELECT "Id",
                           ROW_NUMBER() OVER (PARTITION BY "TenantId" ORDER BY "CreatedAt") AS rn
                    FROM clinic."Patients"
                ) AS numbered
                WHERE p."Id" = numbered."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Patients_TenantId_PatientNumber",
                schema: "clinic",
                table: "Patients",
                columns: new[] { "TenantId", "PatientNumber" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Patients_TenantId_PatientNumber",
                schema: "clinic",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "PatientNumber",
                schema: "clinic",
                table: "Patients");
        }
    }
}
