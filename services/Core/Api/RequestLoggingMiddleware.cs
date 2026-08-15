namespace VenuePulse.Core.Api;

using System.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

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
