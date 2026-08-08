# TODO — VenuePulse

Espejo del task-tracker de la sesión. Actualizar al cerrar o abrir tareas.

> Trabajo repartido entre Claude Code y opencode en paralelo (varias IAs, varias ramas cortas vía
> `git worktree`, ver `.ia/GIT-FLOW.md`). Esta sección es la fuente de verdad compartida entre
> ambas — el plan detallado original de esta ronda vive además en
> `C:\Users\miguel.cidgarcia\.claude\plans\clever-percolating-cat.md` (solo accesible desde
> Claude Code), pero **este fichero es el que deben leer ambas IAs** antes de tocar código.

## Reparto en oleadas (ronda 2, tras Bloque 1/5/6/7)

Objetivo: maximizar paralelismo real sin que las dos IAs toquen el mismo servicio (riesgo de
choque de migraciones EF) a la vez. 8 no depende de nada pendiente; 9/11/10 dependen de 8.

- **Oleada 1 (en paralelo desde ya)**:
  - Claude Code → Bloque 2 (`CheckIn`), después Bloque 4 (`Core`, aditivo) y luego Bloque 3
    (`AccessControl`) — este último solo tras que opencode haya mergeado su parte de
    `AccessControl` del Bloque 8, para no pisar migraciones.
  - opencode → Bloque 8 (`Ticketing`+`AccessControl`+`venue-ops`), el bloque grande y
    prioritario.
- **Oleada 2 (tras merge de Bloque 8 a `develop`)**:
  - opencode → Bloque 11 (`occupancy-prediction`).
  - Claude Code → Bloque 9 (`dynamic-pricing`).
- **Sin asignar / recorte si falta tiempo**: Bloque 10 (opcional), verificación manual E2E
  (al cierre, no paralelizable).

Reserva de números de ADR para evitar colisión entre ramas paralelas (siguiente tras 0009):
`0010` → Bloque 2 (Claude Code), `0011` → Bloque 3 (Claude Code), `0012` → Bloque 11 (opencode,
si aplica). **`0013` → Bloque 8 (opencode, ya usado — ver `docs/architecture/adr/0013-...md`).**

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
- [x] **Bloque 2 — `feature/saga-timeout`** (Claude Code, Oleada 1): timeout en el estado `Reserving`
  de `TicketPurchaseSaga` — si `AccessControl` no responde en `Saga:ReservationTimeoutSeconds`
  (30s por defecto), transición a `Rejected` ("ReservationTimedOut") en vez de quedarse colgada
  para siempre. **Implementado sin `IMessageScheduler`** (Quartz/RabbitMQ delayed-exchange
  descartados por añadir infraestructura sin necesidad real — ver ADR 0010): watchdog en memoria
  (`ReservationTimeoutTracker` + `ReservationWatchdogConsumer` + `ReservationTimeoutMonitor`
  como `BackgroundService`) en `CheckIn.Infrastructure.Sagas`, nuevo evento
  `ReservationTimedOut` en `Core.Messaging.Contracts`. Test nuevo en `TicketPurchaseSagaTests.cs`
  (9/9 en verde). ADR 0010. Riesgo aceptado documentado: no sobrevive a reinicio del servicio ni
  a una carrera con `ZoneSlotReserved` tardío (mismo tipo de trade-off que Bloque 1 en
  KNOWN-ISSUES.md).
- [ ] **Bloque 3 — `feature/accesscontrol-inbox`** (Claude Code, Oleada 1 — tras que opencode
  mergee su parte de `AccessControl` del Bloque 8, para no pisar migraciones EF del mismo
  servicio): Inbox real
  (`ProcessedMessages` keyed por `EventId`) en `AccessControl` — hoy no tiene, pese a que
  `ARCHITECTURE.md` dice que Outbox/Inbox garantiza "exactamente una vez" (solo cierto en
  `CheckIn`). Sin esto, una redelivery de RabbitMQ duplica reservas/check-ins reales. ADR nuevo
  (siguiente número tras 0009).
