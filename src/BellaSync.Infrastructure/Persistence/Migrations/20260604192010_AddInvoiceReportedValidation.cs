using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellaSync.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddInvoiceReportedValidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "rejected_at",
                table: "subscription_invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "reported_at",
                table: "subscription_invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reported_method",
                table: "subscription_invoices",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "reported_reference",
                table: "subscription_invoices",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "validated_at",
                table: "subscription_invoices",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "validated_by_user_id",
                table: "subscription_invoices",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_subscription_invoices_status",
                table: "subscription_invoices",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_subscription_invoices_status",
                table: "subscription_invoices");

            migrationBuilder.DropColumn(
                name: "rejected_at",
                table: "subscription_invoices");

            migrationBuilder.DropColumn(
                name: "reported_at",
                table: "subscription_invoices");

            migrationBuilder.DropColumn(
                name: "reported_method",
                table: "subscription_invoices");

            migrationBuilder.DropColumn(
                name: "reported_reference",
                table: "subscription_invoices");

            migrationBuilder.DropColumn(
                name: "validated_at",
                table: "subscription_invoices");

            migrationBuilder.DropColumn(
                name: "validated_by_user_id",
                table: "subscription_invoices");
        }
    }
}
