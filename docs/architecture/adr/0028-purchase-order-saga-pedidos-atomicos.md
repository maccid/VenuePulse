# ADR 0028 — `PurchaseOrderSaga`: pedidos multi-entrada atómicos

## Contexto

Comprar N entradas en `ticket-checkout` dispara hoy N `POST /api/checkin/api/tickets`
independientes en paralelo (`useCheckoutCart.ts::reservar()`, `Promise.all` sin `try/catch`), cada
uno con su propia `TicketPurchaseSaga` totalmente aislada de las demás. Esto produce dos problemas
reales:

1. **Sin atomicidad de conjunto**: si de 10 entradas 1 falla al reservar aforo, hoy el comprador se
   queda con 9 — no hay ninguna garantía de todo-o-nada. Verificado como el patrón real en
   aerolíneas (PNR — *Passenger Name Record*, donde el pedido agrupa varios asientos reservados
   individualmente pero es la unidad atómica de pago/confirmación) y en vendedores de entradas
   como Ticketmaster (carrito con "holds" individuales por asiento, todo-o-nada al confirmar).
2. **Pérdida de entradas y aforo liberado sin causa aparente**: si UNA sola de las N peticiones de
   red falla con una excepción (no solo `!response.ok`), el `Promise.all` que las envuelve se
   rechaza sin capturarse — el frontend pierde la referencia a TODOS los tickets del lote,
   incluidos los que sí se crearon con éxito en el backend. Esos tickets huérfanos nunca reciben
   `/pay`, expiran solos por el watchdog de timeout (ADR 0010/0027) y liberan el aforo que tenían
   reservado. Causa raíz secundaria agravante, ya señalada como "candidato a un ADR propio" en el
   propio ADR 0027 sin resolver hasta ahora: `ZoneService.TryReserveSlotAsync` no tiene ningún
   token de concurrencia (`RowVersion`) — con varias reservas simultáneas sobre la misma `Zone` hay
   una ventana de carrera real.

Se añade además un requisito nuevo, explícito del producto: el **precio** también debe formar
parte de la garantía todo-o-nada — si el precio efectivo de cualquier línea ya no coincide con el
que el comprador aceptó al reservar, el pedido completo se rechaza, no solo esa línea (mismo
criterio que una tarifa de vuelo "ya no disponible" al confirmar la reserva).

