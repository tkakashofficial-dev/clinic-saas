using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPatientBloodGroupAndClinicPublicProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "PublicBookingEnabled",
                schema: "clinic",
                table: "Tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                schema: "clinic",
                table: "Tenants",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "UpiId",
                schema: "clinic",
                table: "Tenants",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BloodGroup",
                schema: "clinic",
                table: "Patients",
                type: "character varying(3)",
                maxLength: 3,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Slug",
                schema: "clinic",
                table: "Tenants",
                column: "Slug",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Tenants_Slug",
                schema: "clinic",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "PublicBookingEnabled",
                schema: "clinic",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "Slug",
                schema: "clinic",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "UpiId",
                schema: "clinic",
                table: "Tenants");

            migrationBuilder.DropColumn(
                name: "BloodGroup",
                schema: "clinic",
                table: "Patients");
        }
    }
}
