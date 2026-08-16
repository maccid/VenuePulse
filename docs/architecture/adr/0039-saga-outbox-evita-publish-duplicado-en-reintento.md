# ADR 0039 — In-memory outbox en las Sagas: evita duplicar `Publish()` al reintentar tras un conflicto de Mongo

## Contexto

Reportado por el usuario probando el flujo real (`ticket-checkout`): pedidos de varias entradas
que se quedaban colgados sin resolver nunca — uno en "reservando" (6 entradas, una zona sin
aforo dispara la cancelación de las 5 hermanas) y otro en "pagando" (8 entradas, 2 líneas
huérfanas en `ProcessingPayment`). Diagnosticado en real contra Seq/Postgres/Mongo (no
Testcontainers), reconstruyendo la traza completa de ambos pedidos.

**Causa raíz confirmada** (pedido de 6 entradas, `traceId` con la secuencia completa en Seq):

1. Una línea falla al reservar (`ZoneFull`) → `PurchaseOrderSaga.When(TicketRejected)` en
   `ReservingLines` publica `TicketPurchaseCancelled` a las 5 líneas hermanas y transiciona.
2. Guardar el documento de la Saga en MongoDB falla (`MongoDbConcurrencyException: Saga Update
   Failed`) — el `UsePartitioner(8, ...)` de ADR 0036 no evita esto: dos eventos `TicketRejected`
   distintos (de dos líneas hermanas) pueden llegar casi a la vez a dos particiones diferentes y
   competir por el mismo documento del pedido.
3. MassTransit reintenta **el mensaje completo** (`cfg.UseMessageRetry`, ADR 0036: 15 intentos /
   200ms en este endpoint). El reintento **re-ejecuta desde cero** el bloque `.Then()/.ThenAsync()`
   de la máquina de estados — incluidos los `Publish()` que el intento anterior **ya había
   enviado de verdad al bus**, aunque su guardado en Mongo no llegara a persistir.
4. Resultado observado en los logs: `"Pedido ... falló — cancelando 5 líneas hermanas"` aparece
   **dos veces**, y `"Línea X cancelada"` se repite para las mismas líneas. Cada duplicado, aguas
   abajo, viola un invariante de dominio distinto que sí se propaga sin reintento y muere en la
   cola de error de RabbitMQ (`AccessControl` no tiene `UseMessageRetry` configurado, ver
   `.ia/KNOWN-ISSUES.md`):
   - `ReserveZoneSlot` → `DbUpdateConcurrencyException` sin capturar (agotó los 15 intentos de
     `ZoneService.ExecuteWithRetryAsync`, ver ADR 0035).
   - `ReleaseZoneSlot` → `"No hay ninguna reserva pendiente que liberar"` (ya se había liberado
     por el duplicado).
   - `TicketPurchaseRejected` → `"Solo una entrada ... se puede rechazar (estado actual:
     Rejected)"` (ya estaba rechazada por el duplicado).
   - `TicketPurchaseSagaState(...)`: `MongoDbConcurrencyException: Saga Delete Failed` — el
     duplicado de `Cancelled` llega a una Saga individual que ya está finalizando por el original.

Una de las 6 líneas nunca recibe su resolución final → el pedido se queda colgado para siempre.

**Efecto secundario confirmado, no una causa nueva**: el mismo mecanismo, en un pedido de 5
entradas posterior sobre la misma zona (`Grada VIP Norte`), hizo que 2 de 5 `ConfirmZoneSlot`
legítimos (sin ningún duplicado esta vez) fallaran con el mismo `"No hay ninguna reserva
pendiente que confirmar"`. Reconstruyendo la aritmética de `Zone.Reserved`/`ConfirmedSold` con los
datos reales de Postgres, la zona ya arrastraba **2 unidades "reservadas" fantasma** de tickets
huérfanos de incidentes *anteriores* de este mismo bug (`ReleaseZoneSlot`/`ConfirmZoneSlot` que
nunca resolvieron) — no hace falta un mecanismo de fallo nuevo para explicarlo, es la corrupción
de contador ya causada por el punto 3 manifestándose en el siguiente pedido que agota el margen
que quedaba. Ver nota en `.ia/KNOWN-ISSUES.md`.

