using System.Text.RegularExpressions;

namespace BellaSync.Application.Features.Stylists.Validators;

internal static class StylistValidationRules
{
    public const int FullNameMinLength = 3;
    public const int FullNameMaxLength = 150;

    public const int PhoneMaxLength = 30;

    public static readonly Regex HexColorRegex =
        new("^#([0-9A-Fa-f]{3}|[0-9A-Fa-f]{6})$", RegexOptions.Compiled);

    /// <summary>
    /// Acepta números colombianos: con o sin +57, con o sin espacios/guiones.
    /// Mínimo 7 dígitos (línea fija sin código de área en algunas regiones)
    /// para no ser demasiado restrictivo. Solo dígitos, +, espacios y guiones.
    /// </summary>
    public static readonly Regex PhoneRegex =
        new(@"^[\+\d][\d\s\-]{6,29}$", RegexOptions.Compiled);
}
