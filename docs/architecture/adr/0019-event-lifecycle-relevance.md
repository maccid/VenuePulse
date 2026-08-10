# ADR 0019 — Ciclo de vida del `Event`, relevancia y alta desde Operaciones

## Contexto

Bloque 15 de la ronda 3 (`.ia/TODO.md`): hasta ahora los eventos solo nacían de seed data —
`Ticketing` no tenía ningún endpoint de escritura para `Event`. Para que Operaciones (`venue-ops`)
pueda dar de alta eventos en vivo durante la demo (pestañas estilo Chrome + botón "+", Bloque 18),
hacía falta un `POST /api/events` real. De paso, aparecieron dos necesidades relacionadas:

- Un evento necesita poder **cerrar su venta** y **cancelarse** (con reembolso, Bloque 17), para
  que el catálogo de fans deje de venderlo cuando ya no procede.
- El usuario pidió un selector genérico de "relevancia" del evento (un derbi, un concierto de un
  artista muy conocido...) que encarezca el precio — nunca nombres reales, ver
  `.ia/PRODUCT-VISION.md`.

Tres decisiones de arquitectura no triviales en el mismo bloque, documentadas juntas porque
comparten el mismo agregado (`Event`).

## Decisión 1 — Ciclo de vida (`EventStatus`)

`Event` gana `Status` (`OnSale` → `SalesClosed` | `Cancelled`, ambas transiciones solo válidas
desde `OnSale`). `Event.CloseSales()`/`Event.Cancel()` lanzan `InvalidOperationException` ante una
transición inválida — mismo estilo que los guard-clauses ya existentes en `Ticket` (CheckIn), no
el patrón `Result` (ADR 0011 reserva `Result` para fallos de negocio esperables en el camino
normal de un flujo orquestado; aquí es una invariante de agregado). La capa de aplicación
(`TicketingService`) sí comprueba el estado antes de llamar al dominio y devuelve
`EventLifecycleResult` (`Success`/`NotFound`/`InvalidState`) para que el controlador mapee a
`204`/`404`/`409` sin depender de capturar la excepción — la excepción del dominio queda como
invariante de última línea, no como mecanismo de control de flujo normal.

**Cancelar solo se permite mientras `OnSale`** (decisión de producto, confirmada explícitamente):
así ninguna entrada afectada puede estar nunca en `CheckedIn`, y el reembolso (Bloque 17,
`CheckIn`) no necesita contemplar ese caso.

**No se sincroniza con `SimulationMode`** (Sales/Arrivals, motor de simulación de `AccessControl`,
ADR 0013): son conceptos deliberadamente distintos — `EventStatus` decide negocio de catálogo
(¿se puede seguir comprando?), `SimulationMode` decide solo qué curva de demo genera el motor
controlable manualmente. Fusionarlos acoplaría un servicio de catálogo con una herramienta de
presentación, el mismo tipo de acoplamiento que ya se rechazó en ADR 0013/0016. El único punto de
contacto vive en el frontend (Bloque 18): al cerrar venta, `venue-ops` cambia el valor por defecto
del selector de fase a `Arrivals` la próxima vez que se abra el modo Ocupación — ninguna llamada
entre `Ticketing` y `AccessControl`.

## Decisión 2 — Relevancia (`EventRelevance`) y orden de los dos multiplicadores de precio

`EventRelevance` (`Standard` ×1.0 / `Special` ×1.5) — constantes fijas, misma heurística
transparente que ya usa `DynamicPricing` (ADR 0016), nunca configurables por evento. Se fija en el
constructor, inmutable después (igual que `Type`).

Con `DynamicPricing.Apply` ya existente (ocupación → tier), aparece un segundo multiplicador de
precio y hay que decidir el orden:

```
AdjustedBasePrice = TicketType.Price × EventRelevance.Multiplier()   // TicketingService
(EffectivePrice, Tier) = DynamicPricing.Apply(AdjustedBasePrice, occupancy%)   // sin tocar
```

