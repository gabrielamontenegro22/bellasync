using BellaSync.Application.Common.Handlers;

namespace BellaSync.Application.Features.Admin.SeedDemo;

/// <summary>
/// Carga datos demo para el tenant actual: estilistas, servicios, clientes y
/// citas distribuidas en el día indicado. Es 100% idempotente — buscar por
/// nombre / teléfono evita duplicar si ya hay datos parecidos.
///
/// Pensado para que un salón recién creado pueda ver la agenda llena con un
/// click y sentir cómo se ve la aplicación con uso real, antes de capturar
/// sus datos verdaderos. No es destructivo: nunca borra nada.
/// </summary>
/// <param name="TargetDate">
/// Día para el que se generan las citas (zona horaria local del salón).
/// Si es null, usa "mañana" — así garantiza que todas las citas sean futuras
/// y respeten la regla de mínimo 30 min de anticipación.
/// </param>
public sealed record SeedDemoDataCommand(DateOnly? TargetDate)
    : ICommand<SeedDemoDataResponse>;
