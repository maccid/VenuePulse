namespace VenuePulse.Ticketing.Tests;

using FluentAssertions;
using VenuePulse.Ticketing.Domain;

public class DynamicPricingTests
{
    [Fact]
    public void Sin_datos_de_ocupacion_el_precio_es_el_base()
    {
        var (effectivePrice, tier) = DynamicPricing.Apply(100m, occupancyPercentage: null);

        effectivePrice.Should().Be(100m);
        tier.Should().Be(DynamicPricing.TierNormal);
    }

    [Theory]
    [InlineData(0.0, 90.00)]
    [InlineData(0.39, 90.00)]
    public void Ocupacion_baja_baja_el_precio_un_10_por_ciento(double occupancy, double expectedPrice)
    {
        var (effectivePrice, tier) = DynamicPricing.Apply(100m, (decimal)occupancy);

        effectivePrice.Should().Be((decimal)expectedPrice);
        tier.Should().Be(DynamicPricing.TierBaja);
    }

    [Theory]
    [InlineData(0.40)]
    [InlineData(0.69)]
    public void Ocupacion_media_deja_el_precio_igual(double occupancy)
    {
        var (effectivePrice, tier) = DynamicPricing.Apply(100m, (decimal)occupancy);

        effectivePrice.Should().Be(100m);
        tier.Should().Be(DynamicPricing.TierNormal);
    }

    [Theory]
    [InlineData(0.70, 115.00)]
    [InlineData(0.89, 115.00)]
    public void Ocupacion_alta_sube_el_precio_un_15_por_ciento(double occupancy, double expectedPrice)
    {
        var (effectivePrice, tier) = DynamicPricing.Apply(100m, (decimal)occupancy);

        effectivePrice.Should().Be((decimal)expectedPrice);
        tier.Should().Be(DynamicPricing.TierAlta);
    }

    [Theory]
    [InlineData(0.90, 130.00)]
    [InlineData(1.0, 130.00)]
    public void Casi_agotado_sube_el_precio_un_30_por_ciento(double occupancy, double expectedPrice)
    {
        var (effectivePrice, tier) = DynamicPricing.Apply(100m, (decimal)occupancy);

        effectivePrice.Should().Be((decimal)expectedPrice);
        tier.Should().Be(DynamicPricing.TierUltimas);
    }

    [Fact]
    public void La_ocupacion_agregada_es_uno_menos_disponible_entre_aforo()
    {
        var occupancy = DynamicPricing.CalculateOccupancyPercentage(totalCapacity: 100, totalAvailableToSell: 30);

        occupancy.Should().Be(0.70m);
    }

    [Fact]
    public void Sin_aforo_conocido_no_hay_ocupacion_que_calcular()
    {
        var occupancy = DynamicPricing.CalculateOccupancyPercentage(totalCapacity: 0, totalAvailableToSell: 0);

        occupancy.Should().BeNull();
    }
}
