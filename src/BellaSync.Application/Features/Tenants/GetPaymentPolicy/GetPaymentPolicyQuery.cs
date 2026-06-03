using BellaSync.Application.Common.Handlers;
using BellaSync.Application.Features.Tenants.Dtos;

namespace BellaSync.Application.Features.Tenants.GetPaymentPolicy;

/// <summary>Lee la política de pagos del tenant actual (del JWT).</summary>
public sealed record GetPaymentPolicyQuery : IQuery<TenantPaymentPolicyResponse>;