- [x] **Bloque 4 — `feature/core-shared-patterns`** (Claude Code, Oleada 1): ampliar `VenuePulse.Core`
  con interceptor EF Core de auditoría, paginación genérica y guard clauses — inspiración de alto
  nivel de patrones (nunca código/nombres literales de otros proyectos privados, ver
  `.ia/PRODUCT-VISION.md`). **No se
  mezcla con `Result`**: `Guard` sigue lanzando excepción (invariantes de entrada al agregado,
  igual que el código que sustituye), `Result` se queda para fallos de negocio esperables (ver
  ADR 0011). `Guard` adoptado en los 3 dominios (`Ticket`/`Event`/`Zone`, cambio mecánico sin
  migraciones). **Paginación (`PagedResult<T>`+`ToPagedResultAsync`) y auditoría
  (`IAuditable`+`AuditingSaveChangesInterceptor`) adoptadas solo en `CheckIn`** en esta ronda —
  a propósito, para no chocar con `feature/simulation-engine` (Bloque 8) tocando
  `Ticketing`/`AccessControl` en paralelo, ni romper el contrato de `GET /api/tickets?holder=`
  que ya consumen `shell`/`venue-ops` en real. Migración `AddTicketUpdatedAtUtc` en `CheckIn`.
  Tests nuevos con EF Core InMemory (`CoreSharedPatternsTests.cs`, sin Postgres real). Todo en
  verde: CheckIn 12/12, Ticketing 3/3, AccessControl 5/5, Architecture 7/7. ADR 0011. Candidatos
  para la próxima ronda: adoptar auditoría/paginación en `Event`/`Zone` cuando el Bloque 8
  mergee.
- [x] **Bloque 5 — `feature/ui-visual-refresh`** (opencode): aplicar la skill `frontend-design` a
  `shell`/`ticket-checkout`/`venue-ops` — hoy cada app es un único `App.tsx` monolítico sin
  componentización ni sistema de diseño. Sin ADR. **Implementado en rama**
  `feature/ui-visual-refresh`: `App.tsx` roto en componentes (shell: `SiteHeader`/
  `OccupancyWidget`/`EventCatalog`/`EventCard`/`CheckoutPanel`; ticket-checkout: `PurchaseForm`;
  venue-ops: `SiteHeader`/`ScanPanel`/`ZoneGauge`/`ZonesDashboard`), tokens de diseño `--vp-*`
  (tipografía, espacio, radio, sombra), CSS de ticket-checkout escopado bajo `.ticket-checkout`
  (ADR 0004), sin librerías UI nuevas. Las 3 apps compilan y lintean limpio. **Verificación
  pendiente**: `dotnet test` — 2 tests de integración de `CheckIn` (Testcontainers) fallan por
  timeout de infraestructura, no por este cambio (solo toca `ui/`); lo revisa Claude Code.
- [x] **Bloque 8 — `feature/simulation-engine`** (opencode, Oleada 1): motor de simulación **controlable**
  para dar vida al demo y alimentar futuras predicciones. Dos señales separadas — hoy el demo las
  confunde porque convergen: la venta incrementa `ConfirmedSold` y el torniquete `CheckedIn`, y sin
  dimensión temporal ni no-shows ambos parecen lo mismo:
  - **Ventas**: para eventos futuros, genera compras distribuidas en el tiempo (curva: más ventas
    en los últimos días) → `ConfirmedSold`.
  - **Torniquete**: el día del evento, genera check-ins en las horas previas siguiendo una curva de
    llegada con ~5-10% de no-shows → `CheckedIn` < `ConfirmedSold`.
  Controlable desde `venue-ops` (toggle/intensidad), no caos autónomo — en el demo en vivo el
  presentador debe poder dirigir el flujo. **Depende del Bloque 1** (zonas por evento): sin
  `EventId` en `Zone`, ventas y aforo de todos los eventos se mezclan en los mismos contadores.
  Es la fuente de datos para el **Bloque 11** (predicción de ocupación).
  **Implementado en rama `feature/simulation-engine`**: motor singleton en `AccessControl` (fases
  `Sales`/`Arrivals`, curvas `p²` y `1-(1-p)³`, worker `SimulationTickWorker` 500ms), endpoints
  `api/simulation/events/{id}/start|stop|reset|status`, tabla `occupancy_snapshots` (serie temporal
  con tiempo simulado para el Bloque 11, migración `AddOccupancySnapshots`), métodos simulados en
  `Zone` (`RecordSimulatedSales`/`RecordSimulatedCheckIns`/`ResetCounters`), panel de control en
  `venue-ops` (`SimulationPanel` + no-shows visibles en `ZoneGauge`). `Ticketing` intacto: `venue-ops`
  trae `EventStartsAtUtc` del catálogo. ADR 0013. Tests unitarios del motor/curvas/Zone en verde
  (24). **Verificación pendiente**: `dotnet test` completo + `docker compose up --build` end-to-end.
