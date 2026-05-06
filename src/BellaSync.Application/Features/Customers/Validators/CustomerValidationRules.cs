using System.Text.RegularExpressions;

namespace BellaSync.Application.Features.Customers.Validators;

internal static class CustomerValidationRules
{
    public const int FullNameMinLength = 3;
    public const int FullNameMaxLength = 150;

    public const int PhoneMaxLength = 30;
    public const int EmailMaxLength = 150;
    public const int DocumentMaxLength = 30;
    public const int AddressMaxLength = 250;
    public const int NotesMaxLength = 2000;

    /// <summary>
    /// Acepta números colombianos: con o sin +57, con o sin espacios/guiones.
    /// Mínimo 7 dígitos para no ser demasiado restrictivo.
    /// </summary>
    public static readonly Regex PhoneRegex =
        new(@"^[\+\d][\d\s\-]{6,29}$", RegexOptions.Compiled);
}
