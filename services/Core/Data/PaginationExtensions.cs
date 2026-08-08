namespace VenuePulse.Core.Data;

using Microsoft.EntityFrameworkCore;
using VenuePulse.Core.Domain;

/// <summary>
/// Depende de EF Core, a diferencia de <see cref="PagedResult{T}"/> (que vive en
/// <c>Core.Domain</c> sin dependencias de infraestructura) — mismo motivo por el que
/// `Messaging` está separado de `Domain` en este proyecto. Ver ADR 0011.
/// </summary>
public static class PaginationExtensions
{
    public static async Task<PagedResult<T>> ToPagedResultAsync<T>(
        this IQueryable<T> query, int pageNumber, int pageSize, CancellationToken cancellationToken)
    {
        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<T>(items, pageNumber, pageSize, totalCount);
    }
}
