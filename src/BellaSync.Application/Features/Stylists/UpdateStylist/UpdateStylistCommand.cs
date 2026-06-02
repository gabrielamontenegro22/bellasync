using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Stylists.Dtos;
using BellaSync.Domain.Entities;

namespace BellaSync.Application.Features.Stylists.UpdateStylist;

public sealed record UpdateStylistCommand(
    Guid Id,
    string FullName,
    string Role,
    string? Email,
    string? Phone,
    string? IdNumber,
    string? Color,
    DateOnly? HireDate,
    StylistStatus Status,
    List<Guid> ServiceIds) : ICommand<StylistResponse>;
