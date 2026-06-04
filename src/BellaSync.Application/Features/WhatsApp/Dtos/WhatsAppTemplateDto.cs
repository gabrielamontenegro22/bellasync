namespace BellaSync.Application.Features.WhatsApp.Dtos;

/// <summary>
/// DTO de salida para una plantilla. El frontend muestra el body crudo
/// (con placeholders) en el editor y los reemplaza visualmente con valores
/// de ejemplo para la "vista previa".
/// </summary>
public sealed class WhatsAppTemplateDto
{
    /// <summary>"ConfirmCreated", "Reminder24h", etc. — string del enum.</summary>
    public string Kind { get; init; } = string.Empty;

    /// <summary>Label legible en español para mostrar en la UI.</summary>
    public string Title { get; init; } = string.Empty;

    /// <summary>Subtítulo explicativo de cuándo se dispara.</summary>
    public string Description { get; init; } = string.Empty;

    public string Body { get; init; } = string.Empty;

    public bool IsEnabled { get; init; }
}
