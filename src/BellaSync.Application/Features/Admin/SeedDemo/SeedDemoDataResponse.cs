namespace BellaSync.Application.Features.Admin.SeedDemo;

/// <summary>
/// Resumen de lo que el seed creó vs. saltó (porque ya existía). Útil para
/// reportar al frontend qué cambió.
/// </summary>
public class SeedDemoDataResponse
{
    public int StylistsCreated { get; set; }
    public int StylistsSkipped { get; set; }
    public int ServicesCreated { get; set; }
    public int ServicesSkipped { get; set; }
    public int CustomersCreated { get; set; }
    public int CustomersSkipped { get; set; }
    public int AppointmentsCreated { get; set; }
    public int AppointmentsSkipped { get; set; }
    /// <summary>Fecha YYYY-MM-DD para la que se programaron las citas.</summary>
    public string TargetDate { get; set; } = string.Empty;
}
