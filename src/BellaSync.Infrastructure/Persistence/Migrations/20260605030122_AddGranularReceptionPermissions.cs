using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellaSync.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddGranularReceptionPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "reception_can_edit_payment_policy",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "reception_can_edit_salon_info",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "reception_can_edit_schedule",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "reception_can_edit_services",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "reception_can_edit_stylists",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "reception_can_view_commissions",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "reception_can_view_reports",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "reception_can_edit_payment_policy",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "reception_can_edit_salon_info",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "reception_can_edit_schedule",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "reception_can_edit_services",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "reception_can_edit_stylists",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "reception_can_view_commissions",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "reception_can_view_reports",
                table: "tenants");
        }
    }
}
