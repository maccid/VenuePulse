# ADR 0021 — Precio pagado, cascada de cancelación, delay de pago e ingresos en `CheckIn`

## Contexto

Bloque 17 de la ronda 3 (`.ia/TODO.md`): con `EventCancelled` (Bloque 15, ADR 0019) y
`ReleaseConfirmedZoneSlot` (Bloque 16, ADR 0020) ya mergeados a `develop`, falta la pieza que los
conecta con el dinero real de la demo. Hasta este bloque `Ticket` no sabía cuánto se pagó por él
(los ingresos no existían como concepto), la cancelación de un evento no tenía ningún efecto en
`CheckIn`, y el paso "Procesando pago" del timeline era instantáneo.

## Decisión 1 — `Ticket.Price`, fijado en la compra, sin revalidar

`Ticket` gana `Price` (decimal, inmutable, fijado en el constructor). Es el precio efectivo que el
cliente ya vio en el popup de compra (Bloque 14) — así los ingresos no varían retroactivamente si
`Ticketing` recalcula el precio dinámico después de la compra. El cliente lo envía en
`POST /api/tickets` **sin revalidar contra `Ticketing`**: mismo tipo de riesgo aceptado que el
límite de 10 entradas del Bloque 14 — no hay usuario autenticado ni incentivo real a manipularlo en
una demo con pagos mockeados. Migración aditiva `AddTicketPrice` (`decimal(10,2)`, `defaultValue: 0`
para las filas existentes).

## Decisión 2 — `Refunded` no reutiliza `Rejected`; `ProcessingPayment` es un estado real, no solo de la Saga

`TicketStatus` gana dos valores:

- **`Refunded`** (terminal, distinto de `Rejected`): una entrada reembolsada **sí llegó a
  completarse** (estuvo `Confirmed`, alguien la pagó de verdad) antes de que su evento se
  cancelara — semánticamente muy distinto de `Rejected` (la compra nunca llegó a buen término).
  Mezclarlos habría hecho que el agregado de ingresos (Decisión 4) no pudiera distinguir "nunca se
  vendió" de "se vendió y se devolvió", justo la pregunta que un presentador haría en vivo tras
  cancelar un evento.
- **`ProcessingPayment`** (transitorio, entre `AwaitingPayment` y `Confirmed`/`Rejected`): antes de
  este bloque, el `Ticket` en Postgres saltaba directamente de `AwaitingPayment` a `Confirmed`/
  `Rejected` sin ningún reflejo del paso "procesando" que ahora vive en la Saga (Decisión 3). Con
  el delay real, dejar el Ticket en `AwaitingPayment` durante ese tiempo habría permitido que la Api
  aceptase un segundo `POST /pay` sobre la misma entrada — `ProcessingPayment` cierra esa ventana
  reutilizando el mismo guard-clause que ya usa `SubmitPaymentCommandHandler`
  (`Status != AwaitingPayment` → rechazo limpio).

`Confirm()` pasa a exigir `ProcessingPayment` (antes `AwaitingPayment`); `Reject()` acepta
`Pending`/`AwaitingPayment`/`ProcessingPayment` (un rechazo puede llegar en cualquiera de esos tres
puntos: zona llena, timeout esperando pago, pago fallido ya en curso, o cancelación del evento).

## Decisión 3 — Delay de "Procesando pago" dentro del propio `ThenAsync` de la Saga

`TicketPurchaseSaga` publica `TicketProcessingPayment` (ilumina el Ticket/timeline vía el nuevo
`TicketProcessingPaymentConsumer`, mismo patrón que `TicketReadyForPaymentConsumer`) y hace
`await Task.Delay(paymentOptions.Delay, context.CancellationToken)` **dentro de su propio
`ThenAsync`** al recibir `PaymentSubmitted`, antes de publicar `ConfirmZoneSlot`/`ReleaseZoneSlot`.
No bloquea la Api (que ya respondió `202` en `SubmitPaymentCommandHandler` antes de que este
mensaje llegue a la Saga). Configurable vía `Saga:PaymentProcessingDelaySeconds` (mismo patrón que
`SagaTimeoutOptions`/ADR 0010: un `record` inyectado como singleton, `PaymentProcessingOptions`),
con default `3` en `appsettings.json` y `0` en tests (unitarios e integración) para no añadir
flakiness al arnés ya documentado en `.ia/KNOWN-ISSUES.md`.

**Alternativa descartada**: mover el delay a un `BackgroundService`/watchdog como el de ADR 0010.
Descartado porque aquí no hay nada que vigilar ni ningún camino alternativo que dispare por
timeout — es un `await` secuencial en un único flujo ya en curso, no una espera de un evento externo
que podría no llegar nunca. Un mecanismo de esa complejidad sería ceremonia sin beneficio real.

## Decisión 4 — Cascada de cancelación: `EventCancelledConsumer` solo publica, nunca muta

`EventCancelledConsumer` (nuevo, `CheckIn.Infrastructure.Consumers.EventCancellationConsumers.cs`)
recorre `ITicketRepository.GetByEventIdAsync(eventId)` y, según el estado de cada entrada, publica
el mensaje que corresponde — **nunca muta el `Ticket` directamente**:

- `Confirmed` → publica `TicketRefunded` (nuevo) + `ReleaseConfirmedZoneSlot` (ADR 0020, hacia
  `AccessControl`, ticket a ticket).
