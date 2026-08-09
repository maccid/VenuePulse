# ADR 0015 — Inbox real en `AccessControl` vía filtro genérico de MassTransit

## Contexto

`.ia/ARCHITECTURE.md` afirma que el Outbox/Inbox transaccional garantiza "exactamente una vez",
pero eso solo es cierto en `CheckIn`/`Ticketing` (Outbox propio, ver ADR 0002). `AccessControl`
consume los comandos de la Saga (`ReserveZoneSlot`/`ConfirmZoneSlot`/`ReleaseZoneSlot`/
`RecordZoneCheckIn`) y el evento `EventCreated` sin ninguna protección genérica contra
redelivery — solo `EventCreatedConsumer` es idempotente, y únicamente porque su lógica de negocio
lo permite (`ExistsForEventAsync`). Una redelivery real de RabbitMQ (entrega "al menos una vez",
p.ej. tras un fallo de red entre el ack y el commit) duplicaría una reserva/confirmación/check-in
real — aforo incorrecto en el demo en vivo.

## Alternativas descartadas

- **`AddEntityFrameworkOutbox<AccessControlDbContext>`** (el mismo mecanismo que ya usan
  `CheckIn`/`Ticketing`): el outbox EF de MassTransit trae de fábrica tablas de Outbox (salida) +
  `InboxState` (dedup de entrada) juntas. Pero `AccessControl` no publica eventos como parte de
  una transacción de escritura propia — ADR 0002 ya decidió explícitamente que no necesita ese
  grado de atomicidad ("sus mutaciones son más simples"). Adoptar el paquete completo solo para
  aprovechar la mitad (Inbox) es más superficie de la necesaria: una tabla `OutboxMessage` sin
  ningún uso real, `UseBusOutbox()` cambiando la semántica de entrega de todos los mensajes
  salientes del servicio, migración más grande. Descartado por la misma razón que ADR 0006/0010
  descartan infraestructura sin necesidad documentada.
- **Idempotencia ad-hoc por consumer** (como ya tiene `EventCreatedConsumer`): no generaliza —
  cada consumer nuevo tendría que inventar su propio criterio de "ya lo hice", y los comandos de
  zona no tienen un cheque de negocio natural para detectar redelivery (reservar dos veces es
  indistinguible de reservar una vez mirando solo el estado del agregado `Zone`).

## Decisión

Inbox real, pequeño y explícito, sin adoptar el Outbox completo:

- **`ProcessedMessage`** (`AccessControl.Infrastructure.Inbox`): tabla `processed_messages`, PK =
  `IntegrationEvent.EventId` (identidad de mensaje ya presente en el contrato de wire, no un id de
  negocio) + `MessageType` + `ProcessedAtUtc`. Vive en Infrastructure, no en el dominio — es un
  detalle técnico de entrega, no un invariante de negocio (mismo criterio que separa `Guard` de
  `Result` en ADR 0011).
- **`InboxDeduplicationFilter<TMessage>`** (`IFilter<ConsumeContext<TMessage>>`, con
  `where TMessage : IntegrationEvent`): antes de delegar al consumer, comprueba si el `EventId`
  del mensaje ya está en `ProcessedMessages`; si sí, lo descarta (log + `return`) sin tocar el
  consumer. Si no, añade el marcador al `DbContext` (sin `SaveChangesAsync` propio) y delega al
  siguiente eslabón del pipeline.
- Registrado como **filtro genérico abierto** vía
  `UseConsumeFilter(typeof(InboxDeduplicationFilter<>), context)` sobre el bus de `AccessControl`.
  `MessagingExtensions.AddVenuePulseMessaging` (la variante sin Outbox, Core.Messaging) ganó un
  parámetro opcional `configureBus` como único punto de extensión del bus ya construido — cubre
  los 5 consumers existentes sin tocar su código ni su firma.

**Atomicidad sin Outbox**: el filtro y el consumer comparten el mismo `AccessControlDbContext` con
ámbito por mensaje (scope de DI que MassTransit crea por cada mensaje consumido). El marcador se
añade al change tracker pero el filtro no llama a `SaveChangesAsync`; el que ya hace
`ZoneRepository.SaveAsync` dentro del consumer persiste el marcador y el cambio de negocio en la
misma transacción. Si el consumer lanza antes de guardar, el marcador nunca se persiste y el
mensaje puede reprocesarse sin riesgo de inconsistencia.

## Consecuencias / riesgo aceptado

- Si un consumer no llega a llamar a `SaveChangesAsync` (p.ej. `TryReserveSlotAsync` cuando la
  zona ya está llena, que retorna `false` sin guardar), el marcador tampoco se persiste — la
  próxima redelivery se reprocesa igual, pero es inofensivo porque no hubo efecto de negocio que
  duplicar.
- `processed_messages` crece sin purga — aceptable para una demo; un sistema real necesitaría
  retención por TTL sobre `ProcessedAtUtc`.
- Verificado con el harness in-memory de MassTransit (`InboxDeduplicationFilterTests`, sin broker
  ni Postgres reales, igual que `TicketPurchaseSagaTests` en `CheckIn`): dos publicaciones del
  mismo mensaje (mismo `EventId`, simulando una redelivery) solo producen un `ZoneSlotReserved` y
  un `Reserved++`; dos mensajes distintos con `EventId` diferentes se procesan ambos.
