# ADR 0036 — Particionado de PurchaseOrderSaga y por qué su reintento de mensaje era insuficiente

## Contexto

`PurchaseOrderSagaDefinition.ConfigureSaga` ya aplicaba `UsePartitioner(8, ... => sagaContext.Saga.CorrelationId)`
sin ningún ADR que lo explicara. El motivo: `PurchaseOrderSaga` (un documento por pedido, ver ADR
0028) puede recibir varios eventos casi simultáneos de sus líneas hermanas —
`TicketAwaitingPayment`/`TicketPurchaseConfirmed`/`TicketPurchaseRejected`, uno por cada
`TicketPurchaseSaga` individual del pedido — y MassTransit, sin nada que lo evite, puede procesar
esos eventos en paralelo en distintos hilos. Como todos actualizan el MISMO documento Mongo del
pedido, dos escrituras concurrentes chocan (concurrencia optimista nativa de
`MongoDbSagaRepository`, vía `ISagaVersion`). El particionado por `CorrelationId` fuerza que los
eventos de un mismo pedido se encolen y procesen de uno en uno.

El particionado no basta por sí solo. Verificado en real contra el stack (`docker compose`,
Postgres/Mongo/RabbitMQ reales): un pedido de 9 líneas donde varios tickets expiraron casi al
mismo tiempo (`PaymentTimedOut`, publicado por el watchdog en una misma pasada, ver ADR 0010)
generó un `TicketPurchaseRejected` que MassTransit marcó como fallido:

```
MT-Fault-ExceptionType: MassTransit.MongoDbConcurrencyException
MT-Fault-Message: PurchaseOrderSagaState(7a4b37a7-...) Saga exception: Saga Update Failed
MT-Fault-RetryCount: 3
```

Confirmado en la cola `PurchaseOrderSagaState_error` de RabbitMQ tras agotar los 3 reintentos que
aplica `cfg.UseMessageRetry(r => r.Intervals(100, 300, 800))` (configurado globalmente para todo
el bus en `MessagingExtensions.AddMessaging`, ver `.ia/KNOWN-ISSUES.md` sobre esa misma política
para la sub-causa 1 de `TicketPurchaseSaga`). Con varios eventos del mismo pedido encolados en la
misma partición, 3 intentos con backoff corto no dan margen suficiente antes de que el mensaje se
dé por perdido — mismo patrón que ADR 0035 (Zone/xmin), pero aquí Mongo sí detectaba el conflicto
desde el principio; lo insuficiente era el número de reintentos, no la detección.

## Decisión

Subir el número de reintentos específicamente para el endpoint de `PurchaseOrderSagaState`, sin
tocar la política global del bus (que sigue sirviendo bien a los demás consumers/sagas de CheckIn
y Ticketing):

```csharp
endpointConfigurator.UseMessageRetry(r => r.Interval(15, TimeSpan.FromMilliseconds(200)));
```

15 intentos, ya que `PlaceOrderCommandHandler.MaxTicketsPerOrder` limita a 10 líneas por pedido —
10 eventos casi simultáneos en la misma partición es el peor caso real posible, 15 da margen sin
ser arbitrario (mismo criterio que ADR 0035 para `Zone.ExecuteWithRetryAsync`).

## Alternativas descartadas

- **Subir la política global de `UseMessageRetry` en `MessagingExtensions`**: afectaría a todos
  los consumers de CheckIn y Ticketing, no solo a la contención real (pedidos multi-línea). Más
  reintentos por defecto en consumers que no los necesitan solo añade latencia ante fallos reales
  sin ningún beneficio.
- **Más particiones (`UsePartitioner(N)` con N mayor)**: no ayuda — las particiones ya son
  suficientes para no chocar entre pedidos distintos (poco probable que 2 pedidos compartan
  partición y aunque compartieran, no colisionan entre sí); el problema es la cola de eventos
  *dentro* de la misma partición para un único pedido con muchas líneas, que más particiones no
  acortan.
- **Reducir la ventana de expiración simultánea del watchdog** (escalonar los `PaymentTimedOut` de
  las líneas de un mismo pedido): evitaría el pico de contención en este caso concreto, pero no
  cierra la causa general — cualquier camino feliz con muchas líneas confirmando casi a la vez
  tiene el mismo riesgo. Fuera de alcance de este ADR.

## Consecuencias

- Verificado tras el fix: el mismo escenario de contención (pedido multi-línea con varios eventos
  casi simultáneos) ya no genera mensajes en `PurchaseOrderSagaState_error`.
- Riesgo residual aceptado, mismo tipo que el resto del proyecto: si la contención real superara
  las 15 reintentos (más de 10 líneas simultáneas, hoy imposible por `MaxTicketsPerOrder`), el
  mensaje seguiría yendo a la cola de error. No se añade una cola de reconciliación automática —
  desproporcionado para esta demo (ADR 0006).
