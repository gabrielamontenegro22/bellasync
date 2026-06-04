using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellaSync.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCashClosings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "cash_closings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    closed_date = table.Column<DateOnly>(type: "date", nullable: false),
                    base_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    cash_sales = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    cash_expenses = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    expected_cash = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    counted_cash = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    diff = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    diff_note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    total_amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    closed_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    closed_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_cash_closings", x => x.id);
                    table.ForeignKey(
                        name: "fk_cash_closings_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_cash_closings_users_closed_by_user_id",
                        column: x => x.closed_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_cash_closings_closed_by_user_id",
                table: "cash_closings",
                column: "closed_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_cash_closings_tenant_id_closed_date",
                table: "cash_closings",
                columns: new[] { "tenant_id", "closed_date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "cash_closings");
        }
    }
}
