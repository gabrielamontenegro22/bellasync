using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellaSync.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddReceptionPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "reception_can_cancel_with_money",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "reception_can_close_cash",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<decimal>(
                name: "reception_expense_cap_cop",
                table: "tenants",
                type: "numeric(12,2)",
                nullable: true,
                defaultValue: 100000m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "reception_can_cancel_with_money",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "reception_can_close_cash",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "reception_expense_cap_cop",
                table: "tenants");
        }
    }
}
