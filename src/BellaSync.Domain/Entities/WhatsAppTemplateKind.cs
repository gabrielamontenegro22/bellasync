namespace BellaSync.Domain.Entities;

/// <summary>
/// Tipos de plantilla de mensaje WhatsApp. Cada Tenant tiene UNA
/// instancia de cada tipo (no múltiples plantillas del mismo tipo).
/// Eso simplifica el modelo: la admin edita la plantilla existente,
/// no crea nuevas. El Kind también define CUÁNDO se dispara:
///
///   - ConfirmCreated: al crear la cita (inmediato)
///   - Reminder24h: 24 horas antes de la cita (job recurrente)
///   - Ready2h: 2 horas antes (job recurrente)
///   - PendingDeposit: al detectar cita Pending sin voucher tras X tiempo
///   - Birthday: el día del cumpleaños del cliente (job diario)
///
/// Si en el futuro hay que agregar tipos (campañas one-shot, etc.),
/// es agregar valor al enum + lógica de gatillo en el dispatcher.
/// </summary>
public enum WhatsAppTemplateKind
{
    ConfirmCreated = 0,
    Reminder24h = 1,
    Ready2h = 2,
    PendingDeposit = 3,
    Birthday = 4,
}
