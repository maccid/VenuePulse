namespace VenuePulse.AccessControl.Tests;

using FluentAssertions;
using VenuePulse.AccessControl.Domain;

public class ZoneTests
{
    [Fact]
    public void No_se_puede_reservar_por_encima_del_aforo()
    {
        var zone = new Zone(Guid.NewGuid(), Guid.NewGuid(), "Puerta Norte", capacity: 1);
        zone.ReserveSlot();

        var act = zone.ReserveSlot;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Confirmar_una_reserva_mueve_el_contador_a_vendidas()
    {
        var zone = new Zone(Guid.NewGuid(), Guid.NewGuid(), "Puerta Norte", capacity: 10);
        zone.ReserveSlot();

        zone.ConfirmSlot();

        zone.Reserved.Should().Be(0);
        zone.ConfirmedSold.Should().Be(1);
    }

    [Fact]
    public void Liberar_sin_reserva_pendiente_falla()
    {
        var zone = new Zone(Guid.NewGuid(), Guid.NewGuid(), "Puerta Norte", capacity: 10);

        var act = zone.ReleaseSlot;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void No_se_puede_hacer_check_in_sin_entradas_confirmadas()
    {
        var zone = new Zone(Guid.NewGuid(), Guid.NewGuid(), "Puerta Norte", capacity: 10);

        var act = zone.RecordCheckIn;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Check_in_tras_confirmar_incrementa_la_ocupacion_en_vivo()
    {
        var zone = new Zone(Guid.NewGuid(), Guid.NewGuid(), "Puerta Norte", capacity: 10);
        zone.ReserveSlot();
        zone.ConfirmSlot();

        zone.RecordCheckIn();

        zone.CheckedIn.Should().Be(1);
    }

    [Fact]
    public void Cancelar_la_zona_fija_el_flag_y_es_idempotente()
    {
        var zone = new Zone(Guid.NewGuid(), Guid.NewGuid(), "Puerta Norte", capacity: 10);

        zone.Cancel();
        zone.Cancel();

        zone.Cancelled.Should().BeTrue();
    }

    [Fact]
    public void No_se_puede_cancelar_una_zona_con_gente_dentro()
    {
        var zone = new Zone(Guid.NewGuid(), Guid.NewGuid(), "Puerta Norte", capacity: 10);
        zone.ReserveSlot();
        zone.ConfirmSlot();
        zone.RecordCheckIn();

        var act = zone.Cancel;

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Liberar_una_entrada_confirmada_decrementa_vendidas()
    {
        var zone = new Zone(Guid.NewGuid(), Guid.NewGuid(), "Puerta Norte", capacity: 10);
        zone.ReserveSlot();
        zone.ConfirmSlot();

        zone.ReleaseConfirmedSlot();

        zone.ConfirmedSold.Should().Be(0);
    }

    [Fact]
    public void Liberar_una_entrada_confirmada_sin_confirmadas_falla()
    {
        var zone = new Zone(Guid.NewGuid(), Guid.NewGuid(), "Puerta Norte", capacity: 10);

        var act = zone.ReleaseConfirmedSlot;

        act.Should().Throw<InvalidOperationException>();
    }
}
