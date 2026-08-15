using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.Serialization.Serializers;

namespace VenuePulse.Core.Messaging;

public static class MessagingExtensions
{
    public static IServiceCollection AddMessaging<TDbContext>(
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
                cfg.UseMessageRetry(r => r.Intervals(100, 300, 800));
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }

    public static IServiceCollection AddMessaging(
        this IServiceCollection services,
        string rabbitMqConnectionString,
        Action<IBusRegistrationConfigurator> configureConsumers,
        Action<IBusRegistrationContext, IRabbitMqBusFactoryConfigurator>? configureBus = null)
    {
        services.AddMassTransit(x =>
        {
            configureConsumers(x);

            x.UsingRabbitMq((context, cfg) =>
            {
                cfg.Host(new Uri(rabbitMqConnectionString));
                configureBus?.Invoke(context, cfg);
                cfg.ConfigureEndpoints(context);
            });
        });

        return services;
    }

    public static IBusRegistrationConfigurator AddMongoSaga<TStateMachine, TState>(
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

    public static IBusRegistrationConfigurator AddMongoSaga<TStateMachine, TState, TDefinition>(
        this IBusRegistrationConfigurator configurator,
        string mongoConnectionString,
        string databaseName)
        where TStateMachine : MassTransitStateMachine<TState>
        where TState : class, SagaStateMachineInstance, ISagaVersion
        where TDefinition : class, ISagaDefinition<TState>
    {
        EnsureGuidRepresentationRegistered();

        configurator.AddSagaStateMachine<TStateMachine, TState, TDefinition>()
            .MongoDbRepository(r =>
            {
                r.Connection = mongoConnectionString;
                r.DatabaseName = databaseName;
            });

        return configurator;
    }

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
