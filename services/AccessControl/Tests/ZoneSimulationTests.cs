namespace VenuePulse.AccessControl.Tests;

using FluentAssertions;
using VenuePulse.AccessControl.Domain;

public class ZoneSimulationTests
{
    private static Zone CreateZone(int capacity = 100) => new(Guid.NewGuid(), Guid.NewGuid(), "Puerta Norte", capacity);

    [Fact]
    public void Ventas_simuladas_confirmadas_respetan_el_aforo()
    {
        var zone = CreateZone(capacity: 10);

        zone.RecordSimulatedSales(6);

        zone.ConfirmedSold.Should().Be(6);
        zone.AvailableToSell.Should().Be(4);
    }

    [Fact]
    public void Venta_simulada_por_encima_del_aforo_falla()
    {
        var zone = CreateZone(capacity: 10);

        var act = () => zone.RecordSimulatedSales(11);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Check_in_simulado_nunca_supera_lo_confirmado_vendido()
    {
        var zone = CreateZone(capacity: 10);
        zone.RecordSimulatedSales(8);

        zone.RecordSimulatedCheckIns(7);

        zone.CheckedIn.Should().Be(7);
        zone.ConfirmedSold.Should().Be(8);
    }

    [Fact]
    public void Check_in_simulado_por_encima_de_confirmado_falla()
    {
        var zone = CreateZone(capacity: 10);
        zone.RecordSimulatedSales(8);

        var act = () => zone.RecordSimulatedCheckIns(9);

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Los_no_shows_quedan_representados_como_confirmado_menos_checked_in()
    {
        var zone = CreateZone(capacity: 10);
        zone.RecordSimulatedSales(10);

        zone.RecordSimulatedCheckIns(9);

        (zone.ConfirmedSold - zone.CheckedIn).Should().Be(1);
    }

    [Fact]
    public void Reset_pone_los_contadores_a_cero()
    {
        var zone = CreateZone(capacity: 10);
        zone.ReserveSlot();
        zone.ConfirmSlot();
        zone.RecordCheckIn();

        zone.ResetCounters();

        zone.Reserved.Should().Be(0);
        zone.ConfirmedSold.Should().Be(0);
        zone.CheckedIn.Should().Be(0);
    }
}
