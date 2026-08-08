# Arquitectura — VenuePulse

## Stack

- **.NET 10**, Central Package Management (`Directory.Packages.props`), `Directory.Build.props`.
- **EF Core + Npgsql** — una instancia de PostgreSQL, una base de datos por servicio.
- **MassTransit 8.5.10 + RabbitMQ** — mensajería, Saga (state machine), Outbox/Inbox transaccional.
- **MongoDB** — solo para el estado de la Saga (`TicketPurchaseSagaState`), ver ADR de
  persistencia poliglota.
- **MediatR** — CQRS, solo en `CheckIn` (único servicio con asimetría lectura/escritura real).
- **React 19 + Vite + TypeScript**, Web Components en light DOM para el único micro frontend.
- **OpenTelemetry + Jaeger** — trazas distribuidas reales, no solo logging.
- **Serilog** — logging estructurado con `TraceId`/`SpanId` como campos de correlación.

## Estructura de carpetas

```
services/       Backend .NET
  Core/           VenuePulse.Core — tipos base, Messaging (Outbox/Inbox/contratos), Cqrs
  Ticketing/      Sin CQRS — catálogo de eventos y tipos de entrada
  AccessControl/  Sin CQRS — zonas/puertas, aforo
  CheckIn/        CON CQRS (MediatR) — Ticket + Saga de compra
  ArchitectureTests/  ArchUnitNET, falla el build si se viola una regla de capas
ui/             3 apps React independientes: shell, ticket-checkout (micro frontend), venue-ops
infra/          docker-compose, K8s (Kustomize), Jenkinsfile, Nginx, guía Azure — todo separado
                de services/ y ui/, sin dependencias de código en ningún sentido
docs/           ADRs, AI-USAGE.md, demo-script.md
.ia/            Este directorio — contexto local para el agente IA
```

## Flujo de eventos (compra de entrada)

1. `POST /api/tickets` (CheckIn) → crea `Ticket` (Pending) → publica `TicketPurchaseRequested`
   en la MISMA transacción (Outbox) → `TicketPurchaseSaga` arranca.
2. Saga → `ReserveZoneSlot` → `AccessControl` reserva aforo → `ZoneSlotReserved` /
   `ZoneSlotReservationFailed`.
3. Si reservado: paso de pago mockeado (`SimulateFailure` en el mensaje decide el resultado).
4. Si pago OK: `ConfirmZoneSlot` → `Ticket` a `Confirmed` → `TicketPurchaseConfirmed` → SignalR.
5. Si pago falla: **compensación real** — `ReleaseZoneSlot` (libera el aforo ya reservado) →
   `Ticket` a `Rejected: PaymentFailed` → SignalR.
6. Check-in físico (simulado desde `venue-ops`): `CheckInCommand` → válida que el ticket esté
   `Confirmed` y sin usar → `RecordZoneCheckIn` → aforo sube en vivo.

## Decisiones clave (ver ADRs para el detalle completo)

- CQRS solo donde hay asimetría real (`CheckIn`), no como ceremonia (ADR 0003).
- Micro frontends con Web Component en light DOM, sin Module Federation (ADR 0004).
- Saga persistida en MongoDB, no en la misma Postgres que el agregado `Ticket` (ADR 0005).
- Observabilidad real con OpenTelemetry+Jaeger; sin Seq ni Prometheus/Grafana por alcance
  (ADR 0006).
- MediatR/FluentAssertions pese a su licencia comercial 2025 — uso de demo/portfolio (ADR 0008).
- MassTransit fijado en 8.5.10 (no 9.2.0) tras depuración real (ADR 0009).

## Scoping de `Zone` por `Event`

`Zone` tiene `EventId`: `Ticketing` publica `EventCreated` al sembrar un evento (Outbox propio) y
`AccessControl` lo consume para aprovisionar sus 3 zonas fijas ya vinculadas a ese evento — ver
`.ia/KNOWN-ISSUES.md` para el detalle y el riesgo de orden de arranque mitigado en
`docker-compose.yml`.
