namespace VenuePulse.Architecture.Tests;

using ArchUnitNET.Domain;
using ArchUnitNET.Loader;
using ArchUnitNET.xUnit;
using static ArchUnitNET.Fluent.ArchRuleDefinition;

/// <summary>
/// No promete Clean Architecture, la hace fallar el build si se rompe. Analiza los ensamblados
/// reales de los 3 servicios + Core, no una copia aparte de las reglas.
/// </summary>
public class CleanArchitectureTests
{
    private static readonly Architecture Architecture = new ArchLoader()
        .LoadAssemblies(
            typeof(Core.Domain.Entity<Guid>).Assembly,
            typeof(Ticketing.Domain.Event).Assembly,
            typeof(Ticketing.Application.ITicketingService).Assembly,
            typeof(Ticketing.Infrastructure.TicketingDbContext).Assembly,
            typeof(AccessControl.Domain.Zone).Assembly,
            typeof(AccessControl.Application.IZoneService).Assembly,
            typeof(AccessControl.Infrastructure.AccessControlDbContext).Assembly,
            typeof(CheckIn.Domain.Ticket).Assembly,
            typeof(CheckIn.Application.ITicketRepository).Assembly,
            typeof(CheckIn.Infrastructure.CheckInDbContext).Assembly)
        .Build();

    [Theory]
    [InlineData("Ticketing")]
    [InlineData("AccessControl")]
    [InlineData("CheckIn")]
    public void El_dominio_no_depende_de_infraestructura_ni_de_application(string service)
    {
        IObjectProvider<IType> domainLayer = Types()
            .That().ResideInNamespace($"VenuePulse.{service}.Domain")
            .As($"{service}.Domain");

        IObjectProvider<IType> outerLayers = Types()
            .That().ResideInNamespace($"VenuePulse.{service}.Infrastructure")
            .Or().ResideInNamespace($"VenuePulse.{service}.Application")
            .Or().ResideInNamespace($"VenuePulse.{service}.Api")
            .As($"{service}.Infrastructure/Application/Api");

        Types().That().Are(domainLayer)
            .Should().NotDependOnAny(outerLayers)
            .Because("el dominio no debe conocer capas externas (Clean Architecture)")
            .Check(Architecture);
    }

    [Theory]
    [InlineData("Ticketing")]
    [InlineData("AccessControl")]
    [InlineData("CheckIn")]
    public void El_dominio_no_referencia_EF_Core_ni_MassTransit_directamente(string service)
    {
        IObjectProvider<IType> domainLayer = Types()
            .That().ResideInNamespace($"VenuePulse.{service}.Domain")
            .As($"{service}.Domain");

        IObjectProvider<IType> frameworkTypes = Types()
            .That().ResideInNamespace("Microsoft.EntityFrameworkCore")
            .Or().ResideInNamespace("MassTransit")
            .As("EF Core / MassTransit");

        Types().That().Are(domainLayer)
            .Should().NotDependOnAny(frameworkTypes)
            .Because("el dominio es persistence-ignorant y transport-ignorant")
            .Check(Architecture);
    }

    [Fact]
    public void Solo_CheckIn_aplica_CQRS_con_MediatR()
    {
        IObjectProvider<IType> mediatRTypes = Types()
            .That().ResideInNamespace("MediatR")
            .As("MediatR");

        IObjectProvider<IType> simpleApplicationLayers = Types()
            .That().ResideInNamespace("VenuePulse.Ticketing.Application")
            .Or().ResideInNamespace("VenuePulse.AccessControl.Application")
            .As("Ticketing.Application / AccessControl.Application");

        // Ticketing y AccessControl son Application Services simples (sin CQRS, ver ADR 0003) —
        // si algún día aparece un IRequestHandler ahí, es una señal de sobre-ingeniería, no de progreso.
        Types().That().Are(simpleApplicationLayers)
            .Should().NotDependOnAny(mediatRTypes)
            .Because("CQRS se reserva para donde hay asimetría real lectura/escritura (CheckIn)")
            .Check(Architecture);
    }

    [Theory]
    [InlineData("Ticketing", "AccessControl", "CheckIn")]
    [InlineData("AccessControl", "Ticketing", "CheckIn")]
    [InlineData("CheckIn", "Ticketing", "AccessControl")]
    public void Un_servicio_no_referencia_directamente_a_otro(string service, string other1, string other2)
    {
        IObjectProvider<IType> ownLayers = Types()
            .That().ResideInNamespace($"VenuePulse.{service}.Domain")
            .Or().ResideInNamespace($"VenuePulse.{service}.Application")
            .Or().ResideInNamespace($"VenuePulse.{service}.Infrastructure")
            .As(service);

        IObjectProvider<IType> otherServicesLayers = Types()
            .That().ResideInNamespace($"VenuePulse.{other1}.Domain")
            .Or().ResideInNamespace($"VenuePulse.{other1}.Application")
            .Or().ResideInNamespace($"VenuePulse.{other1}.Infrastructure")
            .Or().ResideInNamespace($"VenuePulse.{other2}.Domain")
            .Or().ResideInNamespace($"VenuePulse.{other2}.Application")
            .Or().ResideInNamespace($"VenuePulse.{other2}.Infrastructure")
            .As($"{other1} / {other2}");

        // Un microservicio, una base de datos (ver .ia/RULES.md) — la única dependencia compartida
        // entre los 3 es VenuePulse.Core (Kernel/Bootstrap) y VenuePulse.Contracts (contratos de
        // integración vía el bus), nunca el ensamblado de otro servicio directamente.
        Types().That().Are(ownLayers)
            .Should().NotDependOnAny(otherServicesLayers)
            .Because("cada microservicio tiene su propia base de datos — la única dependencia compartida es Core/Contracts, nunca otro servicio directamente")
            .Check(Architecture);
    }
}
