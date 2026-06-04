using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellaSync.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSalonHours : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_holidays_closed",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "lunch_break_enabled",
                table: "tenants",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "lunch_break_from_hour",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 13);

            migrationBuilder.AddColumn<int>(
                name: "lunch_break_to_hour",
                table: "tenants",
                type: "integer",
                nullable: false,
                defaultValue: 14);

            migrationBuilder.CreateTable(
                name: "salon_closed_dates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    closed_date = table.Column<DateOnly>(type: "date", nullable: false),
                    reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_salon_closed_dates", x => x.id);
                    table.ForeignKey(
                        name: "fk_salon_closed_dates_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "salon_weekly_hours",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    tenant_id = table.Column<Guid>(type: "uuid", nullable: false),
                    day_of_week = table.Column<int>(type: "integer", nullable: false),
                    from_hour = table.Column<int>(type: "integer", nullable: false),
                    to_hour = table.Column<int>(type: "integer", nullable: false),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_salon_weekly_hours", x => x.id);
                    table.ForeignKey(
                        name: "fk_salon_weekly_hours_tenants_tenant_id",
                        column: x => x.tenant_id,
                        principalTable: "tenants",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_salon_closed_dates_tenant_id_closed_date",
                table: "salon_closed_dates",
                columns: new[] { "tenant_id", "closed_date" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_salon_weekly_hours_tenant_id_day_of_week",
                table: "salon_weekly_hours",
                columns: new[] { "tenant_id", "day_of_week" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "salon_closed_dates");

            migrationBuilder.DropTable(
                name: "salon_weekly_hours");

            migrationBuilder.DropColumn(
                name: "is_holidays_closed",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "lunch_break_enabled",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "lunch_break_from_hour",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "lunch_break_to_hour",
                table: "tenants");
        }
    }
}
