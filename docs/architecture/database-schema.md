# Diagrama de base de datos — VenuePulse

VenuePulse sigue el patrón **database-per-service**: tres bases PostgreSQL independientes (una
por microservicio) más una colección MongoDB para el estado de la Saga, sin ningún esquema
compartido. No existen claves foráneas físicas entre servicios — la consistencia entre ellos se
logra de forma asíncrona vía eventos de integración (RabbitMQ / MassTransit). Ver
`.ia/ARCHITECTURE.md` y ADR 0005.

| Base de datos | Motor | Servicio | Propósito |
|---|---|---|---|
| `venuepulse_ticketing` | PostgreSQL 16 | Ticketing | Catálogo de eventos y tipos de entrada |
| `venuepulse_accesscontrol` | PostgreSQL 16 | AccessControl | Zonas y aforo por puerta, histórico de ocupación |
| `venuepulse_checkin` | PostgreSQL 16 | CheckIn | Entradas emitidas y su ciclo de vida (compra → check-in) |
| `venuepulse_sagas` | MongoDB | CheckIn | Estado de la saga de compra (`TicketPurchaseSagaState`), documento de forma variable |

## Ticketing — `venuepulse_ticketing`

Dueño del catálogo: eventos y sus tipos de entrada. `ticket_types` es una owned entity de
`Event` — se persiste y se borra en cascada junto al evento dueño, no es un agregado
independiente.

```mermaid
erDiagram
    EVENTS {
        uuid Id PK
        varchar_200 Name
        varchar_200 VenueName
        timestamptz StartsAtUtc
        varchar_20 Type "enum EventType: Match / Concert"
        varchar_20 Status "enum: OnSale / SalesClosed / Cancelled"
        varchar_20 Relevance "enum: Standard / Special"
    }
    TICKET_TYPES {
        uuid Id PK
        uuid EventId FK
        varchar_120 Name
        decimal_10_2 Price
        varchar_500 Description
    }
    ZONE_DEMAND_SNAPSHOTS {
        uuid ZoneId PK "= AccessControl.zones.Id, logico"
        uuid EventId "logico, misma BD que events"
        int Capacity
        int AvailableToSell
        int ConfirmedSold
        int CheckedIn
    }
    EVENTS ||--o{ TICKET_TYPES : "owns (cascada)"
```

- `ticket_types` no tiene ciclo de vida propio: es una colección owned de `Event` (borrado en
  cascada implícito).
- `zone_demand_snapshots` es una tabla de proyección de lectura alimentada por eventos de
  integración desde AccessControl; su PK (`ZoneId`) es la clave de otra base de datos — no hay
  FK física, solo correlación por `Guid`.

## AccessControl — `venuepulse_accesscontrol`

Aforo por zona/puerta, scoped por evento. No usa el patrón Outbox/Inbox de MassTransit:
implementa su propio inbox idempotente manual (`processed_messages`, ADR 0015).

```mermaid
erDiagram
    ZONES {
        uuid Id PK
        uuid EventId "logico, Ticketing.events.Id"
        varchar_120 Name
        int Capacity
        int Reserved
        int ConfirmedSold
        int CheckedIn
        boolean Cancelled
    }
    OCCUPANCY_SNAPSHOTS {
        uuid Id PK
        uuid EventId "logico"
        uuid ZoneId "logico, sin FK fisica"
        timestamptz RecordedAtUtc
        int ConfirmedSold
        int CheckedIn
    }
    PROCESSED_MESSAGES {
        uuid EventId PK "Id del mensaje, no de negocio"
        varchar_200 MessageType
        timestamptz ProcessedAtUtc
    }
    ZONES |o..o{ OCCUPANCY_SNAPSHOTS : "ZoneId, sin FK"
```

- `AvailableToSell` es una propiedad calculada (`Capacity - Reserved - ConfirmedSold`), no se
  persiste como columna.
- Índice único compuesto `(EventId, Name)` en `zones`; índice compuesto
  `(EventId, RecordedAtUtc)` en `occupancy_snapshots`.
- `occupancy_snapshots` es la serie temporal del motor de simulación — sin navegación EF hacia
  `zones`, solo el `Guid`.

## CheckIn — `venuepulse_checkin`

Único servicio con CQRS/MediatR. Dueño del agregado `Ticket` y de la `TicketPurchaseSaga` (esta
última persistida aparte, en MongoDB).

