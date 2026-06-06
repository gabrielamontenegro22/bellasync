using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellaSync.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddVoucherIsInternalCredit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_internal_credit",
                table: "payment_vouchers",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Backfill: marca como crédito interno los vouchers que fueron
            // creados antes de tener este flag pero que claramente son
            // aplicaciones de crédito (bank='Crédito interno' + reference
            // empezando con 'CR-', patrón que usaba CreateAppointmentHandler
            // .ApplyCustomerCreditsAsync).
            migrationBuilder.Sql(@"
                UPDATE payment_vouchers
                SET is_internal_credit = true
                WHERE bank = 'Crédito interno'
                  AND reference_number LIKE 'CR-%';
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "is_internal_credit",
                table: "payment_vouchers");
        }
    }
}
