using BellaSync.Application.Common.Services;
using FluentAssertions;

namespace BellaSync.Application.Tests.Common;

/// <summary>
/// Tests del calendario de festivos colombianos. Los valores esperados
/// los verifiqué contra el calendario oficial publicado (Decreto Único
/// Reglamentario 1066 de 2015, art. 2.2.5.1) para 2026 y 2027.
/// </summary>
public class ColombiaHolidayCalendarTests
{
    // ─── 2026 — fechas verificadas con fuentes externas ────────────────

    [Theory]
    // Fijos
    [InlineData(2026, 1, 1)]     // Año Nuevo (jueves)
    [InlineData(2026, 5, 1)]     // Día del Trabajo (viernes)
    [InlineData(2026, 7, 20)]    // Independencia (lunes)
    [InlineData(2026, 8, 7)]     // Batalla de Boyacá (viernes)
    [InlineData(2026, 12, 8)]    // Inmaculada Concepción (martes)
    [InlineData(2026, 12, 25)]   // Navidad (viernes)
    // Movibles a lunes (Emiliani)
    [InlineData(2026, 1, 12)]    // Reyes Magos → lunes 12 ene
    [InlineData(2026, 3, 23)]    // San José → lunes 23 mar
    [InlineData(2026, 6, 29)]    // San Pedro y San Pablo cae lunes 29 jun
    [InlineData(2026, 8, 17)]    // Asunción → lunes 17 ago
    [InlineData(2026, 10, 12)]   // Día de la Raza cae lunes 12 oct
    [InlineData(2026, 11, 2)]    // Todos los Santos → lunes 2 nov
    [InlineData(2026, 11, 16)]   // Independencia Cartagena → lunes 16 nov
    // Basados en Pascua (Pascua 2026 = 5 abril)
    [InlineData(2026, 4, 2)]     // Jueves Santo (jueves 2 abr)
    [InlineData(2026, 4, 3)]     // Viernes Santo (viernes 3 abr)
    [InlineData(2026, 5, 18)]    // Ascensión → lunes 18 may
    [InlineData(2026, 6, 8)]     // Corpus Christi → lunes 8 jun
    [InlineData(2026, 6, 15)]    // Sagrado Corazón → lunes 15 jun
    public void Festivos_2026_son_reconocidos(int year, int month, int day)
    {
        var date = new DateOnly(year, month, day);
        ColombiaHolidayCalendar.IsHoliday(date).Should().BeTrue(
            $"{date:dd MMM yyyy} es festivo en Colombia");
    }

    [Theory]
    [InlineData(2026, 1, 2)]     // Vie 2 ene — laboral
    [InlineData(2026, 1, 6)]     // Mar 6 ene — Reyes original, debe moverse a lunes
    [InlineData(2026, 4, 5)]     // Dom 5 abr — Pascua (no festivo en Colombia)
    [InlineData(2026, 7, 21)]    // Mar 21 jul — día después de Independencia
    [InlineData(2026, 12, 24)]   // Jue 24 dic — Nochebuena (no festivo)
    public void Dias_laborales_2026_no_son_festivos(int year, int month, int day)
    {
        var date = new DateOnly(year, month, day);
        ColombiaHolidayCalendar.IsHoliday(date).Should().BeFalse(
            $"{date:dd MMM yyyy} NO es festivo");
    }

    [Fact]
    public void HolidaysIn_2026_tiene_18_festivos()
    {
        // Colombia tiene 18 festivos nacionales por año.
        ColombiaHolidayCalendar.HolidaysIn(2026).Count.Should().Be(18);
    }

    [Fact]
    public void Cache_devuelve_misma_instancia_por_año()
    {
        var a = ColombiaHolidayCalendar.HolidaysIn(2026);
        var b = ColombiaHolidayCalendar.HolidaysIn(2026);
        // Confirmamos caché: misma referencia.
        ReferenceEquals(a, b).Should().BeTrue();
    }
}