## Decisión

Envolver el consumo de ambas Sagas (`PurchaseOrderSaga` y, nueva, `TicketPurchaseSaga`) con el
patrón **in-memory outbox** de MassTransit (`endpointConfigurator.UseInMemoryOutbox(context)`,
después de `UseMessageRetry` para que cada intento de reintento tenga su propio outbox limpio):
todos los `Send()`/`Publish()` que ocurren durante el procesamiento de un mensaje se retienen en
memoria y solo se envían de verdad al bus **si todo el pipeline termina sin excepción**,
incluyendo el guardado del estado de la Saga en Mongo. Si el guardado falla y MassTransit
reintenta el mensaje, el intento fallido nunca llegó a enviar nada real — el reintento genera su
propio conjunto de efectos, sin duplicar los del intento anterior.

`TicketPurchaseSagaDefinition` (nueva clase, antes `TicketPurchaseSaga` se registraba con el
overload sin definición) añade el mismo trío que ya tenía `PurchaseOrderSagaDefinition` (ADR
0036): `UsePartitioner(8, saga => saga.CorrelationId)` (evita procesar en paralelo dos eventos del
mismo ticket — la traza mostró exactamente esa carrera con `Saga Delete Failed`),
`UseMessageRetry(r => r.Interval(15, TimeSpan.FromMilliseconds(200)))` y `UseInMemoryOutbox`.

## Alternativas descartadas

- **Hacer idempotentes los consumers/dominio aguas abajo** (que `Ticket.Reject`,
  `Zone.ConfirmSlot`/`ReleaseSlot` toleren un segundo intento silenciosamente): parchea el síntoma
  en cada punto de fallo, uno por uno, sin cerrar la causa — cualquier otro efecto secundario
  nuevo que se añada a estas Sagas en el futuro repetiría el mismo bug en un sitio distinto. El
  outbox cierra la clase completa de problema en el origen.
- **Quitar el `Publish()` de dentro de `.Then()/.ThenAsync()` y moverlo a un consumer posterior
  dedicado**: reescribiría la máquina de estados existente (varias rondas de fixes de carrera
  detrás, ADR 0010/0018/0021/0025/0027/0036/0037) por un beneficio que el outbox ya da sin tocar
  la lógica de negocio.
- **Subir aún más los reintentos de Mongo**: no cierra nada — el bug no es "pocos reintentos", es
  que cada reintento reproduce efectos secundarios ya enviados. Más reintentos = más duplicados.
- **Arreglar también el guard de invariante en `Zone.ConfirmSlot`/`ReleaseSlot`** (para que
  toleren confirmar/liberar "de más"): descartado — enmascararía sobreventa real si algún día
  ocurre por un motivo distinto a este bug. El guard debe seguir rompiendo fuerte; lo que se
  arregla es que deje de dispararse por un duplicado que nunca debió existir.

## Consecuencias / riesgo aceptado

- Los tickets ya huérfanos de incidentes anteriores a este fix (p. ej. los de `Grada VIP Norte`
  documentados en `.ia/KNOWN-ISSUES.md`) **no se autorreparan** — este fix evita que se generen
  nuevos, no reconcilia el dato ya corrupto. Requiere un reset de datos (`docker compose down -v`
  + `up --build` de nuevo) si se quiere una demo limpia, no un fix de código adicional.
- Mismo trade-off que el resto del proyecto acepta para reintentos (ADR 0035/0036): si la
  contención real agotara también los 15 intentos del guardado de la Saga, el mensaje seguiría
  yendo a la cola de error — el outbox evita la duplicación, no elimina la necesidad de reintentar.
- `UsePartitioner` en `TicketPurchaseSagaDefinition` serializa eventos del mismo ticket entre sí
  (no entre tickets distintos) — no añade contención nueva relevante, mismo criterio que ADR 0036.
