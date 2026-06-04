namespace BellaSync.Application.Features.Tenants.Dtos;

/// <summary>
/// Estado del módulo de comisiones del salón. Aislado en su propio DTO
/// (en lugar de meterlo en TenantPaymentPolicyResponse) porque son
/// features distintos: la política de pagos es sobre tiempos del cupo,
/// las comisiones son sobre cómo se le paga a los estilistas. Pueden
/// crecer cada uno por su lado.
/// </summary>
public class CommissionsSettingResponse
{
    /// <summary>
    /// true = el módulo de Comisiones está activo (pantalla visible
    /// en sidebar, campos de % en formulario de servicios, etc.).
    /// false = invisible. Default para salones nuevos.
    /// </summary>
    public bool Enabled { get; set; }
}