- [ ] **Bloque 11 — `feature/occupancy-prediction`** (opencode, Oleada 2 — tras merge del Bloque 8):
  predicción de ocupación con
  modelo estadístico honesto (regresión / exponential smoothing sobre datos que el propio sistema
  genera vía Bloque 8), **no** "ML mágico". Predice la **curva de llegada + no-shows sobre
  `ConfirmedSold`** — la métrica que de verdad opera un estadio ("¿cuántos pasarán el torniquete y
  cuándo?"), no las ventas. Es el pilar del smart venue que encaja con la demo y la prioridad #1
  de esta ronda de mejoras. Nuevo servicio/endpoint + gráfica en `venue-ops`. ADR nuevo.
- [ ] **Bloque 9 — `feature/dynamic-pricing`** (Claude Code, Oleada 2 — tras merge del Bloque 8):
  precio de un `TicketType` ajustado
  por demanda con heurísticas transparentes (ej. ocupación del sector > X% → sube un escalón;
  demanda baja → baja), **no** ML entrenado. Depende del Bloque 8 (sin demanda simulada no hay
  señal). Visible en el checkout (el precio cambia en vivo con la ocupación). Evaluar ADR pequeño.
  **Recomendado sobre el Bloque 10 si el tiempo aprieta**: se demuestra en 30 segundos y es
  defendible sin datos reales.
- [ ] **Bloque 10 — `feature/fraud-detection`** (sin asignar, **OPCIONAL / candidato a recorte,
  solo si sobra tiempo tras la Oleada 2**):
  detección de fraude/reventa con heurísticas (misma IP comprando N entradas en T segundos, misma
  tarjeta en muchos nombres), con el simulador inyectando patrones y un detector que los marca y
  alerta. Con datos sintéticos no hay labels — solo defendible como reglas explícitas, no ML. Es el
  más débil para defender ("¿cómo sabes que es fraude si lo has generado tú?"). Depende del
  Bloque 8.
- [x] **Bloque 6 — `chore/architecture-tests-location`** (opencode): `services/ArchitectureTests`
  necesita cargar ensamblados de los 3 servicios + Core, así que vivir fuera de un servicio es
  correcto — pero visualmente es hermano de `Core`/`Ticketing`/`AccessControl`/`CheckIn` como si
  fuera un servicio más. Mover a `tests/` de nivel superior o documentar explícitamente que es
  tooling transversal. Sin ADR. **Decisión: documentado como tooling transversal y renombrado a
  `services/Architecture.Tests/`** (`VenuePulse.Architecture.Tests`, convención dotted como el
  resto de proyectos de tests) — no se movió a `tests/`
  porque heredaría de `services/Directory.Build.props`/`Directory.Packages.props` (central package
  management) y las rutas relativas del `.slnx`/`ProjectReference`s; moverse implicaría duplicar
  configuración de paquetes. Se añadió la aclaración en `.ia/ARCHITECTURE.md`, `.ia/RULES.md` y
  `README.md`.
- [x] **Bloque 7 — Infra Docker** (opencode, en curso ya antes de este plan): healthchecks en
  las 3 APIs (`curl /health`), `npm ci` en los Dockerfiles de UI, nota en README sobre rebuild
  selectivo por capas. **Se evaluaron perfiles `api`/`ui` y se descartaron** (sobreingeniería para
  una demo: `up --build` ya solo reconstruye lo que cambió y el orden lo cubren
  `depends_on`+healthchecks); se revisó y mantuvo el nginx.conf original sin `resolver`.
  Contexto adicional: comparado con un gateway (YARP puro) — en VenuePulse
  nginx además compone los estáticos de los micro frontends (ADR 0004); revisar YARP vs nginx
  quedó apuntado en el TODO local del proyecto de referencia, no aquí.
- [ ] Verificar manualmente desde el navegador (no solo API) el camino feliz y el de
  compensación, con el timeline de la Saga por SignalR — único ítem sin marcar en el checklist
  de `docs/demo-script.md`.

**Verificado durante la revisión (sin acción):** MediatR excluido de `Ticketing`/`AccessControl`
es la decisión correcta (ADR 0003, forzado por `Architecture.Tests`); FluentAssertions ya se usa en
los tests de los 3 servicios (ADR 0008 se cumple); el modelo `Zone` (`Capacity`/`Reserved`/
`ConfirmedSold` vs `CheckedIn`) ya es coherente — lo único que faltaba era el scoping por evento
(Bloque 1).

## Pendiente de decidir (ver `docs/ROADMAP.md`)

- [ ] Rellenar `.ia/` con más detalle si aparecen huecos durante el desarrollo en vivo de la
  demo.
- [ ] Definir mecanismo de exportación "código sin `.git/` ni `.ia/`" para la entrega real
  (distinto de repartir solo el Docker, que era el plan inicial).

## Hecho

- [x] Scaffolding, 3 microservicios, Saga con Outbox/Inbox, tests (unit + arquitectura +
  integración), frontend (shell + ticket-checkout + venue-ops), CI/CD, documentación completa.
- [x] Estructura de carpetas renombrada a `services/`/`ui/` (desde `src/`/`frontend/`).
- [x] Fix CRLF en `init-multiple-dbs.sh`, nomenclatura de contenedores Docker, limpieza de
  imágenes huérfanas.
- [x] Verificación manual contra `docker-compose` real de ambos caminos de la Saga.
