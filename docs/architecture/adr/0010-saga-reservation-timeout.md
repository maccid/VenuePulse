# ADR 0010 — Timeout de `Reserving` en `TicketPurchaseSaga` sin `IMessageScheduler`

## Contexto

Si `AccessControl` no responde a `ReserveZoneSlot` (caído, cola perdida, bug), la Saga se queda
colgada en `Reserving` para siempre: el `Ticket` nunca sale de `Pending` y el fan no ve ni
confirmación ni rechazo. Hace falta un timeout que la mueva a `Rejected` ("ReservationTimedOut").

## Alternativas descartadas

- **`IMessageScheduler` de MassTransit con Quartz.NET**: añade el paquete
  `MassTransit.QuartzIntegration` + Quartz.NET. Un job store en memoria tiene la misma limitación
  que la solución elegida (no sobrevive a un reinicio) pero con más piezas; un job store
  persistente añadiría otra base de datos/esquema solo para un timeout. Ceremonia sin beneficio
  real — mismo argumento que ADR 0003 contra CQRS fuera de `CheckIn`.
- **`IMessageScheduler` con RabbitMQ delayed-exchange**: requiere el plugin
  `rabbitmq_delayed_message_exchange`, que la imagen actual (`rabbitmq:3-management-alpine`) no
  trae. Habría que construir una imagen custom que descargue el `.ez` en build time y lo habilite
  — una dependencia de red en el build de infra y un componente más que verificar en la demo.
  Exactamente el tipo de infraestructura que ADR 0006 y el README ("qué NO se construyó") evitan
  sin una necesidad real y documentada.

## Decisión

Un **watchdog en memoria, sin infraestructura nueva**, dentro de `CheckIn.Infrastructure.Sagas`:

- `ReservationTimeoutTracker` (singleton): `ConcurrentDictionary<CorrelationId, (ZoneId, DeadlineUtc)>`.
- `ReservationWatchdogConsumer`: escucha los mismos eventos que ya escucha la Saga
  (`TicketPurchaseRequested` → `Track`; `ZoneSlotReserved`/`ZoneSlotReservationFailed` →
  `Untrack`), sin acoplarse al estado interno de la state machine ni a su repositorio de Mongo.
- `ReservationTimeoutMonitor` (`BackgroundService`): cada `Saga:TimeoutPollIntervalSeconds`
  (5s por defecto) barre el tracker y publica `ReservationTimedOut` para lo que superó
  `Saga:ReservationTimeoutSeconds` (30s por defecto).
- La Saga añade `When(TimedOut)` en `During(Reserving, ...)`, con la misma forma que
  `SlotReservationFailed`: fija `RejectionReason`, publica `TicketPurchaseRejected` y transiciona
  a `Rejected` con `Finalize()`.

## Consecuencias / riesgo aceptado

- **No sobrevive a un reinicio de `CheckIn`**: una reserva en curso durante un reinicio pierde su
  timeout (el consumer nunca la volverá a `Track`-ear). Mismo tipo de riesgo ya aceptado en
  `KNOWN-ISSUES.md` para el arranque en frío del Bloque 1 — cubre el caso relevante para una
  demo, no es una garantía formal de todos los casos.
- **Granularidad = intervalo de sondeo**, no un timeout exacto al segundo (aceptable: el objetivo
  es no colgarse para siempre, no precisión de reloj).
- **Ventana de carrera documentada**: si `ZoneSlotReserved` llega justo después de que el
  watchdog ya haya publicado `ReservationTimedOut`, la Saga ya finalizó en `Rejected` y ese aforo
  reservado en `AccessControl` no se libera automáticamente. No se resuelve aquí (añadir un
  Inbox/compensación para este caso es justamente el Bloque 3, `feature/accesscontrol-inbox`,
  para la redelivery general de mensajes).
