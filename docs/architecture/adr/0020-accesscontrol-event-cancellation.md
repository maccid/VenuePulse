# ADR 0020 — Cancelación de evento en AccessControl (cierre de zonas)

## Contexto

Bloque 16, sub-fase B de la ronda 3 (`.ia/TODO.md`): con la sub-fase A ya mergeada, `AccessControl`
aprovisiona un set de zonas realista por tipo de evento (4 en partido / 5 en concierto). Desde el
Bloque 15 (ADR 0019) existe `POST /events/{id}/cancel` en `Ticketing`, que publica `EventCancelled`
— contrato nuevo que **no tenía consumer** hasta este bloque. Además, el reembolso de entradas
confirmadas (Bloque 17, `CheckIn`) necesita liberar aforo YA confirmado en las zonas, algo que
`ReleaseZoneSlot` no hace: eso decrementa `Reserved` (reserva en curso), no `ConfirmedSold`.

Dos necesidades nuevas, ambas en el agregado `Zone`:

- **Cerrar/bloquear las zonas del evento cancelado**: `EventCancelled` debe hacer que una zona
  cancelada rechace reservas nuevas (como si estuviera llena).
- **Liberar una entrada YA confirmada** (no una reserva): la compensación por cancelación del
  evento reembolsa entradas ya vendidas, así que `ConfirmedSold` tiene que poder bajar — antes de
  este bloque solo subía (reserva → confirmación → check-in).

## Decisión 1 — `Zone.Cancelled` (flag) + `Cancel()`

`Zone` gana un flag `Cancelled` (persistido, columna `Cancelled` en `zones`). `Cancel()` solo
fija el flag y es **idempotente a propósito**: la redelivery de `EventCancelled` (Inbox genérico,
ADR 0015) o una llamada repetida no fallan ni cambian nada.

`Cancel()` guarda la única invariante relevante: lanza si `CheckedIn > 0`. No obstante, este caso
es inalcanzable por producto: `Event.Cancel()` solo se permite mientras el evento está `OnSale`
(ADR 0019) y el check-in solo ocurre el día del evento tras cerrar la venta, así que un evento
cancelado nunca puede tener gente dentro. La guarda es defensa en profundidad, no un flujo
esperado. `Cancel()` **no toca los contadores**: `Reserved`/`ConfirmedSold` los liberan
respectivamente la rama de cancelación de la Saga (`ReleaseZoneSlot`, ya existente) y
`ReleaseConfirmedSlot` (Decisión 2).

## Decisión 2 — `ReleaseConfirmedZoneSlot`: nuevo comando, decrementa `ConfirmedSold`

Nuevo contrato `ReleaseConfirmedZoneSlot(CorrelationId, ZoneId)` en `Core.Messaging.Contracts`
(frente a `ReleaseZoneSlot`, que libera una reserva). Simétrico en forma pero **semánticamente
distinto**: `ReleaseConfirmedSlot()` decrementa `ConfirmedSold` (una entrada que YA se vendió y se
reembolsa), nunca `Reserved`. Antes de este comando `ConfirmedSold` solo subía; sin él, el
reembolso de `CheckIn` (Bloque 17) no tendría forma de devolver aforo vendido.

El flujo queda **ticket a ticket**: `CheckIn` publica un `ReleaseConfirmedZoneSlot` por entrada
reembolsada (su Bloque 17), y `AccessControl` los consume uno a uno. `AccessControl` no publica ni
deriva cantidades por su cuenta — es el destinatario del contrato, no su origen.

## Decisión 3 — Rechazo de reserva en zona cancelada

`TryReserveSlotAsync` rechaza la reserva si `zone.Cancelled`, con el mismo motivo de fallo que una
zona llena (`ZoneFull`). El motivo es un pelo impreciso ("la zona está llena" cuando en realidad
está cancelada), pero la Saga solo necesita saber que no se pudo reservar para rechazar la compra —
no merece un tipo de fallo nuevo en el contrato de la Saga. Se documenta esta imprecisión en el
código, no se propaga un tercer motivo.

## Decisión 4 — Consumo y notificación

Dos consumers en `Infrastructure.Consumers`:

- `EventCancelledConsumer` → `CancelEventZonesAsync(eventId)`: recorre las zonas del evento, las
  cancela y notifica `occupancy-changed` por cada una (SignalR, para que el panel en vivo las
  muestre canceladas).
- `ReleaseConfirmedZoneSlotConsumer` → `ReleaseConfirmedSlotAsync(zoneId)`: decrementa y notifica.

Ambos quedan cubiertos gratis por el Inbox genérico (`InboxDeduplicationFilter<>`, ADR 0015): la
redelivery no re-aplica el efecto.

`ZoneOccupancyDto` gana `Cancelled` (aditivo) para que la cancelación sea observable vía
API/SignalR; el frontend la refleja cuando convenga (Bloques 18/19).

## Alternativas descartadas

- **`ReleaseConfirmedZoneSlot` reutilizando `ReleaseZoneSlot`**: descartado — tocaría `Reserved`
  (que ya es 0 al confirmar) y no liberaría `ConfirmedSold`, que es justo lo que el reembolso
  necesita. Se añadió un contrato nuevo y explícito en vez de forzar una semántica ajena.
- **Tipo de fallo nuevo (`ZoneCancelled`) en el rechazo de reserva**: descartado — la Saga no lo
  distingue de `ZoneFull` (ambos → rechazo de la compra) y añade ruido al contrato.
- **Cancelar el evento poniendo los contadores a cero en AccessControl**: descartado — el aforo
  confirmado se libera en sincronía con los reembolsos de `CheckIn` (uno por uno); vaciar aquí
  desincronizaría el panel de ocupación respecto a las entradas realmente reembolsadas.
- **Dejar la cancelación solo en el flag sin rechazar reservas**: descartado — un evento cancelado
  que siguiera vendiendo entradas confirmadas rompería el invariante de producto (el reembolso
  asume que nada se vendió tras cancelar).

## Consecuencias / riesgo aceptado

- Migración aditiva `AddZoneCancelled` (`Cancelled` con `default: false`), no rompe datos
  existentes.
- `ReleaseConfirmedZoneSlot` lo publica `CheckIn` (Bloque 17, rama en paralelo de Claude Code) y
  lo consume `AccessControl` (este bloque). Hasta que Bloque 17 mergee, el consumer queda vivo pero
  sin emisores — aceptable durante la Oleada 2, mismo razonamiento que en ADR 0019 con
  `EventCancelled`.
- `ZoneOccupancyDto.Cancelled` es aditivo y opcional para el frontend (JS tolera campos extra).
- Tests: `ZoneTests` (flag idempotente, guarda con gente dentro, `ReleaseConfirmedSlot`),
  `ZoneServiceTests` (cancelación de todas las zonas + notificaciones, rechazo de reserva en zona
  cancelada, liberación de confirmada + notificación). AccessControl 46/46, resto del solution sin
  regresión (Ticketing 33/33, CheckIn 15/15, Architecture 7/7).
