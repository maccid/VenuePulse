namespace VenuePulse.AccessControl.Application.Prediction;

using VenuePulse.AccessControl.Application.Simulation;

/// <summary>Punto observado de la curva de llegada: progreso dentro de la ventana previa al
/// evento y personas que ya han pasado el torniquete (agregado del evento).</summary>
public sealed record ObservedArrival(double Progress, int Arrivals);

/// <summary>Resultado del ajuste del modelo de llegadas (Bloque 11).</summary>
public sealed record ArrivalForecastResult(
    bool FitFromData,
    int PredictedTotalArrivals,
    double NoShowRate,
    double? FitR2);

/// <summary>
/// Modelo de predicción de llegadas (Bloque 11). Regresión honesta de un parámetro — nada de ML
/// mágico: se ajusta por mínimos cuadrados lineales la asíntota A (llegadas totales esperadas,
/// es decir la "cola" de la curva) contra la forma de llegada fija
/// F(p)=1-(1-p)³. Es la misma forma que genera el simulador del Bloque 8
/// (<see cref="SimulationCurves.Arrivals"/>): el sistema predice con la forma de llegada que él
/// mismo genera, y de ahí salen los no-shows esperados (1 - A/ConfirmedSold).
/// </summary>
public static class ArrivalForecast
{
    /// <summary>No-show asumido cuando aún no hay historia de llegadas para ajustar la asíntota.</summary>
    public const double DefaultNoShowRate = 0.08;

    /// <summary>Forma de la curva de llegada: se concentra en las horas previas al evento.</summary>
    public static double Shape(double progress) => SimulationCurves.Arrivals(progress);

    /// <summary>
    /// Ajusta el modelo. Con menos de 2 puntos observados (aún en venta, sin llegadas) no hay
    /// nada que ajustar: se devuelve la asíntota por no-show asumido y <see cref="ArrivalForecastResult.FitFromData"/>
    /// = false para que el frontend lo muestre como supuesto, no como dato.
    /// </summary>
    public static ArrivalForecastResult Fit(IReadOnlyList<ObservedArrival> observed, int confirmedSold)
    {
        if (confirmedSold <= 0)
            return new ArrivalForecastResult(false, 0, 0, null);

        var points = observed
            .Where(o => o.Progress > 0 && o.Arrivals > 0)
            .OrderBy(o => o.Progress)
            .ToList();

        if (points.Count < 2)
        {
            var assumed = (int)Math.Round(confirmedSold * (1 - DefaultNoShowRate));
            return new ArrivalForecastResult(
                false,
                Math.Clamp(assumed, 0, confirmedSold),
                DefaultNoShowRate,
                null);
        }

        // Mínimos cuadrados lineales de la asíntota: minimizar Σ(yᵢ - A·F(pᵢ))² → A = Σ y·F / Σ F².
        double numerator = 0, denominator = 0;
        foreach (var point in points)
        {
            var shape = Shape(point.Progress);
            numerator += point.Arrivals * shape;
            denominator += shape * shape;
        }

        var total = denominator > 0 ? numerator / denominator : confirmedSold;

        // Invariantes: nunca por debajo de lo ya llegado (monótona) ni por encima de lo vendido.
        total = Math.Clamp(total, points[^1].Arrivals, confirmedSold);

        var noShowRate = Math.Clamp(1 - (total / confirmedSold), 0, 1);

        double? r2 = null;
        if (points.Count >= 2 && total > 0)
        {
            var mean = points.Average(point => (double)point.Arrivals);
            double sumSquaresResidual = 0, sumSquaresTotal = 0;
            foreach (var point in points)
            {
                var predicted = total * Shape(point.Progress);
                sumSquaresResidual += Math.Pow(point.Arrivals - predicted, 2);
                sumSquaresTotal += Math.Pow(point.Arrivals - mean, 2);
            }

            r2 = sumSquaresTotal > 0 ? 1 - (sumSquaresResidual / sumSquaresTotal) : null;
        }

        return new ArrivalForecastResult(true, (int)Math.Round(total), noShowRate, r2);
    }
}
