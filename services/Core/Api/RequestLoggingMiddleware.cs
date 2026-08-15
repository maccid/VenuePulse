namespace VenuePulse.Core.Api;

using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

/// <summary>
/// Log de cada petición HTTP (método, ruta, status y duración) en los 3 servicios — hasta ahora
/// los endpoints sin CQRS/MediatR de <c>Ticketing</c>/<c>AccessControl</c> eran silenciosos y una
/// compra no dejaba rastro HTTP en el log hasta que se registró esta pieza (ver ADR 0033). El
/// <c>TraceId</c> de cada línea lo añade el enricher <c>WithSpan()</c> del pipeline de Serilog, así
/// que en Seq cada petición queda correlacionada con el trace distribuido de la compra. El log de
/// ASP.NET Core (<c>Microsoft.AspNetCore</c>) está en <c>Warning</c> en los appsettings, por eso se
/// necesita esto en vez de depender de su logging por defecto.
/// </summary>
public sealed class RequestLoggingMiddleware(RequestDelegate next, ILogger<RequestLoggingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        var stopwatch = Stopwatch.StartNew();
        try
        {
            await next(context);
        }
        finally
        {
            stopwatch.Stop();

            var status = context.Response.StatusCode;
            if (status >= 500)
                logger.LogError("HTTP {Method} {Path} -> {StatusCode} en {ElapsedMs} ms", context.Request.Method, context.Request.Path, status, stopwatch.ElapsedMilliseconds);
            else if (status >= 400)
                logger.LogWarning("HTTP {Method} {Path} -> {StatusCode} en {ElapsedMs} ms", context.Request.Method, context.Request.Path, status, stopwatch.ElapsedMilliseconds);
            else
                logger.LogInformation("HTTP {Method} {Path} -> {StatusCode} en {ElapsedMs} ms", context.Request.Method, context.Request.Path, status, stopwatch.ElapsedMilliseconds);
        }
    }
}
