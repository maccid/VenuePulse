using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;

namespace VenuePulse.Core.Api;

/// <summary>
/// Serializa enums como su nombre (string) en vez del entero subyacente, para que un DTO como
/// SimulationRunStatusDto.Mode (AccessControl) no dependa del orden de declaración del enum y
/// sea legible en Swagger/logs sin tener que repetir el converter en cada servicio.
/// </summary>
public static class ApiExtensions
{
    public static IMvcBuilder AddVenuePulseControllersWithStringEnums(this IServiceCollection services) =>
        services.AddControllers().AddJsonOptions(options =>
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
}
