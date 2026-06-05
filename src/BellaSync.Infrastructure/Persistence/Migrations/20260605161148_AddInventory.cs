using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellaSync.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInventory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "reception_can_edit_inventory",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "products",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    brand = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    category = table.Column<int>(type: "integer", nullable: false),
                    unit = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    stock = table.Column<int>(type: "integer", nullable: false),
                    min_stock = table.Column<int>(type: "integer", nullable: false),
                    cost = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    tone = table.Column<int>(type: "integer", nullable: false),
                    last_in_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_products", x => x.id);
                    table.ForeignKey(
                        name: "fk_products_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "product_movements",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    product_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<int>(type: "integer", nullable: false),
                    qty = table.Column<int>(type: "integer", nullable: false),
                    reason = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    stock_before = table.Column<int>(type: "integer", nullable: false),
                    stock_after = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    registered_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    registered_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_product_movements", x => x.id);
                    table.ForeignKey(
                        name: "fk_product_movements_products_product_id",
                        column: x => x.product_id,
                        principalTable: "products",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_product_movements_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_product_movements_users_registered_by_user_id",
                        column: x => x.registered_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_product_movements_product_id",
                table: "product_movements",
                column: "product_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_movements_registered_by_user_id",
                table: "product_movements",
                column: "registered_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_product_movements_tenant_id_product_id_registered_at",
                table: "product_movements",
                columns: new[] { "tenant_id", "product_id", "registered_at" });

            migrationBuilder.CreateIndex(
                name: "ix_products_tenant_id_category",
                table: "products",
                columns: new[] { "tenant_id", "category" });

            migrationBuilder.CreateIndex(
                name: "ix_products_tenant_id_is_active",
                table: "products",
                columns: new[] { "tenant_id", "is_active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_movements");

            migrationBuilder.DropTable(
                name: "products");

            migrationBuilder.DropColumn(
                name: "reception_can_edit_inventory",
                table: "tenants");
        }
    }
}
