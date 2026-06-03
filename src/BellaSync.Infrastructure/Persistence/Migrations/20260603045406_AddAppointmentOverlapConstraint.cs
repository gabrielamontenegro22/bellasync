using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BellaSync.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Agrega una restricción a nivel BD que impide que dos citas activas del
    /// mismo estilista se solapen en tiempo. Antes solo lo prevenía el
    /// AppointmentValidator en la capa Application — útil para el flujo normal
    /// del API, pero un INSERT SQL crudo (seed/import) podía crear overlaps.
    ///
    /// Cómo funciona:
    ///  - Usa el operador EXCLUDE de PostgreSQL con índice GiST.
    ///  - tstzrange(start_at, end_at, '[)') representa el intervalo cerrado-abierto
    ///    de la cita; '&&' es el operador "overlaps" de rangos.
    ///  - La condición WHERE (status NOT IN (4,5)) excluye citas Cancelled
    ///    (4) y NoShow (5) — esas no bloquean el slot.
    ///
    /// Defensa en profundidad: el handler sigue validando antes para devolver
    /// un 409 con mensaje en español. Si por error llega un overlap hasta acá,
    /// PostgreSQL lo rechaza con un constraint violation (23P01) que se mapea
    /// a 500 — preferimos que rompa loud y se note, en lugar de corromper datos.
    /// </summary>
    public partial class AddAppointmentOverlapConstraint : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // btree_gist permite mezclar tipos btree (uuid) con tipos gist
            // (tstzrange) en el mismo EXCLUDE. Sin esta extensión el constraint
            // no se puede crear.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");

            migrationBuilder.Sql(@"
ALTER TABLE appointments
ADD CONSTRAINT ck_appointments_no_overlap
EXCLUDE USING gist (
    stylist_id WITH =,
    tstzrange(start_at, end_at, '[)') WITH &&
) WHERE (status NOT IN (4, 5));
");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE appointments DROP CONSTRAINT IF EXISTS ck_appointments_no_overlap;");
            // No removemos btree_gist en Down — otras partes del schema podrían
            // empezar a depender de ella en el futuro.
        }
    }
}
