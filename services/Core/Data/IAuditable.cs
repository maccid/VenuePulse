namespace VenuePulse.Core.Data;

/// <summary>
/// Marca una entidad para que <see cref="AuditingSaveChangesInterceptor"/> rellene sus timestamps
/// automáticamente. Solo expone getters a propósito: el interceptor escribe a través del
/// <c>ChangeTracker</c> (que sí puede asignar propiedades con setter privado), no hace falta un
/// setter público que rompa la encapsulación del agregado — ver ADR 0011.
/// </summary>
public interface IAuditable
{
    DateTimeOffset CreatedAtUtc { get; }

    DateTimeOffset? UpdatedAtUtc { get; }
}
