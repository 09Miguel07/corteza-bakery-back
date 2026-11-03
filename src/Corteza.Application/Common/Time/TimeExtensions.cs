namespace Corteza.Application.Common.Time;

/// <summary>"Hoy" de la panadería. Colombia es UTC-5 todo el año (sin horario de verano).</summary>
public static class TimeExtensions
{
    private static readonly TimeSpan ColombiaOffset = TimeSpan.FromHours(-5);

    public static DateOnly ColombiaToday(this TimeProvider time) =>
        DateOnly.FromDateTime(time.GetUtcNow().ToOffset(ColombiaOffset).DateTime);

    /// <summary>Inicio del día de hoy en Colombia, expresado en UTC (para filtrar fechas guardadas en UTC).</summary>
    public static DateTime ColombiaStartOfDayUtc(this TimeProvider time) =>
        time.ColombiaToday().ToDateTime(TimeOnly.MinValue).Add(-ColombiaOffset);
}
