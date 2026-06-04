using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Cash.Dtos;

namespace BellaSync.Application.Features.Cash.GetCashClosingForDate;

/// <summary>
/// Devuelve el cierre de una fecha si existe, null si no se cerró aún.
/// Lo usa el frontend al cargar /caja para mostrar el pill correcto
/// ("Caja abierta" vs "Caja cerrada") y deshabilitar el botón Cerrar
/// si ya está hecho.
/// </summary>
public sealed record GetCashClosingForDateQuery(DateOnly Date)
    : IQuery<CashClosingResponse?>;
