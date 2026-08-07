# ADR 0001 — Orquestar la compra de entradas con una Saga (MassTransit + RabbitMQ)

## Contexto

Comprar una entrada implica coordinar dos servicios distintos (reservar aforo en
`AccessControl`, "cobrar" en `CheckIn`) con consistencia eventual, y necesita poder
**compensar** una reserva de aforo ya hecha si el paso siguiente falla — el caso de uso
central que la oferta de Sportian pide poder defender (Saga/Outbox/Pub-Sub).

## Decisión

- **Patrón**: Saga orquestada (no coreografiada) — `TicketPurchaseSaga`, una state machine de
  MassTransit, vive en `CheckIn` porque es el dueño del agregado transaccional (`Ticket`).
- **Broker**: RabbitMQ, no Azure Service Bus. Corre 100% local vía Docker sin coste ni
  credenciales de Azure — crítico para que la demo sea reproducible el día de la entrevista.
  MassTransit abstrae el transporte: pasar a Azure Service Bus en producción es cambiar
  `UsingRabbitMq` por `UsingAzureServiceBus` en la configuración, no tocar la Saga ni los
  consumidores (ver `infra/deploy/azure/README.md`).
- **Librería**: MassTransit v8, no RabbitMQ.Client puro (reinventaría retries/outbox/saga) ni
  NServiceBus (licencia comercial). Aporta de fábrica: state machines, Outbox/Inbox con EF
  Core, test harness in-memory y abstracción de transporte.

## Flujo (camino feliz y compensación)

1. `PurchaseTicketCommand` → `Ticket` (Pending) + evento `TicketPurchaseRequested` (vía Outbox)
   → arranca la Saga.
2. Saga → `ReserveZoneSlot` a `AccessControl` → `ZoneSlotReserved`/`ZoneSlotReservationFailed`.
3. Si reservado: paso de "pago" mockeado (sin pasarela real). Si falla → **compensación real**:
   `ReleaseZoneSlot` (libera el aforo ya reservado) → `Ticket` a `Rejected`.
4. Si el pago mock va bien: `ConfirmZoneSlot` → `Ticket` a `Confirmed`.

El paso 3 es el momento central de la demo: no es un rollback simulado, es la propia state
machine deshaciendo un paso que ya se había completado con éxito.

## Por qué no MediatR/RabbitMQ.Client a mano

Ver ADR 0008 sobre MediatR/FluentAssertions. Sobre no reinventar el Outbox: ver ADR 0002.
