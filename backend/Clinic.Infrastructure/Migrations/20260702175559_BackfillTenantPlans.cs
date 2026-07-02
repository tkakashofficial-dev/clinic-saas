using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Clinic.Infrastructure.Migrations
{
    /// <summary>
    /// Data repair: AddTenantPlans backfilled pre-existing tenants with
    /// Plan = '' (empty string), which EF's enum converter silently reads as
    /// Solo — silently downgrading every clinic that existed before the plans
    /// feature shipped. Those tenants get the intended default: Clinic plan
    /// with a fresh 14-day trial.
    /// </summary>
    public partial class BackfillTenantPlans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(
                """
                UPDATE clinic."Tenants"
                SET "Plan" = 'Clinic',
                    "TrialEndsAt" = NOW() + INTERVAL '14 days'
                WHERE "Plan" = '';
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Data repair — nothing sensible to undo.
        }
    }
}
