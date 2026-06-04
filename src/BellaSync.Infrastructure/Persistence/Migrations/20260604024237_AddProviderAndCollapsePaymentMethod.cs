using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellaSync.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddProviderAndCollapsePaymentMethod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1) Agregar columna provider (nullable).
            migrationBuilder.AddColumn<string>(
                name: "provider",
                table: "payments",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "provider",
                table: "expenses",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            // 2) Backfill: el enum colapsó de 7 a 4 valores.
            //    Antes:  Cash=0, Bancolombia=1, Nequi=2, Daviplata=3,
            //            CreditCard=4, DebitCard=5, Other=99.
            //    Ahora:  Cash=0, Transfer=1, Card=2, Other=99.
            //
            //    Los rows existentes con method ∈ {1,2,3} pasan a Transfer (=1)
            //    y conservan el banco en provider. Los con method ∈ {4,5}
            //    pasan a Card (=2). Cash y Other quedan iguales.
            //
            //    Importante: hacemos provider primero, después method,
            //    porque el CASE necesita el valor original de method.
            migrationBuilder.Sql(@"
                UPDATE payments
                SET provider = CASE method
                    WHEN 1 THEN 'Bancolombia'
                    WHEN 2 THEN 'Nequi'
                    WHEN 3 THEN 'Daviplata'
                    ELSE provider
                END
                WHERE method IN (1, 2, 3);

                UPDATE payments SET method = 1 WHERE method IN (2, 3);  -- Nequi/Daviplata → Transfer
                UPDATE payments SET method = 2 WHERE method IN (4, 5);  -- CreditCard/DebitCard → Card

                UPDATE expenses
                SET provider = CASE method
                    WHEN 1 THEN 'Bancolombia'
                    WHEN 2 THEN 'Nequi'
                    WHEN 3 THEN 'Daviplata'
                    ELSE provider
                END
                WHERE method IN (1, 2, 3);

                UPDATE expenses SET method = 1 WHERE method IN (2, 3);
                UPDATE expenses SET method = 2 WHERE method IN (4, 5);
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Best-effort: restaurar method desde provider para los rows
            // que sabemos mapear. CreditCard/DebitCard se pierden
            // (no podemos distinguir uno del otro) y caen en CreditCard=4.
            migrationBuilder.Sql(@"
                UPDATE payments SET method = CASE provider
                    WHEN 'Bancolombia' THEN 1
                    WHEN 'Nequi'       THEN 2
                    WHEN 'Daviplata'   THEN 3
                    ELSE method
                END
                WHERE method = 1 AND provider IS NOT NULL;

                UPDATE payments SET method = 4 WHERE method = 2;  -- Card → CreditCard (default)

                UPDATE expenses SET method = CASE provider
                    WHEN 'Bancolombia' THEN 1
                    WHEN 'Nequi'       THEN 2
                    WHEN 'Daviplata'   THEN 3
                    ELSE method
                END
                WHERE method = 1 AND provider IS NOT NULL;

                UPDATE expenses SET method = 4 WHERE method = 2;
            ");

            migrationBuilder.DropColumn(
                name: "provider",
                table: "payments");

            migrationBuilder.DropColumn(
                name: "provider",
                table: "expenses");
        }
    }
}