Este ADR **reclama el número 0028**, reservado en `.ia/TODO.md` para el Bloque 24 ("GUID de grupo
para compras múltiples") — nunca llegó a escribirse ningún documento con ese número. El `GroupId`
que introduce la `PurchaseOrderSaga` de este ADR **absorbe y sustituye** el alcance de ese bloque:
es el mismo identificador que agrupará varias entradas para su escaneo conjunto en `venue-ops`/
check-in, no dos mecanismos distintos.

## Alternativas descartadas

- **Fusionar todo en una sola Saga con N líneas internas** (en vez de una Saga orquestadora nueva
  por encima de la `TicketPurchaseSaga` existente): obligaría a tocar los contratos de mensajería
  que hoy son 1 mensaje = 1 ticket (añadiendo un identificador de línea a cada uno) y a reescribir
  una máquina de estados ya probada, con varias rondas de fixes de carrera reales detrás (ADR
  0010/0018/0021/0025/0027). Más riesgo de romper un camino que ya funciona, por un beneficio
  (un solo mecanismo en vez de dos capas) que no compensa. Descartada con el usuario.
- **Tabla `PurchaseOrder` en Postgres con sus propias líneas** (`OwnsMany`, igual que
  `Event.TicketTypes`): descartada — copiaría `TicketTypeId`/`ZoneId`/precio que ya vive en cada
  `Ticket.GroupId`, con riesgo real de divergencia entre las dos copias. Las líneas de un pedido se
  derivan agrupando los propios `Ticket` por `GroupId` cuando hace falta consultarlas.
- **Proyección asíncrona en `CheckIn` del precio efectivo de `Ticketing`** (mismo patrón que ADR
  0016, donde `Ticketing` se suscribe a la ocupación de `AccessControl`): descartada para este
  caso concreto — la validación de precio es una lectura puntual en un único instante (el momento
  de reservar el pedido), no un dato que se necesite mantener sincronizado en todo momento. Montar
  un consumer + tabla de proyección nueva solo para responder "¿cuál es el precio ahora?" una vez
  por pedido sería la sobreingeniería que el proyecto evita en otros sitios (ADR 0006).
- **Segundo watchdog/mensaje de compensación nuevo para el pedido**: descartado — se reutilizan
  `TicketPurchaseCancelled` (ya manejado por `TicketPurchaseSaga` en `Reserving`/`AwaitingPayment`)
  y `ReleaseConfirmedZoneSlot`+`TicketRefunded` (ya existentes desde la cascada de cancelación de
  evento, Bloque 17/ADR 0020) para compensar líneas hermanas — no hace falta ningún mecanismo de
  compensación nuevo.

## Decisión

1. **`PurchaseOrderSaga` nueva** (`services/CheckIn/Infrastructure/Sagas/`), correlacionada por
   `GroupId`, orquestadora por encima de N `TicketPurchaseSaga` sin tocar su lógica interna (solo
   se le añade `GroupId` como campo de propagación en sus mensajes ya existentes, como parámetro
   opcional al final — compatible con todos los call-sites actuales). Estados:
   `ReservingLines → AwaitingPayment → ConfirmingLines`, con `CancellingLines` como rama de
   compensación temprana. Si cualquier línea falla al reservar, cancela las hermanas
   (`TicketPurchaseCancelled`) y rechaza el pedido completo. Si cualquier línea falla al pagar,
   compensa (`ReleaseConfirmedZoneSlot`+`TicketRefunded`) las hermanas ya confirmadas y rechaza el
   pedido completo. Expone un único paso de pago (`POST /api/orders/{id}/pay`) que reenvía
   `TicketPaymentSubmitted` a cada Saga hija.
2. **Validación de precio síncrona**: al recibir `PlaceOrderCommand`, `CheckIn` hace una llamada
   HTTP **síncrona** a `GET /api/events/{eventId}/ticket-types` de `Ticketing` (endpoint ya
   existente) para comprobar que el precio de cada línea coincide con el `EffectivePrice` vigente
   en ese instante. Si cualquiera no coincide, se rechaza el pedido completo (`"PriceChanged"`)
   **sin** crear ningún `Ticket` ni publicar ningún mensaje. Es la **primera llamada HTTP síncrona
   entre dos backends del proyecto** (hasta ahora toda comunicación entre servicios era asíncrona
   por bus) — justificada porque es una lectura sin efectos secundarios (`GET`, idempotente) en un
   único instante puntual, no un flujo continuo que necesite desacoplarse. El precio se fija una
   sola vez por línea, igual para todas sus unidades, aunque la propia reserva del pedido cruce un
   escalón de precio dinámico a mitad de camino (nunca "2 unidades a un precio, 1 a otro" dentro
   del mismo pedido).
3. **Aforo insuficiente en cualquier línea → rechazo del pedido completo**, mismo mecanismo de
   compensación que el punto 1 (no se entrega un pedido parcial).
4. **`PurchaseOrderSagaState` en MongoDB**, igual que `TicketPurchaseSagaState` (ADR 0005) — sin
   tabla de líneas propia en Postgres (ver alternativas descartadas). Lo único permanente en
   Postgres es `Ticket.GroupId` (columna nueva, migración aditiva) y el agregado `PurchaseOrder`
   (solo cabecera: `EventId`, `Holder`, `Status`, `RejectionReason` — sin líneas).
5. **Concurrencia en `AccessControl.Zone`**: se añade `RowVersion` (shadow property vía Fluent
   API, sin ensuciar el dominio) y reintento (`ExecuteWithRetryAsync`, máx. 3 intentos) ante
   `DbUpdateConcurrencyException` en `ZoneService` — cierra el gap que el propio ADR 0027 dejó
   explícitamente abierto como "candidato a un ADR propio".
6. **UI a nivel de pedido**: `ticket-checkout` deja de mostrar solo el estado por-ticket y añade un
   estado/motivo agregado de pedido (`OrderStatusDto`, notificado por SignalR reutilizando el hub
   `/hubs/tickets` ya existente con un evento nuevo `orderStatusChanged` — sin hub nuevo).

## Consecuencias / riesgo aceptado

- `api-checkin` pasa a depender en tiempo real de que `api-ticketing` esté vivo para poder crear un
  pedido (antes, la compra no dependía de que ningún otro servicio respondiera de inmediato) —
  nuevo edge `depends_on: api-ticketing: condition: service_healthy` en `docker-compose.yml`. Esto
  matiza la afirmación de `README.md` de que "los servicios no se llaman por HTTP entre sí": sigue
  siendo cierto para las escrituras/comandos (siempre por bus), pero deja de ser cierto sin
  excepción para las lecturas — se documenta aquí como la primera y, por ahora, única excepción.
- **Ambigüedad de motivo aceptada y no visible para el usuario**: cuando la Saga orquestadora
  cancela una línea hermana reutilizando `TicketPurchaseCancelled`, el `Ticket` hermano queda con
  `RejectionReason = "EventCancelled"` (literal ya hardcodeado dentro de
  `TicketPurchaseSaga.When(Cancelled)`, que no se toca) aunque el evento no se haya cancelado
  realmente — la causa real (precio/aforo del pedido) solo es correcta a nivel de
  `PurchaseOrder.RejectionReason`. Se acepta porque el frontend deja de mostrar el motivo por-ticket
  individual (decisión 6) y solo muestra el del pedido — el usuario nunca ve el texto ambiguo.
- Bloque 24 (`.ia/TODO.md`) queda **absorbido y cerrado por este ADR**: `Ticket.GroupId` queda listo
  y poblado, pero el endpoint de *validación/check-in por grupo* (escanear `group-<GUID>` en
  `venue-ops`) no se construye en esta ronda — queda como ampliación de seguimiento, mismo criterio
  de scoping incremental que ya usó el proyecto varias veces.
- Riesgo residual en `AccessControl`: si se agotan los 3 reintentos de concurrencia, el
  comportamiento cae al de hoy (el mensaje va a la cola de error de MassTransit) — no se añade
  además `UseMessageRetry` de MassTransit encima para no solapar dos mecanismos de reintento
  distintos sobre el mismo fallo.
