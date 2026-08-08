# ADR 0011 — Guard clauses, paginación genérica e interceptor de auditoría en `VenuePulse.Core`

## Contexto

`VenuePulse.Core` es la única dependencia compartida entre los 3 servicios (`Ticketing`,
`AccessControl`, `CheckIn`). Tres invariantes de constructor idénticas estaban copiadas a mano en
`Ticket`, `Event` y `Zone` (`if (string.IsNullOrWhiteSpace(x)) throw ...`), no había forma
reutilizable de paginar una consulta, y el único timestamp de auditoría (`Ticket.CreatedAtUtc`)
se asignaba a mano en el constructor — inspiración de alto nivel de patrones de `olix.workspace`
(nunca código ni nombres literales, ver `.ia/PRODUCT-VISION.md`).

## Decisión

Tres capacidades nuevas en `Core`, cada una con un consumidor real (no solo "por si acaso"):

1. **`Guard`** (`Core.Domain`): helpers estáticos (`AgainstNullOrWhiteSpace`, `AgainstNonPositive`)
   que lanzan `ArgumentException`, igual que el código que sustituyen. Adoptado en los 3
   dominios (`Ticket`, `Event`, `Zone`) — cambio mecánico, sin tocar Infrastructure ni migraciones.
2. **`PagedResult<T>`** (`Core.Domain`, sin dependencias de infraestructura) +
   **`IQueryable<T>.ToPagedResultAsync()`** (`Core.Data`, con EF Core — separado de `Domain` por
   el mismo motivo por el que `Messaging` ya está separado de `Domain` en este proyecto).
3. **`IAuditable` + `AuditingSaveChangesInterceptor`** (`Core.Data`): en `SavingChanges`, recorre
   `ChangeTracker.Entries<IAuditable>()` y escribe `CreatedAtUtc`/`UpdatedAtUtc` a través del
   `PropertyEntry` (que sí puede asignar un setter privado), sin exigir un setter público que
   rompa la encapsulación del agregado.

## Por qué no se mezcla con `Result`

`Result` ya existe en `Core.Domain` para fallos de negocio **esperables** dentro de un flujo
(`CheckInCommand` devuelve `Result` porque intentar hacer check-in de una entrada ya usada es un
camino normal de la aplicación). `Guard` protege invariantes de **entrada al agregado**: un
`Ticket` sin titular no es un caso de negocio a manejar, es un error de programación/entrada
inválida — de ahí que siga lanzando excepción en vez de devolver `Result.Failure`, igual que el
código original que sustituye. Mezclarlos habría sido forzar un único mecanismo de error donde
ya había dos categorías distintas y bien resueltas.

## Alcance de esta ronda: solo `CheckIn`

`Guard` se adoptó en los 3 dominios porque es un cambio de una línea por invariante, sin tocar
`Infrastructure` ni migraciones. **Paginación y auditoría, en cambio, solo se aplican en
`CheckIn`** (test dedicado con el proveedor InMemory de EF Core sobre `CheckInDbContext`, más la
migración `AddTicketUpdatedAtUtc`), a propósito:

- `feature/simulation-engine` (Bloque 8, en paralelo) está tocando activamente `Ticketing` y
  `AccessControl` (`EventRepository`, `ZonesController`, UI de `venue-ops`). Añadir ahí mismo una
  migración de auditoría o cambiar la forma de una respuesta paginada mientras esa rama está
  viva es el tipo de colisión que el reparto en oleadas de `.ia/TODO.md` intenta evitar.
- El endpoint `GET /api/tickets?holder=` (`GetMyTicketsQuery`) ya lo consumen `shell` y
  `venue-ops` en real — envolver su respuesta en `PagedResult<T>` habría sido un cambio de
  contrato de API con riesgo de romper esas dos UIs sin necesidad real para esta ronda.

`Event`/`Zone` son los candidatos obvios para adoptar `CreatedAtUtc`/`UpdatedAtUtc` y paginación
en cuanto el Bloque 8 mergee a `develop` y deje de haber dos ramas escribiendo a la vez sobre
`Ticketing`/`AccessControl`.
