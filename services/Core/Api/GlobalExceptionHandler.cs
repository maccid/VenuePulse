namespace VenuePulse.Core.Api;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

/// <summary>
/// Red de seguridad para excepciones NO controladas (bug, fallo de infraestructura) — no sustituye
/// a <c>Result</c> (fallos de negocio esperables, ver ADR 0011) ni introduce una jerarquía de
/// excepciones de negocio paralela. Sin esto, una excepción sin capturar llega al cliente como la
/// página de error por defecto de ASP.NET Core en vez de un <see cref="ProblemDetails"/> JSON.
/// </summary>
public sealed class GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        logger.LogError(exception, "Excepción no controlada en {Path}", httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;

        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Error interno del servidor",
                Detail = "Ha ocurrido un error inesperado procesando la petición.",
            },
            cancellationToken);

        return true;
    }
}
