# ADR 0005 — Persistencia poliglota: el estado de la Saga vive en MongoDB, no en Postgres

## Contexto

`CheckIn` ya tiene Postgres para el agregado `Ticket` (con su Outbox). La Saga necesita su
propio almacén de estado — la pregunta es si reutilizar Postgres o usar algo distinto.

## Decisión

El estado de `TicketPurchaseSaga` (`TicketPurchaseSagaState`) se persiste en **MongoDB**, vía
el `MongoDbSagaRepository` nativo de MassTransit — no en la misma base Postgres que `Ticket`.

## Justificación

El estado de una Saga es un **documento de proceso** (paso actual, IDs de correlación,
timestamps, motivo de rechazo) cuya forma cambia según el camino tomado: un documento en el
camino de compensación tiene campos (`rejectionReason`, fecha de compensación) que no existen
en el camino feliz. Eso encaja mejor como documento que como fila con columnas fijas, donde
cada campo nuevo de un paso futuro exigiría una migración de esquema y dejaría columnas NULL
para los caminos que no las usan.

Además, la Saga **no necesita atomicidad transaccional** con el agregado `Ticket` de Postgres:
es eventualmente consistente por diseño (por eso es una Saga y no una transacción distribuida).
Separar su almacén no compromete ninguna garantía real.

Es el mismo principio que se aplica en el `core` privado del candidato para instancias de
tarea — aquí reconstruido con la librería oficial de MassTransit y datos 100% nuevos, sin
ningún código ni conocimiento propietario trasladado.

## Trade-off aceptado

Un tercer motor de persistencia (Postgres + MongoDB + RabbitMQ) añade una pieza más a operar.
Se acepta porque el motivo es real, no decorativo: cada tecnología resuelve un problema que las
otras dos no resuelven igual de bien (ver también ADR sobre Redis descartado, en el README).
