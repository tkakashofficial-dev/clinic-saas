using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddIntakeFormResponses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "IntakeFormResponses",
                schema: "clinic",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TenantId = table.Column<Guid>(type: "uuid", nullable: false),
                    PatientId = table.Column<Guid>(type: "uuid", nullable: false),
                    Template = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    AnswersJson = table.Column<string>(type: "character varying(12000)", maxLength: 12000, nullable: false),
                    FilledByTenantUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ModifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ModifiedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    IsDeleted = table.Column<bool>(type: "boolean", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DeletedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IntakeFormResponses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IntakeFormResponses_Patients_PatientId",
                        column: x => x.PatientId,
                        principalSchema: "clinic",
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IntakeFormResponses_TenantUsers_FilledByTenantUserId",
                        column: x => x.FilledByTenantUserId,
                        principalSchema: "clinic",
                        principalTable: "TenantUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IntakeFormResponses_FilledByTenantUserId",
                schema: "clinic",
                table: "IntakeFormResponses",
                column: "FilledByTenantUserId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeFormResponses_PatientId",
                schema: "clinic",
                table: "IntakeFormResponses",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_IntakeFormResponses_TenantId_PatientId_CreatedAt",
                schema: "clinic",
                table: "IntakeFormResponses",
                columns: new[] { "TenantId", "PatientId", "CreatedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IntakeFormResponses",
                schema: "clinic");
        }
    }
}
