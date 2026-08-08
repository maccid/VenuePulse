namespace VenuePulse.AccessControl.Tests;

using FluentAssertions;
using VenuePulse.AccessControl.Application;

public class SimulationCurvesTests
{
    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(0.5, 0.25)]
    [InlineData(1.0, 1.0)]
    public void Sales_es_la_curva_cuadratica_p2(double progress, double expected)
    {
        SimulationCurves.Sales(progress).Should().BeApproximately(expected, 0.0001);
    }

    [Theory]
    [InlineData(0.0, 0.0)]
    [InlineData(0.5, 0.875)]
    [InlineData(1.0, 1.0)]
    public void Arrivals_es_la_curva_cubica_1_minus_1_minus_p_cubed(double progress, double expected)
    {
        SimulationCurves.Arrivals(progress).Should().BeApproximately(expected, 0.0001);
    }

    [Fact]
    public void Arrivals_crece_mas_rapido_al_final_pero_sin_pasar_de_uno()
    {
        var early = SimulationCurves.Arrivals(0.25);
        var late = SimulationCurves.Arrivals(0.75);

        early.Should().BeLessThan(late);
        SimulationCurves.Arrivals(1.0).Should().Be(1.0);
    }

    [Fact]
    public void Sales_arranca_lento_y_acelera_al_final()
    {
        var early = SimulationCurves.Sales(0.25);
        var late = SimulationCurves.Sales(0.75);

        early.Should().Be(0.0625);
        late.Should().Be(0.5625);
    }
}
