namespace VenuePulse.AccessControl.Tests;

using FluentAssertions;
using VenuePulse.AccessControl.Domain;

public class ZoneTests
{
    [Fact]
    public void No_se_puede_reservar_por_encima_del_aforo()
    {
        var zone = new Zone(Guid.NewGuid(), "Puerta Norte", capacity: 1);
        zone.ReserveSlot();

        var act = zone.ReserveSlot;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Confirmar_una_reserva_mueve_el_contador_a_vendidas()
    {
        var zone = new Zone(Guid.NewGuid(), "Puerta Norte", capacity: 10);
        zone.ReserveSlot();

        zone.ConfirmSlot();

        zone.Reserved.Should().Be(0);
        zone.ConfirmedSold.Should().Be(1);
    }

    [Fact]
    public void Liberar_sin_reserva_pendiente_falla()
    {
        var zone = new Zone(Guid.NewGuid(), "Puerta Norte", capacity: 10);

        var act = zone.ReleaseSlot;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void No_se_puede_hacer_check_in_sin_entradas_confirmadas()
    {
        var zone = new Zone(Guid.NewGuid(), "Puerta Norte", capacity: 10);

        var act = zone.RecordCheckIn;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Check_in_tras_confirmar_incrementa_la_ocupacion_en_vivo()
    {
        var zone = new Zone(Guid.NewGuid(), "Puerta Norte", capacity: 10);
        zone.ReserveSlot();
        zone.ConfirmSlot();

        zone.RecordCheckIn();

        zone.CheckedIn.Should().Be(1);
    }
}
