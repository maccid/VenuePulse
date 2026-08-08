# TODO — VenuePulse

Espejo del task-tracker de la sesión. Actualizar al cerrar o abrir tareas.

> Trabajo repartido entre Claude Code y opencode en paralelo (varias IAs, varias ramas cortas vía
> `git worktree`, ver `.ia/GIT-FLOW.md`). Esta sección es la fuente de verdad compartida entre
> ambas — el plan detallado original de esta ronda vive además en
> `C:\Users\miguel.cidgarcia\.claude\plans\clever-percolating-cat.md` (solo accesible desde
> Claude Code), pero **este fichero es el que deben leer ambas IAs** antes de tocar código.

## En curso

- [x] **Bloque 1 — `fix/zone-event-scoping`** (Claude Code): `EventId` en `Zone` vía `EventCreated`
  (event-driven) — `Ticketing` publica al sembrar (wiring de MassTransit + Outbox nuevo, antes no
  publicaba nada), `EventCreatedConsumer` nuevo en `AccessControl` aprovisiona las 3 zonas del
  evento (idempotente por `ExistsForEventAsync`), migraciones EF en ambos servicios,
  `ZonesController` con filtro `?eventId=`, `ZoneOccupancyDto` con `EventId`+`AvailableToSell`
  (de paso se corrigió que el frontend calculaba "libres" como `capacity-confirmedSold` sin
  restar `reserved`), frontend (`shell`/`ticket-checkout`/`venue-ops`) actualizado para
  pasar/mostrar el evento correcto (venue-ops ahora tiene selector de evento). `api-ticketing`
  en `docker-compose.yml` ahora depende de `api-accesscontrol: service_healthy` para mitigar la
  pérdida de `EventCreated` si el consumer no está enlazado a tiempo (ver `.ia/KNOWN-ISSUES.md`).
  Verificado en real con `docker compose up --build`: 2 eventos → 6 zonas (3 por evento, `eventId`
  correcto), compra de entrada real confirma `confirmedSold` en la zona correcta. Tests unitarios
  + arquitectura en verde (23). Sin ADR nuevo (ya justificado en KNOWN-ISSUES.md).
- [ ] **Bloque 2 — `feature/saga-timeout`** (Claude Code): timeout en el estado `Reserving` de
  `TicketPurchaseSaga` — si `AccessControl` no responde en N segundos, transición a `Rejected`
  ("ReservationTimedOut") en vez de quedarse colgada para siempre. Test nuevo en
  `TicketPurchaseSagaTests.cs`. ADR pequeño.
- [ ] **Bloque 3 — `feature/accesscontrol-inbox`** (Claude Code, después del Bloque 1): Inbox real
  (`ProcessedMessages` keyed por `EventId`) en `AccessControl` — hoy no tiene, pese a que
  `ARCHITECTURE.md` dice que Outbox/Inbox garantiza "exactamente una vez" (solo cierto en
  `CheckIn`). Sin esto, una redelivery de RabbitMQ duplica reservas/check-ins reales. ADR nuevo
  (siguiente número tras 0009).
- [ ] **Bloque 4 — `feature/core-shared-patterns`** (Claude Code): ampliar `VenuePulse.Core` con
  interceptor EF Core de auditoría, paginación genérica y guard clauses — inspiración de alto
  nivel de `olix.workspace` (nunca código/nombres literales, ver `.ia/PRODUCT-VISION.md`).
  Evaluar con cuidado si se mezcla con `Result` o se sustituye en algún punto. ADR nuevo.
- [ ] **Bloque 5 — `feature/ui-visual-refresh`** (opencode): aplicar la skill `frontend-design` a
  `shell`/`ticket-checkout`/`venue-ops` — hoy cada app es un único `App.tsx` monolítico sin
  componentización ni sistema de diseño. Sin ADR.
- [ ] **Bloque 6 — `chore/architecture-tests-location`** (opencode): `services/ArchitectureTests`
  necesita cargar ensamblados de los 3 servicios + Core, así que vivir fuera de un servicio es
  correcto — pero visualmente es hermano de `Core`/`Ticketing`/`AccessControl`/`CheckIn` como si
  fuera un servicio más. Mover a `tests/` de nivel superior o documentar explícitamente que es
  tooling transversal. Sin ADR.
- [x] **Bloque 7 — Infra Docker** (opencode, en curso ya antes de este plan): healthchecks en
  las 3 APIs (`curl /health`), `npm ci` en los Dockerfiles de UI, nota en README sobre rebuild
  selectivo por capas. **Se evaluaron perfiles `api`/`ui` y se descartaron** (sobreingeniería para
  una demo: `up --build` ya solo reconstruye lo que cambió y el orden lo cubren
  `depends_on`+healthchecks); se revisó y mantuvo el nginx.conf original sin `resolver`.
  Contexto adicional: comparado con el gateway de `olix` (YARP puro) — en VenuePulse
  nginx además compone los estáticos de los micro frontends (ADR 0004); revisar YARP vs nginx
  quedó apuntado en `services.olix/.ia/TODO.md`, no aquí.
- [ ] Verificar manualmente desde el navegador (no solo API) el camino feliz y el de
  compensación, con el timeline de la Saga por SignalR — único ítem sin marcar en el checklist
  de `docs/demo-script.md`.

**Verificado durante la revisión (sin acción):** MediatR excluido de `Ticketing`/`AccessControl`
es la decisión correcta (ADR 0003, forzado por `ArchitectureTests`); FluentAssertions ya se usa en
los tests de los 3 servicios (ADR 0008 se cumple); el modelo `Zone` (`Capacity`/`Reserved`/
`ConfirmedSold` vs `CheckedIn`) ya es coherente — lo único que faltaba era el scoping por evento
(Bloque 1).

## Pendiente de decidir (ver `docs/ROADMAP.md`)

- [ ] Rellenar `.ia/` con más detalle si aparecen huecos durante el desarrollo en vivo de la
  entrevista.
- [ ] Definir mecanismo de exportación "código sin `.git/` ni `.ia/`" para la entrega real al
  entrevistador (distinto de repartir solo el Docker, que era el plan inicial).

## Hecho

- [x] Scaffolding, 3 microservicios, Saga con Outbox/Inbox, tests (unit + arquitectura +
  integración), frontend (shell + ticket-checkout + venue-ops), CI/CD, documentación completa.
- [x] Estructura de carpetas renombrada a `services/`/`ui/` (desde `src/`/`frontend/`).
- [x] Fix CRLF en `init-multiple-dbs.sh`, nomenclatura de contenedores Docker, limpieza de
  imágenes huérfanas.
- [x] Verificación manual contra `docker-compose` real de ambos caminos de la Saga.
