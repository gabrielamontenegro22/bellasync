using BellaSync.Application.Common.Handlers;

namespace BellaSync.Application.Features.Services.DeleteService;

/// <summary>
/// Borrado lógico (Archive). Idempotente: si ya estaba archivado,
/// igual devuelve Success.
/// </summary>
public sealed record DeleteServiceCommand(Guid Id) : ICommand;
