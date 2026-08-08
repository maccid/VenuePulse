# Visión de producto — VenuePulse

## Qué es esto

Una demo técnica propia, construida para un proceso de selección de liderazgo técnico en
**Sportian** (línea Smart Venues: digitalización de estadios). No es un producto real ni tiene
fines comerciales — es un portfolio que demuestra experiencia real en DDD, microservicios .NET,
CQRS selectivo, arquitectura event-driven (Saga/Outbox/Pub-Sub), React con micro frontends,
Azure/AKS (documentado), CI/CD y uso de IA como acelerador de desarrollo validado por criterio
senior.

## Dominio simulado

Gestión de un recinto (estadio) con dos frentes deliberadamente acotados de los 5 posibles en
Smart Venues (ver README, sección de alcance):

- **Experiencia del fan** → comprar una entrada para un evento.
- **Operaciones del recinto** → control de accesos y aforo en tiempo real por zona/puerta.

Fuera de alcance a propósito (documentado como roadmap, no construido): videovigilancia,
analítica de datos avanzada, IoT/WiFi del recinto.

## Glosario común

| Término | Significado |
|---|---|
| **Event** (Ticketing) | Partido o concierto que se celebra en el recinto. Tiene `TicketType`s. |
| **TicketType** | Tipo de entrada de un Event (precio, descripción) — p.ej. "General", "VIP". |
| **Ticket** (CheckIn) | Entrada emitida a un fan para un `Event`+`TicketType`+`Zone` concretos. |
| **Zone** (AccessControl) | Puerta/zona física del recinto con aforo máximo, vinculada a un
  `EventId` concreto (aprovisionada vía `EventCreated` — ver `.ia/KNOWN-ISSUES.md`). |
| **Saga** (`TicketPurchaseSaga`) | Orquestador de la compra: reserva de zona → pago (mockeado)
  → confirmación o compensación. Vive en `CheckIn`, persistida en MongoDB. |
| **Outbox/Inbox** | Patrón transaccional de MassTransit para publicar eventos en la misma
  transacción que el cambio de agregado, y consumirlos exactamente una vez. |
| **Recinto** | El estadio físico. En esta demo, un único recinto (Estadio Santiago Bernabéu)
  para todos los eventos — ver ADR sobre persistencia poliglota y `.ia/ARCHITECTURE.md`. |

## Restricciones de propiedad intelectual (importante para cualquier agente)

- **Nunca** copiar código, namespaces o nombres literales de `olix.workspace` (proyecto privado
  del autor, usado solo como inspiración de patrón arquitectónico de alto nivel).
- **Nunca** referenciar "Sportian", "LaLiga" o el nombre real de la empresa dentro del código o
  los namespaces — el proyecto se llama `VenuePulse` a propósito.
- Pagos: siempre mockeados, nunca una pasarela real.

## Qué se entrega y qué no

Al entrevistador: el código fuente sin `.git/`, `.ia/`, `CLAUDE.md` ni `AGENTS.md` — esos cuatro
son contexto de trabajo local, no parte del proyecto en sí. El proyecto se enseña y ejecuta en
vivo con `docker compose up`. Todo lo demás (`services/`, `ui/`, `infra/`, `docs/`,
`README.md`) sí se entrega tal cual.
