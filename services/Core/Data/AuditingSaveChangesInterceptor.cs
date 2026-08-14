namespace VenuePulse.Core.Data;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

/// <summary>
/// Sustituye el `CreatedAtUtc = DateTimeOffset.UtcNow` copiado a mano en cada constructor de
/// agregado (antes solo en `Ticket`) por un único punto de verdad en el momento de guardar — ver
/// ADR 0011. Sin estado propio salvo el reloj: se puede instanciar directamente en `AddDbContext`,
/// no hace falta registrarlo en el contenedor de DI. <see cref="TimeProvider"/> en vez de
/// `DateTimeOffset.UtcNow` estático para poder fijar un reloj falso en tests.
/// </summary>
public sealed class AuditingSaveChangesInterceptor(TimeProvider timeProvider) : SaveChangesInterceptor
{
    public AuditingSaveChangesInterceptor() : this(TimeProvider.System)
    {
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ApplyAudit(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        ApplyAudit(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void ApplyAudit(DbContext? context)
    {
        if (context is null)
            return;

        var nowUtc = timeProvider.GetUtcNow();

        foreach (var entry in context.ChangeTracker.Entries<IAuditable>())
        {
            if (entry.State == EntityState.Added)
                entry.Property(nameof(IAuditable.CreatedAtUtc)).CurrentValue = nowUtc;

            if (entry.State is EntityState.Added or EntityState.Modified)
                entry.Property(nameof(IAuditable.UpdatedAtUtc)).CurrentValue = nowUtc;
        }
    }
}
