using System.Text.RegularExpressions;

namespace BellaSync.Application.Features.Services.Validators;

/// <summary>
/// Constantes y reglas compartidas entre Create y Update de Service.
/// Si una regla cambia, se cambia aquí y aplica a ambos validators.
/// </summary>
internal static class ServiceValidationRules
{
    public const decimal PriceMin = 10_000m;
    public const decimal PriceMax = 500_000m;

    public const int DurationMin = 1;
    public const int DurationMax = 480; // 8 horas

    public const int NameMaxLength = 100;
    public const int DescriptionMaxLength = 500;

    public static readonly Regex HexColorRegex =
        new("^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})$", RegexOptions.Compiled);
}
