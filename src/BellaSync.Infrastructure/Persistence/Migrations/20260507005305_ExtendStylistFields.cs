using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellaSync.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ExtendStylistFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Agregar las columnas nuevas (manteniendo IsActive todavía)
            migrationBuilder.AddColumn<string>(
                name: "Email",
                table: "stylists",
                type: "character varying(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IdNumber",
                table: "stylists",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Role",
                table: "stylists",
                type: "character varying(80)",
                maxLength: 80,
                nullable: false,
                defaultValue: "Estilista");

            // Status arranca en 0 (Active) por default. Después migramos los inactivos.
            migrationBuilder.AddColumn<int>(
                name: "Status",
                table: "stylists",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // 2. Migrar datos: los que estaban con IsActive=false ahora son Status=2 (Inactive).
            //    Se ejecuta ANTES de borrar la columna IsActive para no perder la información.
            migrationBuilder.Sql(@"UPDATE stylists SET ""Status"" = 2 WHERE ""IsActive"" = false;");

            // 3. Reemplazar el índice único: antes filtraba por IsActive=true,
            //    ahora filtra por Status != 2 (no inactivo).
            migrationBuilder.DropIndex(
                name: "IX_stylists_TenantId_FullName",
                table: "stylists");

            migrationBuilder.CreateIndex(
                name: "IX_stylists_TenantId_FullName",
                table: "stylists",
                columns: new[] { "TenantId", "FullName" },
                unique: true,
                filter: "\"Status\" <> 2");

            // 4. Por último, eliminar la columna vieja IsActive.
            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "stylists");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // 1. Recrear IsActive (con default false; se van a actualizar en el paso 2).
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "stylists",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // 2. Migrar al revés: cualquier estado distinto de Inactive vuelve a IsActive=true.
            migrationBuilder.Sql(@"UPDATE stylists SET ""IsActive"" = true WHERE ""Status"" <> 2;");

            // 3. Restaurar el índice original.
            migrationBuilder.DropIndex(
                name: "IX_stylists_TenantId_FullName",
                table: "stylists");

            migrationBuilder.CreateIndex(
                name: "IX_stylists_TenantId_FullName",
                table: "stylists",
                columns: new[] { "TenantId", "FullName" },
                unique: true,
                filter: "\"IsActive\" = true");

            // 4. Drop columnas nuevas (orden inverso al Up).
            migrationBuilder.DropColumn(name: "Status",   table: "stylists");
            migrationBuilder.DropColumn(name: "Role",     table: "stylists");
            migrationBuilder.DropColumn(name: "IdNumber", table: "stylists");
            migrationBuilder.DropColumn(name: "Email",    table: "stylists");
        }
    }
}
