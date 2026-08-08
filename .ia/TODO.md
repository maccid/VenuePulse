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
- [ ] **Bloque 8 — `feature/simulation-engine`** (sin asignar): motor de simulación **controlable**
  para dar vida al demo y alimentar futuras predicciones. Dos señales separadas — hoy el demo las
  confunde porque convergen: la venta incrementa `ConfirmedSold` y el torniquete `CheckedIn`, y sin
  dimensión temporal ni no-shows ambos parecen lo mismo:
  - **Ventas**: para eventos futuros, genera compras distribuidas en el tiempo (curva: más ventas
    en los últimos días) → `ConfirmedSold`.
  - **Torniquete**: el día del evento, genera check-ins en las horas previas siguiendo una curva de
    llegada con ~5-10% de no-shows → `CheckedIn` < `ConfirmedSold`.
  Controlable desde `venue-ops` (toggle/intensidad), no caos autónomo — en el demo en vivo el
  entrevistador debe poder dirigir el flujo. **Depende del Bloque 1** (zonas por evento): sin
  `EventId` en `Zone`, ventas y aforo de todos los eventos se mezclan en los mismos contadores.
  Es la fuente de datos para el **Bloque 11** (predicción de ocupación).
- [ ] **Bloque 11 — `feature/occupancy-prediction`** (sin asignar): predicción de ocupación con
  modelo estadístico honesto (regresión / exponential smoothing sobre datos que el propio sistema
  genera vía Bloque 8), **no** "ML mágico". Predice la **curva de llegada + no-shows sobre
  `ConfirmedSold`** — la métrica que de verdad opera un estadio ("¿cuántos pasarán el torniquete y
  cuándo?"), no las ventas. Es el pilar de Smart Venues que encaja con la demo y la prioridad #1
  de esta ronda de mejoras. Nuevo servicio/endpoint + gráfica en `venue-ops`. ADR nuevo.
- [ ] **Bloque 9 — `feature/dynamic-pricing`** (sin asignar): precio de un `TicketType` ajustado
  por demanda con heurísticas transparentes (ej. ocupación del sector > X% → sube un escalón;
  demanda baja → baja), **no** ML entrenado. Depende del Bloque 8 (sin demanda simulada no hay
  señal). Visible en el checkout (el precio cambia en vivo con la ocupación). Evaluar ADR pequeño.
  **Recomendado sobre el Bloque 10 si el tiempo aprieta**: se demuestra en 30 segundos y es
  defendible sin datos reales.
- [ ] **Bloque 10 — `feature/fraud-detection`** (sin asignar, **OPCIONAL / candidato a recorte**):
  detección de fraude/reventa con heurísticas (misma IP comprando N entradas en T segundos, misma
  tarjeta en muchos nombres), con el simulador inyectando patrones y un detector que los marca y
  alerta. Con datos sintéticos no hay labels — solo defendible como reglas explícitas, no ML. Es el
  más débil para la entrevista ("¿cómo sabes que es fraude si lo has generado tú?"). Depende del
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
  Contexto adicional: comparado con el gateway de `olix` (YARP puro) — en VenuePulse
  nginx además compone los estáticos de los micro frontends (ADR 0004); revisar YARP vs nginx
  quedó apuntado en `services.olix/.ia/TODO.md`, no aquí.
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
