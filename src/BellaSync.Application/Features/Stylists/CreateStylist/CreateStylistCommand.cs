using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Stylists.Dtos;

namespace BellaSync.Application.Features.Stylists.CreateStylist;

public sealed record CreateStylistCommand(
    string FullName,
    string Role,
    string? Email,
    string? Phone,
    string? IdNumber,
    string? Color,
    DateOnly? HireDate,
    List<Guid> ServiceIds) : ICommand<StylistResponse>;
