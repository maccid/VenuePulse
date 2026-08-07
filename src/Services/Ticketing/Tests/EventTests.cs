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
}
