using System.Globalization;
using BellaSync.Domain.Entities;
using BellaSync.Domain.ValueObjects;

namespace BellaSync.Application.Common.Services;

/// <summary>
/// Reemplaza placeholders {nombre}, {fecha}, {hora}, etc. en el body de
/// una plantilla con los valores reales de una cita/cliente/salón.
///
/// Reglas:
///   - Placeholder no presente en el data → se reemplaza con string vacío
///     (NO se deja {fecha} literal en el mensaje final, queda feo).
///   - Hora se formatea como "3:00 pm" (12h con am/pm en minúscula).
///   - Fecha como "sáb 7 jun" (corto local).
///   - Montos con formato "$80.000" (separador de miles, sin decimales).
///
/// Por qué un service y no método estático: lo dejamos inyectable por DI
/// para que el dispatcher pueda usarlo y los tests puedan mockear si
/// hace falta. No tiene estado.
/// </summary>
public sealed class WhatsAppTemplateRenderer
{
    private static readonly CultureInfo CoCulture = CultureInfo.GetCultureInfo("es-CO");

    /// <summary>
    /// Renderiza el body usando los datos provistos. Cualquier placeholder
    /// {x} que no esté en `data` queda como string vacío.
    /// </summary>
    public string Render(string body, IDictionary<string, string?> data)
    {
        if (string.IsNullOrWhiteSpace(body)) return string.Empty;

        var result = body;
        foreach (var (key, value) in data)
        {
            var placeholder = "{" + key + "}";
            result = result.Replace(placeholder, value ?? string.Empty);
        }

        // Limpia placeholders no resueltos para que no quede {ruido} en el
        // mensaje final. Regex liberal pero match estricto: solo {letras}.
        result = System.Text.RegularExpressions.Regex.Replace(
            result,
            @"\{[a-zA-ZáéíóúñÁÉÍÓÚÑ]+\}",
            string.Empty);

        return result.Trim();
    }

    /// <summary>
    /// Helper para construir el diccionario a partir de la combinación
    /// típica (cita + cliente + servicio + estilista + tenant). El
    /// dispatcher arma esto antes de llamar Render.
    /// </summary>
    public static IDictionary<string, string?> BuildContext(
        Customer customer,
        Service service,
        Appointment? appointment,
        Tenant tenant,
        TimeZoneInfo? tz = null)
    {
        // Por default trabajamos en hora Colombia (UTC-5).
        tz ??= TimeZoneInfo.CreateCustomTimeZone("Colombia", TimeSpan.FromHours(-5), "Colombia", "Colombia");

        var dict = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase)
        {
            ["nombre"] = customer.FullName.Split(' ').FirstOrDefault() ?? customer.FullName,
            ["servicio"] = service.Name,
            ["salon"] = tenant.Name,
            ["direccion"] = tenant.Address,
        };

        if (appointment is not null)
        {
            var localStart = TimeZoneInfo.ConvertTimeFromUtc(
                DateTime.SpecifyKind(appointment.StartAt, DateTimeKind.Utc), tz);

            dict["fecha"] = FormatDateShort(localStart);
            dict["hora"] = FormatTime12(localStart);

            if (appointment.HoldExpiresAt.HasValue)
            {
                var localExpire = TimeZoneInfo.ConvertTimeFromUtc(
                    DateTime.SpecifyKind(appointment.HoldExpiresAt.Value, DateTimeKind.Utc), tz);
                dict["limite"] = $"las {FormatTime12(localExpire)}";
            }
        }

        // Anticipo si la cita pide depósito (DepositAmount > 0 = requiere).
        if (appointment is not null && appointment.DepositAmount.Amount > 0m)
        {
            dict["anticipo"] = FormatMoney(appointment.DepositAmount);
        }

        return dict;
    }

    private static string FormatDateShort(DateTime d)
    {
        // "sáb 7 jun" — día corto + número + mes corto, todo en español CO.
        return d.ToString("ddd d MMM", CoCulture).Replace(".", string.Empty).ToLowerInvariant();
    }

    private static string FormatTime12(DateTime d)
    {
        // "3:00 pm"
        return d.ToString("h:mm tt", CoCulture).ToLowerInvariant();
    }

    private static string FormatMoney(Money m)
    {
        return "$" + m.Amount.ToString("N0", CoCulture);
    }
}
