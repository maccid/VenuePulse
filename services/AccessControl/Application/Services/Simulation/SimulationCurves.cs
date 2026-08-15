namespace VenuePulse.AccessControl.Application.Simulation;

public static class SimulationCurves
{
    public static double Sales(double progress) => progress * progress;

    public static double Arrivals(double progress) => 1 - Math.Pow(1 - progress, 3);

    public static double Clamp(double value) => Math.Clamp(value, 0, 1);
}
