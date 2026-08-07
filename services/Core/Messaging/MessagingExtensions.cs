using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace VenuePulse.Core.Messaging;

/// <summary>
/// Wiring compartido de MassTransit + RabbitMQ + Outbox transaccional de EF Core.
/// Cada servicio aporta su propio DbContext (dueño de sus tablas de Outbox/Inbox tras la
/// migración) y sus propios consumidores; aquí solo vive la fontanería repetida.
/// </summary>
public static class MessagingExtensions
{
    public static IServiceCollection AddVenuePulseMessaging<TDbContext>(
        this IServiceCollection services,
        string rabbitMqConnectionString,
        Action<IBusRegistrationConfigurator> configureConsumersAndSagas)
        where TDbContext : DbContext
    {
        services.AddMassTransit(x =>
        {
            configureConsumersAndSagas(x);

            x.AddEntityFrameworkOutbox<TDbContext>(o =>
            {
                o.UsePostgres();
                o.UseBusOutbox();
            });

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(new Uri(rabbitMqConnectionString));
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }

    /// <summary>
    /// Variante para AccessControl: no necesita EF Core Outbox propio para eventos salientes,
    /// solo consumir comandos entrantes y actualizar su propio estado en Postgres.
    /// </summary>
    public static IServiceCollection AddVenuePulseMessaging(
        this IServiceCollection services,
        string rabbitMqConnectionString,
        Action<IBusRegistrationConfigurator> configureConsumers)
    {
        services.AddMassTransit(x =>
        {
            configureConsumers(x);

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(new Uri(rabbitMqConnectionString));
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }

    /// <summary>
    /// Registra un state machine de Saga persistido en MongoDB en vez de EF Core — ver ADR
    /// sobre por qué el estado de la Saga vive en un documento y no en una fila relacional.
    /// </summary>
    public static IBusRegistrationConfigurator AddVenuePulseMongoSaga<TStateMachine, TState>(
        this IBusRegistrationConfigurator configurator,
        string mongoConnectionString,
        string databaseName)
        where TStateMachine : MassTransitStateMachine<TState>
        where TState : class, SagaStateMachineInstance, ISagaVersion
    {
        EnsureGuidRepresentationRegistered();

        configurator.AddSagaStateMachine<TStateMachine, TState>()
            .MongoDbRepository(r =>
            {
                r.Connection = mongoConnectionString;
                r.DatabaseName = databaseName;
            });

        return configurator;
    }

    /// <summary>
    /// MongoDB.Driver 2.19+ dejó de asumir una representación de Guid por defecto — sin este
    /// registro, serializar CUALQUIER Guid (como TicketPurchaseSagaState.ZoneId) lanza
    /// "GuidSerializer cannot serialize a Guid when GuidRepresentation is Unspecified" en el
    /// primer intento de guardar la Saga. Idempotente: en tests que crean el host varias veces
    /// en el mismo proceso, el segundo registro se ignora en vez de lanzar.
    /// </summary>
    private static void EnsureGuidRepresentationRegistered()
    {
        try
        {
            BsonSerializer.RegisterSerializer(new GuidSerializer(GuidRepresentation.Standard));
        }
        catch (BsonSerializationException)
        {
        }
    }
}