```mermaid
erDiagram
    TICKETS {
        uuid Id PK
        uuid EventId "logico, Ticketing"
        uuid TicketTypeId "logico, Ticketing"
        uuid ZoneId "logico, AccessControl"
        varchar_200 Holder
        varchar_20 Status "Pending..AwaitingPayment..ProcessingPayment..Confirmed..Rejected..CheckedIn..Refunded"
        varchar_200 RejectionReason
        decimal_10_2 Price "inmutable tras compra, ADR 0021"
        timestamptz CreatedAtUtc
        timestamptz UpdatedAtUtc
        timestamptz ConfirmedAtUtc
        timestamptz CheckedInAtUtc
    }
```

- `tickets` es la única tabla de dominio de este servicio; sus tres referencias (`EventId`,
  `TicketTypeId`, `ZoneId`) apuntan a agregados de otros microservicios — EF Core no las modela
  como FK relacional.
- `CreatedAtUtc` / `UpdatedAtUtc` los rellena el `AuditingSaveChangesInterceptor` vía la interfaz
  `IAuditable` (ADR 0011).

## Infraestructura de mensajería (MassTransit)

Mismo esquema Outbox/Inbox transaccional, replicado físicamente en `venuepulse_ticketing` y
`venuepulse_checkin` (ADR 0002). AccessControl no lo usa — tiene su propio inbox manual
(`processed_messages`, ver arriba).

```mermaid
erDiagram
    OUTBOX_STATE {
        uuid OutboxId PK
        timestamptz Created
        timestamptz Delivered
        uuid LockId
        bigint LastSequenceNumber
        bytea RowVersion
    }
    OUTBOX_MESSAGE {
        bigint SequenceNumber PK
        uuid OutboxId FK
        uuid InboxMessageId FK "parte de FK compuesta"
        uuid InboxConsumerId FK "parte de FK compuesta"
        text Body
        text MessageType
        varchar_256 DestinationAddress
        uuid MessageId
        uuid CorrelationId
        timestamptz SentTime
        timestamptz EnqueueTime
    }
    INBOX_STATE {
        bigint Id PK
        uuid MessageId
        uuid ConsumerId
        timestamptz Delivered
        timestamptz Consumed
        int ReceiveCount
        bytea RowVersion
    }
    OUTBOX_STATE ||--o{ OUTBOX_MESSAGE : "OutboxId"
    INBOX_STATE ||--o{ OUTBOX_MESSAGE : "(MessageId, ConsumerId)"
```

## Relaciones entre bases de datos (event-driven, sin FK física)

Cada base de datos es opaca para las demás. Lo que en un monolito sería una FK aquí es una copia
de `Guid` mantenida por consistencia eventual, propagada por eventos de integración.

```mermaid
flowchart LR
    subgraph TICKETING["venuepulse_ticketing"]
        EVT[events]
        TT[ticket_types]
        ZDS[zone_demand_snapshots]
    end
    subgraph ACCESSCONTROL["venuepulse_accesscontrol"]
        ZN[zones]
        OCC[occupancy_snapshots]
    end
    subgraph CHECKIN["venuepulse_checkin"]
        TK[tickets]
    end
    subgraph SAGAS["venuepulse_sagas (Mongo)"]
        SG[TicketPurchaseSagaState]
    end

    EVT -.->|"EventCreated: provisiona 3 zonas"| ZN
    EVT -.->|"EventId"| TK
    TT -.->|"TicketTypeId"| TK
    ZN -.->|"ZoneId"| TK
    ZN -.->|"eventos de ocupacion"| ZDS
    ZN -.->|"ZoneId, misma BD, sin FK"| OCC
    ZN -.->|"ZoneId, cross-store"| SG
```

**Convención:** línea sólida = FK física, misma base de datos, integridad referencial
garantizada por Postgres. Línea discontinua = referencia lógica por `Guid`, entre bases de datos
(o sin FK declarada); consistencia eventual vía eventos de integración.

---

Generado a partir de los `DbContext`, entidades de dominio y migraciones EF Core bajo
`services/*/Domain` e `Infrastructure` (estado del repo al 2026-08-10). Referencias: ADR 0002
(Outbox/Inbox), ADR 0005 (persistencia poliglota), ADR 0011 (auditoría), ADR 0015 (inbox manual
AccessControl), ADR 0021 (precio inmutable del ticket).
