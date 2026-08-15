namespace VenuePulse.AccessControl.Application.Prediction;

using VenuePulse.AccessControl.Application.Simulation;

public sealed record ObservedArrival(double Progress, int Arrivals);

public sealed record ArrivalForecastResult(
    bool FitFromData,
    int PredictedTotalArrivals,
    double NoShowRate,
    double? FitR2);

public static class ArrivalForecast
{
    public const double DefaultNoShowRate = 0.08;

    public static double Shape(double progress) => SimulationCurves.Arrivals(progress);

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

        double numerator = 0, denominator = 0;
        foreach (var point in points)
        {
            var shape = Shape(point.Progress);
            numerator += point.Arrivals * shape;
            denominator += shape * shape;
        }

        var total = denominator > 0 ? numerator / denominator : confirmedSold;

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