- `Pending`/`AwaitingPayment` → publica `TicketPurchaseCancelled` (nuevo), correlacionado por
  `CorrelationId` con la propia `TicketPurchaseSaga`, que lo trata como una compensación más:
  - En `Reserving`: rechaza sin liberar aforo (el aforo nunca llegó a reservarse) — misma forma que
    la rama `TimedOut` ya existente ahí.
  - En `AwaitingPayment`: publica `ReleaseZoneSlot` y transiciona a `Confirming`, que ya resuelve el
    rechazo final al ver `SlotReleased` — reutiliza el mismo camino que `TimedOut` en ese estado,
    sin añadir una rama de resolución nueva.
- `ProcessingPayment` queda **fuera a propósito**: el pago ya está en curso resolviéndose solo: no
  tiene sentido cancelar una compra cuyo resultado (tarjeta/bizum) ya se decidió. Ver riesgo
  aceptado más abajo.

La mutación real (`Ticket.Refund()`) la hace `TicketRefundedConsumer`, un consumer de resultado más
con el mismo patrón que `TicketPurchaseConfirmedConsumer`/`TicketPurchaseRejectedConsumer` — separar
"decidir qué corresponde" (el consumer de cascada) de "aplicarlo" (los consumers de resultado) evita
duplicar la lógica de transición de estado en dos sitios.

**Naturalmente idempotente, sin Inbox nuevo**: tras la primera pasada ya no quedan entradas en
`Confirmed`/`Pending`/`AwaitingPayment` para ese evento (pasaron a `Refunded`/`Rejected`), así que
una redelivery de `EventCancelled` no encuentra nada que reenviar — mismo argumento que ya usa
`AccessControl` para sus propios consumers de cancelación (ADR 0020).

## Decisión 5 — `Ignore(Cancelled)` en `Confirming`

Ventana de carrera real, no hipotética: si `EventCancelled` llega justo cuando la Saga ya transicionó
a `Confirming` (pago ya enviado por el cliente), el `Ticket` en Postgres puede seguir en
`AwaitingPayment`/`ProcessingPayment` un instante — la consulta de `EventCancelledConsumer` puede
verlo así y publicarle un `TicketPurchaseCancelled` que, para cuando llega a la Saga, ya no tiene
rama declarada en `Confirming`. Sin manejarlo, MassTransit lanza `UnhandledEventException` y el
mensaje acaba en la cola de error tras reintentos — no rompe la demo, pero ensucia el broker sin
necesidad. Se añade `Ignore(Cancelled)` en `During(Confirming, ...)`: el pago ya en curso resuelve
solo (confirmado o rechazado), sin relación con la cancelación que llegó tarde.

## Decisión 6 — Ingresos: agregado sobre `Ticket.Price`, nunca una tabla nueva

`GET /api/tickets/events/{eventId}/revenue` (nuevo, `GetEventRevenueQuery` + `EventRevenueDto`)
suma `Price` agrupado en dos cubos: `Confirmed`+`CheckedIn` (confirmados) y
`AwaitingPayment`+`ProcessingPayment` (pendientes). `Refunded`/`Rejected` quedan fuera **por
construcción** — un reembolso baja los ingresos solo por dejar de sumar, sin necesidad de restar
nada explícitamente. Reutiliza `ITicketRepository.GetByEventIdAsync` (mismo método que la cascada de
cancelación); sin tabla ni proyección de "revenue" nueva — el dinero sigue viviendo solo en
`Ticket.Price` (sección 5 del plan de ronda, nunca en Mongo, ADR 0005).

## Alternativas descartadas

- **Revalidar `Price` contra `Ticketing` al comprar**: añadiría una llamada síncrona entre
  `CheckIn` y `Ticketing` que hoy no existe (solo se comunican por bus) — sobreingeniería para una
  demo sin usuarios reales ni incentivo a manipular el precio.
- **`EventCancelledConsumer` mutando el `Ticket` directamente** en vez de publicar `TicketRefunded`:
  descartado por duplicar la lógica de transición que ya tienen los consumers de resultado, y por
  perder el punto de extensión (otros servicios podrían querer reaccionar a `TicketRefunded` sin
  acoplarse a `EventCancelled`).
- **Watchdog/`BackgroundService` para el delay de pago** (ver Decisión 3): descartado, no hay nada
  que vigilar.
- **Incluir `ProcessingPayment` en la cascada de cancelación**: descartado — el pago ya en curso no
  tiene una compensación limpia sin arriesgar una carrera peor (¿qué pasa si `SlotConfirmed` llega
  justo después de cancelar? Ver Decisión 5, riesgo aceptado en vez de resuelto).

## Consecuencias / riesgo aceptado

- Ventana de carrera documentada (Decisión 5): una entrada cuyo pago ya estaba en curso cuando el
  evento se cancela **no se cancela** — se resuelve como si el evento no se hubiera cancelado
  (confirmada o rechazada por su propio resultado de pago). Mismo tipo de trade-off ya aceptado en
  ADR 0010 para el timeout de reserva: cubre el caso relevante para una demo, no una garantía
  formal de todos los casos.
- `Price` lo fija el cliente sin revalidar (Decisión 1) — aceptable sin usuarios autenticados ni
  pasarela de pago real.
- Migración `AddTicketPrice` aditiva, no rompe datos existentes.
- Tests: `TicketTests` (precio positivo, `ProcessingPayment`/`Refunded`, guards de `Confirm`/
  `Reject`/`Refund`), `TicketPurchaseSagaTests` (cancelación en `Reserving`/`AwaitingPayment`,
  `TicketProcessingPayment` publicado antes de resolver, delay a cero en el arnés in-memory),
  `TicketPurchaseFlowTests` (cascada de cancelación de extremo a extremo con infraestructura real:
  una entrada confirmada se reembolsa, una pendiente se rechaza con `EventCancelled`).
