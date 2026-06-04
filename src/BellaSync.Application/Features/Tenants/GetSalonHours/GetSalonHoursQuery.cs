using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Tenants.Dtos;

namespace BellaSync.Application.Features.Tenants.GetSalonHours;

public sealed record GetSalonHoursQuery() : IQuery<SalonHoursResponse>;
