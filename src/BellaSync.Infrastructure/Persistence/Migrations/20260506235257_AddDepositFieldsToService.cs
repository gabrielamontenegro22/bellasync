using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellaSync.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddDepositFieldsToService : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "DepositPercentage",
                table: "services",
                type: "numeric(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresDeposit",
                table: "services",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DepositPercentage",
                table: "services");

            migrationBuilder.DropColumn(
                name: "RequiresDeposit",
                table: "services");
        }
    }
}
