namespace BellaSync.Application.Common.Services;

/// <summary>
/// Calculadora de festivos nacionales de Colombia. Combina:
///   - Festivos fijos (Año Nuevo, Trabajo, Independencia, etc.).
///   - Festivos movibles a lunes por la Ley Emiliani de 1983
///     (Reyes, San José, Asunción, Día de la Raza, etc.).
///   - Festivos religiosos basados en la Pascua, calculada con el
///     algoritmo de Gauss para domingos de Pascua occidentales.
///     De los basados en Pascua, los de "Jueves/Viernes Santo"
///     quedan fijos (jueves y viernes); los demás se mueven a
///     lunes (Ascensión, Corpus Christi, Sagrado Corazón).
///
/// Servicio puro — sin IO, sin state — para que sea trivialmente
/// testeable y reusable.
/// </summary>
public static class ColombiaHolidayCalendar
{
    /// <summary>True si la fecha es festivo nacional en Colombia.</summary>
    public static bool IsHoliday(DateOnly date)
    {
        return HolidaysIn(date.Year).Contains(date);
    }

    /// <summary>
    /// Devuelve todos los festivos nacionales de un año en Colombia.
    /// Lo cacheamos en memoria por año porque calcular es barato pero
    /// se llama mucho durante la validación de citas.
    /// </summary>
    public static IReadOnlySet<DateOnly> HolidaysIn(int year)
    {
        if (_cache.TryGetValue(year, out var cached)) return cached;
        var set = ComputeHolidaysIn(year);
        _cache[year] = set;
        return set;
    }

    // Caché simple — un dict thread-unsafe está bien acá: si dos hilos
    // computan el mismo año a la vez, el resultado es el mismo y solo
    // duplican el trabajo una vez.
    private static readonly Dictionary<int, IReadOnlySet<DateOnly>> _cache = new();

    private static IReadOnlySet<DateOnly> ComputeHolidaysIn(int year)
    {
        var result = new HashSet<DateOnly>();

        // 1) Festivos fijos — no se mueven nunca.
        result.Add(new DateOnly(year, 1, 1));   // Año Nuevo
        result.Add(new DateOnly(year, 5, 1));   // Día del Trabajo
        result.Add(new DateOnly(year, 7, 20));  // Independencia
        result.Add(new DateOnly(year, 8, 7));   // Batalla de Boyacá
        result.Add(new DateOnly(year, 12, 8));  // Inmaculada Concepción
        result.Add(new DateOnly(year, 12, 25)); // Navidad

        // 2) Festivos movibles por Ley Emiliani — al lunes siguiente
        //    si no caen en lunes. (Si cae lunes, queda igual.)
        result.Add(ToNextMonday(new DateOnly(year, 1, 6)));    // Reyes Magos
        result.Add(ToNextMonday(new DateOnly(year, 3, 19)));   // San José
        result.Add(ToNextMonday(new DateOnly(year, 6, 29)));   // San Pedro y San Pablo
        result.Add(ToNextMonday(new DateOnly(year, 8, 15)));   // Asunción
        result.Add(ToNextMonday(new DateOnly(year, 10, 12)));  // Día de la Raza
        result.Add(ToNextMonday(new DateOnly(year, 11, 1)));   // Todos los Santos
        result.Add(ToNextMonday(new DateOnly(year, 11, 11)));  // Independencia Cartagena

        // 3) Festivos basados en la Pascua.
        var easter = EasterSunday(year);
        result.Add(easter.AddDays(-3));                            // Jueves Santo (queda jueves)
        result.Add(easter.AddDays(-2));                            // Viernes Santo (queda viernes)
        result.Add(ToNextMonday(easter.AddDays(43)));              // Ascensión (movible)
        result.Add(ToNextMonday(easter.AddDays(64)));              // Corpus Christi (movible)
        result.Add(ToNextMonday(easter.AddDays(71)));              // Sagrado Corazón (movible)

        return result;
    }

    /// <summary>
    /// Si la fecha es lunes la deja igual. Si no, devuelve el lunes
    /// siguiente. System.DayOfWeek tiene Sunday=0 … Monday=1, así que
    /// días-hasta-lunes = (1 - dayOfWeek + 7) % 7.
    /// </summary>
    private static DateOnly ToNextMonday(DateOnly date)
    {
        var dow = (int)date.DayOfWeek;
        var daysUntilMonday = (1 - dow + 7) % 7;
        return date.AddDays(daysUntilMonday);
    }

    /// <summary>
    /// Algoritmo de Gauss para calcular el domingo de Pascua occidental
    /// (calendario gregoriano). Devuelve el DateOnly correspondiente.
    /// </summary>
    private static DateOnly EasterSunday(int year)
    {
        var a = year % 19;
        var b = year / 100;
        var c = year % 100;
        var d = b / 4;
        var e = b % 4;
        var f = (b + 8) / 25;
        var g = (b - f + 1) / 3;
        var h = (19 * a + b - d - g + 15) % 30;
        var i = c / 4;
        var k = c % 4;
        var l = (32 + 2 * e + 2 * i - h - k) % 7;
        var m = (a + 11 * h + 22 * l) / 451;
        var month = (h + l - 7 * m + 114) / 31;
        var day = ((h + l - 7 * m + 114) % 31) + 1;
        return new DateOnly(year, month, day);
    }
}
