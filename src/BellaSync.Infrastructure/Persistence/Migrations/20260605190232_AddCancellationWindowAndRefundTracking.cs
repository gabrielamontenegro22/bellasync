using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellaSync.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCancellationWindowAndRefundTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "cancellation_window_hours",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 2);

            migrationBuilder.AddColumn<bool>(
                name: "reception_can_refund_deposit",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "refund_decision",
                table: "payment_vouchers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "refund_resolved_at",
                table: "payment_vouchers",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "refund_resolved_by_user_id",
                table: "payment_vouchers",
                type: "uuid",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "cancellation_window_hours",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "reception_can_refund_deposit",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "refund_decision",
                table: "payment_vouchers");

            migrationBuilder.DropColumn(
                name: "refund_resolved_at",
                table: "payment_vouchers");

            migrationBuilder.DropColumn(
                name: "refund_resolved_by_user_id",
                table: "payment_vouchers");
        }
    }
}
