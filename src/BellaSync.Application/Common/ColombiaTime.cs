namespace BellaSync.Application.Common;

/// <summary>
/// Constantes y helpers para manejar la zona horaria de Colombia (UTC-5)
/// de manera consistente en todo el codebase. Antes había `TimeSpan.FromHours(-5)`
/// repetido en 5+ handlers, lo que hacía fácil olvidarse de algún borde.
///
/// Colombia no observa horario de verano, así que el offset es fijo.
/// Si en el futuro adopta DST (improbable, pero ha estado en discusión
/// política varias veces), cambiar SOLO acá impacta toda la app.
/// </summary>
public static class ColombiaTime
{
    /// <summary>UTC-5 fijo. Colombia no usa horario de verano.</summary>
    public static readonly TimeSpan Offset = TimeSpan.FromHours(-5);

    /// <summary>Día calendario actual en Colombia para un UtcNow dado.</summary>
    public static DateOnly TodayFor(DateTime utcNow) =>
        DateOnly.FromDateTime(utcNow.Add(Offset));

    /// <summary>
    /// Convierte un día calendario Colombia a su rango UTC [00:00, 24:00).
    /// Usar este helper en cualquier query "del día" para evitar el bug
    /// histórico donde las citas vespertinas 19:00-23:59 CO se computaban
    /// en el día UTC siguiente.
    /// </summary>
    public static (DateTime startUtc, DateTime endUtc) DayRangeUtc(DateOnly day)
    {
        var startUtc = new DateTimeOffset(
            day.ToDateTime(TimeOnly.MinValue), Offset).UtcDateTime;
        return (startUtc, startUtc.AddDays(1));
    }
}
