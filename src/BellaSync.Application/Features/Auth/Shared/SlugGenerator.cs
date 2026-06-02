using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace BellaSync.Application.Features.Auth.Shared;

/// <summary>
/// Genera slugs URL-friendly a partir del nombre del salón.
/// "Bella Spa Neiva" → "bella-spa-neiva".
/// Quita diacríticos, lowercase, reemplaza no alfanuméricos por guiones.
/// </summary>
internal static class SlugGenerator
{
    public static string Generate(string source)
    {
        if (string.IsNullOrWhiteSpace(source)) return Guid.NewGuid().ToString("N")[..8];

        var normalized = source.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (category != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        }

        var ascii = sb.ToString().Normalize(NormalizationForm.FormC);
        var slug = Regex.Replace(ascii, @"[^a-z0-9]+", "-").Trim('-');
        return string.IsNullOrEmpty(slug) ? Guid.NewGuid().ToString("N")[..8] : slug;
    }
}