La relevancia se aplica **antes y fuera** de `DynamicPricing`, que sigue siendo una función pura
`(precio, ocupación%) → (precio, tier)` sin saber que la relevancia existe — **cero cambios en
`DynamicPricing.cs`**. Así el tier que ve el presentador en directo ("Demanda alta", "Últimas
entradas") sigue significando solo ocupación; nunca se mezcla con "es caro porque el evento es
especial". `TicketTypeDto.Price` sigue siendo el precio de catálogo puro (sin relevancia);
`EffectivePrice` incorpora ambos multiplicadores. Matemáticamente el orden es conmutativo — la
decisión es de dónde vive cada uno, no de resultado numérico.

## Decisión 3 — Alta de evento desde Operaciones (`POST /api/events`)

Primer endpoint de escritura de `Event`. Reutiliza exactamente el patrón Add→Publish→SaveChanges
(Outbox transaccional) que ya usa `PurchaseTicketCommandHandler` en `CheckIn` y el propio seed
data — publica `EventCreated` **sin cambios en el contrato**, así que `AccessControl` no necesita
ningún cambio para reaccionar a un evento creado por la UI en vez de por seed. `VenueName` fijo
server-side (`"Estadio Central"`, único recinto de esta demo) — el formulario no lo pide.

`IEventRepository.AddAsync` deja de guardar internamente (antes hacía Add+SaveChanges en el mismo
método, lo que habría impedido publicar en medio); se separa en `AddAsync` (solo trackea) +
`SaveChangesAsync` — mismo patrón que `ITicketRepository` en CheckIn. `GetByIdAsync` deja de usar
`AsNoTracking()`: los nuevos casos de uso de mutación (`CloseSalesAsync`/`CancelEventAsync`)
necesitan que `SaveChangesAsync` detecte el cambio de estado sobre la misma instancia.

Los tipos de entrada por defecto de cada `EventType` (`DefaultTicketTypeCatalog`, `Ticketing.Domain`)
quedan fijados en este bloque junto con las zonas equivalentes en `AccessControl` (Bloque 16, ver
`.ia/TODO.md`) — 2 tipos para partido (`General`/`VIP`, 4 zonas), 4 para concierto (`Pista VIP`/
`Pista`/`Grada Frontal`/`Grada Lateral`, 5 zonas).

## Alternativas descartadas

- **`Result<Event>` en el dominio para `CloseSales`/`Cancel`**: se descartó a favor de excepción +
  chequeo previo en la capa de aplicación (ver Decisión 1) — mantiene el dominio consistente con
  el resto de agregados (`Ticket`) sin introducir una segunda convención de manejo de errores en
  el mismo servicio.
- **Sincronizar `EventStatus` con `SimulationMode`** (una única "fase" compartida): descartado por
  acoplar catálogo de negocio con motor de demo — ver Decisión 1.
- **Revalidar el precio contra `Ticketing` al cerrar la venta o cancelar**: innecesario, ninguna
  de las dos operaciones toca precios.

## Consecuencias / riesgo aceptado

- `EventCancelled` (nuevo, `Core.Messaging.Contracts.EventLifecycleMessages.cs`, fichero separado
  de `TicketingMessages.cs` para minimizar conflictos de merge con el Bloque 16 en paralelo) no
  tiene todavía consumers en este bloque — los añaden los Bloques 16 (`AccessControl`, cierre de
  zonas) y 17 (`CheckIn`, reembolso). Hasta que esos bloques mergeen, cancelar un evento no tiene
  efecto visible fuera de `Ticketing`; aceptable durante la Oleada 1 de una ronda con trabajo en
  paralelo entre dos IAs (ver `.ia/TODO.md`).
- Migración `AddEventLifecycleAndRelevance` aditiva (dos columnas con `DEFAULT`), no rompe datos
  existentes.
- Tests: `EventTests` (guard-clauses de dominio), `TicketingServiceTests` (alta con tipos de
  entrada por defecto, cierre/cancelación con `EventLifecycleResult`, relevancia aplicada antes de
  la demanda) — con el harness in-memory de MassTransit para `IPublishEndpoint` real, mismo patrón
  que `ZoneOccupancyChangedConsumerTests`. Ticketing 33/33, resto del solution sin regresión
  (CheckIn 15/15, AccessControl 39/39, Architecture 7/7).
