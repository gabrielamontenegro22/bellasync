using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellaSync.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditCancelledByAndUserNavs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "cancelled_by_user_id",
                table: "appointments",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_payment_vouchers_decided_by",
                table: "payment_vouchers",
                column: "decided_by");

            migrationBuilder.CreateIndex(
                name: "ix_appointments_cancelled_by_user_id",
                table: "appointments",
                column: "cancelled_by_user_id");

            migrationBuilder.AddForeignKey(
                name: "fk_appointments_users_cancelled_by_user_id",
                table: "appointments",
                column: "cancelled_by_user_id",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "fk_payment_vouchers_users_decided_by",
                table: "payment_vouchers",
                column: "decided_by",
                principalTable: "users",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_appointments_users_cancelled_by_user_id",
                table: "appointments");

            migrationBuilder.DropForeignKey(
                name: "fk_payment_vouchers_users_decided_by",
                table: "payment_vouchers");

            migrationBuilder.DropIndex(
                name: "ix_payment_vouchers_decided_by",
                table: "payment_vouchers");

            migrationBuilder.DropIndex(
                name: "ix_appointments_cancelled_by_user_id",
                table: "appointments");

            migrationBuilder.DropColumn(
                name: "cancelled_by_user_id",
                table: "appointments");
        }
    }
}
