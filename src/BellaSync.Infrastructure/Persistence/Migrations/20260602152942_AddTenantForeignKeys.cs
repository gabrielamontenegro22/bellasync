using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellaSync.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddTenantForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddForeignKey(
                name: "FK_customers_tenants_TenantId",
                table: "customers",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_services_tenants_TenantId",
                table: "services",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stylist_services_tenants_TenantId",
                table: "stylist_services",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_stylists_tenants_TenantId",
                table: "stylists",
                column: "TenantId",
                principalTable: "tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_customers_tenants_TenantId",
                table: "customers");

            migrationBuilder.DropForeignKey(
                name: "FK_services_tenants_TenantId",
                table: "services");

            migrationBuilder.DropForeignKey(
                name: "FK_stylist_services_tenants_TenantId",
                table: "stylist_services");

            migrationBuilder.DropForeignKey(
                name: "FK_stylists_tenants_TenantId",
                table: "stylists");
        }
    }
}
