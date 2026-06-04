namespace BellaSync.Application.Features.Subscription;

/// <summary>
/// Catálogo estático de planes de BellaSync. Static en C# (no entity)
/// porque los planes los define el equipo de BellaSync (la empresa SaaS),
/// no la admin del salón. Si cambian, se cambian acá y se redeploya.
///
/// Cuando un cliente se suscribe, su TenantSubscription guarda el
/// PlanCode (string). Los precios cambian acá → solo aplican al
/// próximo cobro. Lo ya facturado queda con su monto histórico en
/// SubscriptionInvoice.
/// </summary>
public static class SubscriptionPlanCatalog
{
    public sealed record Plan(
        string Code,
        string Name,
        decimal MonthlyPrice,
        string Tagline,
        IReadOnlyList<string> Features,
        bool IsHighlighted);

    public static readonly IReadOnlyList<Plan> All = new[]
    {
        new Plan(
            Code: "basic",
            Name: "Básico",
            MonthlyPrice: 50_000m,
            Tagline: "Para salones de 1–3 estilistas que arrancan",
            Features: new[]
            {
                "Agenda y CRM básicos",
                "Hasta 3 estilistas",
                "Hasta 150 citas / mes",
                "Soporte por email",
            },
            IsHighlighted: false),

        new Plan(
            Code: "professional",
            Name: "Profesional",
            MonthlyPrice: 90_000m,
            Tagline: "Para salones consolidados, lo más pedido",
            Features: new[]
            {
                "Todo lo del Básico",
                "Hasta 8 estilistas",
                "Hasta 500 citas / mes",
                "Reportes avanzados",
                "Notificaciones WhatsApp",
                "Comisiones automáticas",
            },
            IsHighlighted: true),

        new Plan(
            Code: "premium",
            Name: "Premium",
            MonthlyPrice: 150_000m,
            Tagline: "Para spas con múltiples sedes",
            Features: new[]
            {
                "Todo lo del Profesional",
                "Estilistas ilimitados",
                "Citas ilimitadas",
                "Inventario integrado",
                "Acceso de estilistas",
                "Soporte prioritario WhatsApp",
            },
            IsHighlighted: false),
    };

    public static Plan? Get(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return null;
        return All.FirstOrDefault(p => p.Code == code.Trim().ToLowerInvariant());
    }

    /// <summary>Default usado al crear un tenant nuevo (trial).</summary>
    public const string DefaultPlanCode = "professional";

    /// <summary>Días de trial al onboarding.</summary>
    public const int DefaultTrialDays = 14;

    /// <summary>Días estándar usados como base del prorrateo.</summary>
    public const int ProratingDaysInMonth = 30;

    /// <summary>
    /// Cargo prorrateado por upgrade mid-cycle. Si el plan nuevo cuesta
    /// menos o igual, devuelve 0 (no hay reembolso por downgrade —
    /// anti-pasarela, simple).
    ///
    /// Fórmula: (priceNew − priceOld) × (díasRestantes / 30), redondeado
    /// al peso. Devuelve 0 si daysRemaining ≤ 0 o si el cargo sería &lt;
    /// $1.000 (no vale la pena emitir una factura por pesos).
    /// </summary>
    public static decimal ComputeProratedUpgradeCharge(
        decimal oldMonthlyPrice,
        decimal newMonthlyPrice,
        double daysRemaining)
    {
        if (newMonthlyPrice <= oldMonthlyPrice) return 0m;
        if (daysRemaining <= 0) return 0m;

        var diff = newMonthlyPrice - oldMonthlyPrice;
        var prorated = diff * (decimal)(daysRemaining / ProratingDaysInMonth);
        var rounded = Math.Round(prorated, 0, MidpointRounding.AwayFromZero);
        return rounded < 1000m ? 0m : rounded;
    }
}
