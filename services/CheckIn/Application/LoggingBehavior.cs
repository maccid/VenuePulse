using MediatR;
using Microsoft.Extensions.Logging;

namespace VenuePulse.CheckIn.Application;

public sealed class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        var requestName = typeof(TRequest).Name;
        logger.LogInformation("Procesando {RequestName}", requestName);

        try
        {
            var response = await next(cancellationToken);
            logger.LogInformation("{RequestName} completado", requestName);
            return response;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "{RequestName} ha fallado", requestName);
            throw;
        }
    }
}
