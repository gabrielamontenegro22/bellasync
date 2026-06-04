using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellaSync.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "commissions_enabled",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "commission_payouts",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    stylist_id = table.Column<Guid>(type: "uuid", nullable: false),
                    amount = table.Column<decimal>(type: "numeric(12,2)", nullable: false),
                    period_from = table.Column<DateOnly>(type: "date", nullable: false),
                    period_to = table.Column<DateOnly>(type: "date", nullable: false),
                    paid_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    paid_by_user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_commission_payouts", x => x.id);
                    table.ForeignKey(
                        name: "fk_commission_payouts_stylists_stylist_id",
                        column: x => x.stylist_id,
                        principalTable: "stylists",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_commission_payouts_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_commission_payouts_users_paid_by_user_id",
                        column: x => x.paid_by_user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_commission_payouts_paid_by_user_id",
                table: "commission_payouts",
                column: "paid_by_user_id");

            migrationBuilder.CreateIndex(
                name: "ix_commission_payouts_stylist_id",
                table: "commission_payouts",
                column: "stylist_id");

            migrationBuilder.CreateIndex(
                name: "ix_commission_payouts_tenant_id_stylist_id_period_to",
                table: "commission_payouts",
                columns: new[] { "tenant_id", "stylist_id", "period_to" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "commission_payouts");

            migrationBuilder.DropColumn(
                name: "commissions_enabled",
                table: "tenants");
        }
    }
}
