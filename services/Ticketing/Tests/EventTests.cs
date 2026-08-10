namespace VenuePulse.Ticketing.Tests;

using FluentAssertions;
using VenuePulse.Ticketing.Domain;

public class EventTests
{
    [Fact]
    public void Un_evento_sin_nombre_no_es_valido()
    {
        var act = () => new Event(Guid.NewGuid(), "", "Estadio", DateTimeOffset.UtcNow.AddDays(1));

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Un_tipo_de_entrada_con_precio_negativo_no_es_valido()
    {
        var @event = new Event(Guid.NewGuid(), "Final de Copa", "Estadio", DateTimeOffset.UtcNow.AddDays(1));

        var act = () => @event.AddTicketType(Guid.NewGuid(), "General", -10m, null);

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Anadir_un_tipo_de_entrada_lo_deja_disponible_en_el_evento()
    {
        var @event = new Event(Guid.NewGuid(), "Final de Copa", "Estadio", DateTimeOffset.UtcNow.AddDays(1));

        var ticketType = @event.AddTicketType(Guid.NewGuid(), "General", 25m, "Grada general");

        @event.TicketTypes.Should().ContainSingle().Which.Should().Be(ticketType);
    }

    [Fact]
    public void Un_evento_nuevo_nace_en_venta_y_con_relevancia_estandar()
    {
        var @event = new Event(Guid.NewGuid(), "Final de Copa", "Estadio", DateTimeOffset.UtcNow.AddDays(1));

        @event.Status.Should().Be(EventStatus.OnSale);
        @event.Relevance.Should().Be(EventRelevance.Standard);
    }

    [Fact]
    public void Cerrar_la_venta_de_un_evento_en_venta_lo_deja_SalesClosed()
    {
        var @event = new Event(Guid.NewGuid(), "Final de Copa", "Estadio", DateTimeOffset.UtcNow.AddDays(1));

        @event.CloseSales();

        @event.Status.Should().Be(EventStatus.SalesClosed);
    }

    [Fact]
    public void Cerrar_la_venta_dos_veces_no_es_valido()
    {
        var @event = new Event(Guid.NewGuid(), "Final de Copa", "Estadio", DateTimeOffset.UtcNow.AddDays(1));
        @event.CloseSales();

        var act = () => @event.CloseSales();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void Cancelar_un_evento_en_venta_lo_deja_Cancelled()
    {
        var @event = new Event(Guid.NewGuid(), "Final de Copa", "Estadio", DateTimeOffset.UtcNow.AddDays(1));

        @event.Cancel();

        @event.Status.Should().Be(EventStatus.Cancelled);
    }

    [Fact]
    public void No_se_puede_cancelar_un_evento_con_la_venta_ya_cerrada()
    {
        var @event = new Event(Guid.NewGuid(), "Final de Copa", "Estadio", DateTimeOffset.UtcNow.AddDays(1));
        @event.CloseSales();

        var act = () => @event.Cancel();

        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void La_relevancia_especial_multiplica_el_precio_base_por_1_5()
    {
        EventRelevance.Special.Multiplier().Should().Be(1.5m);
        EventRelevance.Standard.Multiplier().Should().Be(1.0m);
    }
}
