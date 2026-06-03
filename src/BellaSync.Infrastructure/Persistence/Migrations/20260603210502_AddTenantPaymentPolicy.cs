using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellaSync.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantPaymentPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "hold_duration_hours",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 3);

            migrationBuilder.AddColumn<int>(
                name: "hold_min_before_appointment_minutes",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 30);

            migrationBuilder.AddColumn<int>(
                name: "min_advance_minutes",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 30);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "hold_duration_hours",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "hold_min_before_appointment_minutes",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "min_advance_minutes",
                table: "tenants");
        }
    }
}
