# ADR 0003 — CQRS solo en CheckIn, no en Ticketing ni AccessControl

## Contexto

La oferta pide experiencia aplicando CQRS. Es tentador aplicarlo uniformemente a los 3
servicios "porque la oferta lo pide", pero eso sería ceremonia sin beneficio en dos de ellos.

## Decisión

| Servicio | Complejidad real | Patrón |
|---|---|---|
| **Ticketing** | Catálogo que apenas cambia, mayoritariamente lectura | Application Service simple (`GetEvents`, `GetTicketTypes`) |
| **AccessControl** | Reservar/confirmar/liberar aforo es CRUD con un par de invariantes | Application Service simple |
| **CheckIn** | Única pieza con orquestación real (la Saga) y consistencia eventual | **CQRS con MediatR**: `PurchaseTicketCommand`/`CheckInCommand` (escritura compleja) vs `GetTicketStatusQuery`/`GetMyTicketsQuery` (lectura simple y frecuente) |

## Justificación

CQRS separa modelos de lectura y escritura cuando hay **asimetría real** entre ambos. En
`Ticketing` y `AccessControl` leer y escribir tienen la misma forma y el mismo coste — separar
comandos y consultas ahí añadiría una capa de indirección sin ganancia. En `CheckIn`, escribir
implica arrancar una Saga con varios saltos de red y compensación posible, mientras que leer
es una consulta directa a Postgres: la asimetría es real y CQRS aporta claridad genuina.

Este reparto es en sí mismo el argumento de entrevista: "aplico CQRS donde hay asimetría real,
no como checklist".
