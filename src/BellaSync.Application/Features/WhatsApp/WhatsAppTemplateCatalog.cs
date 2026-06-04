using BellaSync.Domain.Entities;

namespace BellaSync.Application.Features.WhatsApp;

/// <summary>
/// Catálogo estático de todos los tipos de plantilla soportados, con:
///   - Título y descripción en español para la UI
///   - Body default (lo que se seedea al crear el tenant)
///   - IsEnabled default (algunos vienen ON, otros OFF)
///
/// Si en el futuro hay que agregar un Kind nuevo, basta con:
///   1. Agregar valor al enum WhatsAppTemplateKind
///   2. Agregar entrada acá con su metadata
///   3. Implementar el trigger en WhatsAppDispatcher
///
/// Centralizar acá evita que los strings se dupliquen en frontend/backend
/// y que un nuevo Kind se olvide en algún lado.
/// </summary>
public static class WhatsAppTemplateCatalog
{
    public sealed record CatalogEntry(
        WhatsAppTemplateKind Kind,
        string Title,
        string Description,
        string DefaultBody,
        bool DefaultEnabled);

    /// <summary>
    /// Catálogo completo. El orden es el que se muestra al usuario en la
    /// página de Configuración → WhatsApp (primero los más usados).
    /// </summary>
    public static readonly IReadOnlyList<CatalogEntry> All = new[]
    {
        new CatalogEntry(
            WhatsAppTemplateKind.ConfirmCreated,
            "Confirmación al crear cita",
            "Se envía apenas se agenda la cita",
            "Hola {nombre} 💛 Tu cita en {salon} quedó confirmada para el {fecha} a las {hora}. Te esperamos en {direccion}.",
            DefaultEnabled: true),

        new CatalogEntry(
            WhatsAppTemplateKind.Reminder24h,
            "Recordatorio 24h antes",
            "Un día antes de la cita",
            "Hola {nombre}, te recordamos tu cita mañana {fecha} a las {hora} para {servicio}. Responde CONFIRMO para apartar tu cupo.",
            DefaultEnabled: true),

        new CatalogEntry(
            WhatsAppTemplateKind.Ready2h,
            "Recordatorio 2h antes",
            "Dos horas antes — para que no se le pase",
            "Hola {nombre}, te esperamos en 2 horas para tu cita de {servicio}. {direccion}.",
            DefaultEnabled: true),

        new CatalogEntry(
            WhatsAppTemplateKind.PendingDeposit,
            "Anticipo pendiente",
            "Cuando falta el comprobante del anticipo",
            "Hola {nombre}, para apartar tu cita de {servicio} envíanos el comprobante del anticipo de {anticipo}. Tienes hasta {limite}.",
            DefaultEnabled: false),

        new CatalogEntry(
            WhatsAppTemplateKind.Birthday,
            "Cumpleaños",
            "El día del cumpleaños de la clienta",
            "¡Feliz cumpleaños {nombre}! 🎉 En {salon} queremos consentirte: ven este mes y recibe un 15% en tu servicio favorito.",
            DefaultEnabled: false),

        new CatalogEntry(
            WhatsAppTemplateKind.AppointmentCancelled,
            "Cita cancelada",
            "Al cancelar una cita ya confirmada",
            "Hola {nombre}, te confirmamos que tu cita de {servicio} del {fecha} a las {hora} fue cancelada. Si querés reagendar, respondé este mensaje. ¡Te esperamos pronto en {salon}!",
            DefaultEnabled: true),

        new CatalogEntry(
            WhatsAppTemplateKind.AppointmentRescheduled,
            "Cita reagendada",
            "Al reagendar a una nueva fecha/hora",
            "Hola {nombre}, tu cita de {servicio} en {salon} se reagendó para el {fecha} a las {hora}. Te esperamos. 💛",
            DefaultEnabled: true),
    };

    public static CatalogEntry Get(WhatsAppTemplateKind kind)
        => All.First(e => e.Kind == kind);
}
