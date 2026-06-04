namespace BellaSync.Application.Features.WhatsApp.Dtos;

/// <summary>
/// DTO de un mensaje encolado/enviado/fallado. La UI lo muestra en una
/// tabla cronológica para que la admin vea qué se mandó (o qué falló).
/// </summary>
public sealed class WhatsAppMessageDto
{
    public Guid Id { get; init; }

    public string Kind { get; init; } = string.Empty;

    public string CustomerPhone { get; init; } = string.Empty;

    public string RenderedBody { get; init; } = string.Empty;

    public Guid? AppointmentId { get; init; }

    public string Status { get; init; } = string.Empty;

    public DateTime QueuedAt { get; init; }

    public DateTime? SentAt { get; init; }

    public DateTime? FailedAt { get; init; }

    public string? FailureReason { get; init; }
}
