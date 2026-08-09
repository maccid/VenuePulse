namespace VenuePulse.AccessControl.Application.Simulation;

/// <summary>Curvas temporales del motor de simulación. Funciones puras, [0,1] → [0,1], sin estado:
/// lo único que hay que poder explicar en el demo es "la venta acelera en los últimos días" y
/// "la llegada se concentra en las horas previas".</summary>
public static class SimulationCurves
{
    /// <summary>Ventas: más ventas en los últimos días (curva cuadrática, arranque lento).</summary>
    public static double Sales(double progress) => progress * progress;

    /// <summary>Llegada: se concentra en las horas previas al evento (curva cúbica, más abrupta).</summary>
    public static double Arrivals(double progress) => 1 - Math.Pow(1 - progress, 3);

    /// <summary>Aforo ya vendido: el modelo no puede vender por encima del objetivo ni por debajo
    /// de cero.</summary>
    public static double Clamp(double value) => Math.Clamp(value, 0, 1);
}
