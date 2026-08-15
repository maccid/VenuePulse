# ADR 0034 — Cancelación voluntaria de un pedido por el propio comprador

## Contexto

Hasta ahora la única forma de liberar el aforo de un pedido sin pagar era esperar el timeout del
watchdog (ADR 0010, 60s por defecto) — si un fan se arrepiente o simplemente cambia de opinión
mientras está en `Reserving`/`AwaitingPayment`, no tiene forma de liberar ese aforo antes de que
expire solo, bloqueando plazas reales (especialmente sensible en zonas pequeñas como "Grada VIP
Norte", 8 plazas) que otro comprador podría estar esperando.

El mecanismo de compensación para una entrada todavía sin resolver ya existe: la cascada de
cancelación de evento (ADR 0021) publica `TicketPurchaseCancelled` por cada `Ticket`
`Pending`/`AwaitingPayment`, y `TicketPurchaseSaga` ya sabe reaccionar a eso — rechaza
directamente si el aforo no llegó a reservarse, o libera (`ReleaseZoneSlot`) si ya estaba
reservado. El problema es que ese mensaje no lleva ningún motivo: `TicketPurchaseSaga` fija
`RejectionReason = "EventCancelled"` como literal hardcodeado al recibirlo, así que reutilizarlo
tal cual para una cancelación voluntaria del comprador mostraría un motivo falso ("el evento se
canceló") cuando en realidad el evento sigue en venta y fue el propio comprador quien canceló su
pedido.

## Decisión

Añadir un campo `Reason` a `TicketPurchaseCancelled` (con valor por defecto `"EventCancelled"`
para no romper ningún call-site existente) y que `TicketPurchaseSaga` use
`context.Message.Reason` en vez del literal hardcodeado en sus dos ramas `When(Cancelled)`
(`Reserving` y `AwaitingPayment`). La cascada de evento (`EventCancelledConsumer`) sigue
publicando el mensaje sin este parámetro, conservando su motivo de siempre.

Un nuevo `CancelOrderCommand` (`POST /api/orders/{orderId}/cancel`) es el segundo origen de este
mismo mensaje: valida que el `PurchaseOrder` esté en `Reserving`/`AwaitingPayment` (cualquier otro
estado ya no es cancelable — el pago ya se envió o el pedido ya terminó) y publica
`TicketPurchaseCancelled(ticketId, "CancelledByCustomer")` para cada línea todavía sin resolver.
No muta ningún `Ticket` ni el propio `PurchaseOrder` directamente — mismo reparto de
responsabilidades que `EventCancelledConsumer`: el comando solo dispara, la
`TicketPurchaseSaga`/`PurchaseOrderSaga` ya existentes hacen la compensación real y llevan el
pedido a `Rejected` con el motivo correcto, sin Saga ni mecanismo de compensación nuevo.

## Alternativas descartadas

- **Evento nuevo (`TicketPurchaseCancelledByCustomer`) en vez de extender el existente**: hubiera
  evitado tocar `TicketPurchaseSaga`, pero habría duplicado dos ramas `When(...)` casi idénticas
  a las de `Cancelled` (mismo motivo/rechazar-o-liberar) solo para llevar un texto distinto.
  Descartado por la ceremonia sin beneficio real, mismo criterio que ADR 0003/0006.
- **Mutar `PurchaseOrder` a `Rejected` directamente desde `CancelOrderCommandHandler`**: daría
  feedback inmediato sin esperar a la Saga, pero duplicaría la mutación con
  `PurchaseOrderRejectedConsumer` (que también llama a `order.Reject(...)` cuando la Saga
  confirma que todas las líneas se cancelaron) — la segunda llamada lanzaría
  `InvalidOperationException` (`Reject` no es válido dos veces). Descartado por romper la
  garantía de que solo la Saga decide cuándo el pedido está de verdad resuelto.
- **Permitir cancelar en `Confirming`**: en ese estado el pago ya se envió — cancelar ahí sería un
  reembolso, no un arrepentimiento previo al pago, y esa distinción ya existe en el dominio
  (`TicketRefunded` vs. `TicketPurchaseRejected`, ver ADR 0021). Fuera de alcance de este ADR.

## Consecuencias

- El fan puede liberar su propia reserva sin esperar el timeout, reduciendo el bloqueo real de
  aforo en zonas pequeñas.
- `RejectionReason` a nivel de ticket individual ahora puede ser `"EventCancelled"` o
  `"CancelledByCustomer"` según el origen — sin cambios en `OrderStatusDto.RejectionReason` (el
  motivo a nivel de pedido, que ya es el único que muestra el frontend, ver ADR 0028
  "Consecuencias").
- Riesgo residual aceptado, mismo tipo que el resto de compensaciones de esta Saga: si la
  cancelación llega justo cuando una línea hermana ya pagó (`Confirming`), el camino existente de
  `PurchaseOrderSaga.AwaitingPayment.When(TicketRejected)` (rama "alreadyPaid") ya compensa las
  líneas confirmadas — no es un caso nuevo, ya estaba cubierto por la ventana de carrera
  equivalente a ADR 0027 a nivel de pedido.
