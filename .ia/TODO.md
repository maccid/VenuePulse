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
`0010` → Bloque 2 (Claude Code, ya usado). **`0011` lo tomó en la práctica el Bloque 4** (la
reserva original decía Bloque 3, pero Bloque 4 se cerró antes — la tabla quedó desactualizada).
`0012` → Bloque 11 (opencode, ya usado — ver `docs/architecture/adr/0012-occupancy-prediction.md`). **`0013` → Bloque 8 (opencode, ya usado — ver
`docs/architecture/adr/0013-...md`).** **`0014` → Bloque 12 (opencode, si aplica).**
**`0015` → Bloque 3 (Claude Code, ya usado — ver `docs/architecture/adr/0015-accesscontrol-inbox.md`).**
**`0016` → Bloque 9 (Claude Code, ya usado — ver `docs/architecture/adr/0016-dynamic-pricing.md`).**
**`0017` → Bloque 13 (Claude Code, ya usado — ver
`docs/architecture/adr/0017-purchase-wizard-venue-map.md`).**
Próximo libre para nuevas ramas: `0018`.

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
- [x] **Bloque 3 — `feature/accesscontrol-inbox`** (Claude Code, Oleada 1 — desbloqueado tras el
  merge del Bloque 8 a `develop`): Inbox real (`ProcessedMessages` keyed por
  `IntegrationEvent.EventId`, no un id de negocio) en `AccessControl` — antes solo
  `EventCreatedConsumer` era idempotente (`ExistsForEventAsync`); los comandos de zona
  (`ReserveZoneSlot`/`ConfirmZoneSlot`/`ReleaseZoneSlot`/`RecordZoneCheckIn`) no tenían ninguna
  protección contra redelivery de RabbitMQ. **Sin adoptar el Outbox EF completo** (descartado por
  sobreingeniería, ver ADR 0015): filtro genérico `InboxDeduplicationFilter<TMessage>`
  (`IFilter<ConsumeContext<TMessage>>`) registrado vía `UseConsumeFilter` sobre los 5 consumers
  existentes, sin tocar su código. Atómico con el cambio de negocio sin Outbox: el filtro y el
  consumer comparten el mismo `AccessControlDbContext` con ámbito por mensaje, así que el
  `SaveChangesAsync` que ya hace `ZoneRepository.SaveAsync` persiste el marcador y el cambio de
  negocio juntos. `MessagingExtensions.AddVenuePulseMessaging` (variante sin Outbox) ganó un
  parámetro opcional `configureBus` como único punto de extensión para engancharlo. Migración
  `AddProcessedMessages`. Tests nuevos con el harness in-memory de MassTransit
  (`InboxDeduplicationFilterTests.cs`, sin broker real): redelivery del mismo mensaje solo aplica
  el efecto una vez, mensajes distintos se procesan ambos. AccessControl 26/26 en verde. ADR 0015.
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
- [x] **Bloque 11 — `feature/occupancy-prediction`** (opencode, Oleada 2 — tras merge del Bloque 8):
  predicción de ocupación con **modelo estadístico honesto**, no "ML mágico": mínimos cuadrados
  lineales de la **asíntota A** (llegadas totales esperadas) contra la forma fija
  `F(p)=1-(1-p)³` — la misma curva de llegada que genera el simulador, así que el sistema predice
  con la forma de llegada que él mismo genera — y **no-shows = `1 - A/ConfirmedSold`** (estimados
  por el ajuste, no un parámetro adivino), con **R²** como bondad visible en la UI. **Sin servicio
  nuevo** (mismo criterio que ADR 0013, sobreingeniería): endpoint
  `GET api/prediction/events/{id}?eventStartsAtUtc=` en `AccessControl` sobre los
  `occupancy_snapshots` del Bloque 8 **agregados a nivel de evento** (métrica que opera un estadio);
  `eventStartsAtUtc` lo trae venue-ops del catálogo (bounded contexts, patrón del ADR 0013); 404 si
  el evento aún no tiene snapshots; **sin historia de llegadas → no-show asumido 8%** con
  `FitFromData=false` para que la UI lo muestre como supuesto, no como dato. Gráfica SVG a mano en
  `venue-ops` (`PredictionChart` + `PredictionPanel`, sin librerías — coherencia Bloque 5): curva
  proyectada vs puntos observados, total previsto, vendidas y marca de "ahora". Piezas:
  `ArrivalForecast` + `OccupancyPredictionService` (`Application/Prediction/`), `PredictionController`,
  `GetForEventAsync` en el repo de snapshots. Sin CQRS/MediatR (ADR 0003). Tests: modelo puro
  (recuperación de la asíntota, clamping a vendidas/llegadas, no-shows asumido) + servicio
  (agregación por zonas, proyección hasta el evento, 404 sin datos) — AccessControl 38/38 en verde.
  ADR 0012. **Verificación E2E**: `docker compose up --build` + Sales→Arrivals del Bloque 8 →
  predicción con `fitFromData=true`, `nowUtc` dentro de la ventana pre-evento, R²≈0.999 y no-shows
  ≈8% (el 8% real de la simulación). De paso se arreglaron tres bugs que solo aparecían con
  Postgres real (los tests unitarios usan in-memory): (1) el **reset no limpiaba los snapshots**
  (`ClearForEventAsync` existía pero nunca se conectaba) y la predicción acumulaba basura de
  corridas anteriores (R² 0.62, puntos pegados a progress 0/1); (2) el engine escribía snapshots
  **post-evento** (el `VirtualNowUtc` no se clampa, solo el progress) que corrompían el "ahora"
  de la predicción — ahora el tick que completa el run aplica pero no escribe snapshot; (3) los
  snapshots se escribían con el offset local del `EventStartsAtUtc` del catálogo (+02:00) y
  **Npgsql rechaza offsets ≠ 0 en `timestamptz`** — el write fallaba silenciosamente en el worker
  y rompía todo el pipeline simulación→predicción; ahora se normaliza a UTC. Regresión completa
  de la solución en verde (salvo los 2 integration tests de CheckIn, flaky pre-existente).
- [ ] **Bloque 12 — `feature/ui-tailwind`** (sin asignar): introducir Tailwind CSS en las 3 apps
  React (`shell`/`ticket-checkout`/`venue-ops`) y de paso revisar la parte `ui/` para hacerla más
  amigable. Hoy el estilo es CSS en BEM con tokens `--vp-*` (Bloque 5, que fijó "sin librerías UI
  nuevas") — Tailwind sustituye las clases BEM por utilidades y acelera iterar en el diseño.
  Alcance: Tailwind v4 + plugin `@tailwindcss/vite` en cada app, mapear los tokens `--vp-*` a
  `@theme` para no perder el sistema de diseño, migrar los `App.css`/`index.css` y los `className`
  de los componentes, y una pasada de UX (estados vacíos, jerarquía, responsive). Notas: revierte
  la decisión del Bloque 5, así que ADR pequeño (`0014`) si se aprueba; toca solo `ui/` (sin riesgo
  de migraciones EF); el Bloque 11 también mete una gráfica en `venue-ops` — coordinar para no
  pisarse el mismo componente.
- [ ] **Bloque 13 — `feature/purchase-wizard-venue-map`** (Claude Code, nuevo — ampliación de
  alcance pedida al plantear el Bloque 12): antes de migrar a Tailwind, dar más realismo funcional
  al checkout y a operaciones. `ticket-checkout`: `PurchaseForm` (un único formulario) sustituido
  por un wizard de 3 pasos (`PurchaseWizard` + `StepTicketType`/`StepZone`/`StepConfirm`) — tipo de
  entrada, zona (elegida sobre un plano visual del recinto, `StadiumMap`, en vez de un `<select>`),
  y nombre del comprador + confirmación como último paso (antes se capturaba en `shell` y viajaba
  fijo). `shell`: `EventCatalog`/`CheckoutPanel` dejan de gestionar `holder`, sin otro consumidor
  afectado. `venue-ops`: mismo `StadiumMap` (copia adaptada, solo lectura, coloreado por
  `checkedIn` en vez de `availableToSell`) añadido como panel adicional en `ZonesDashboard`, sin
  quitar las tarjetas `ZoneGauge` existentes. **Plano = mapeo estático en frontend por `zone.name`,
  sin geometría en el backend**: las 3 zonas (`Puerta Norte`/`Puerta Sur`/`Grada VIP`) son
  hardcodeadas e idénticas para cualquier evento (`ZoneService.FixedZones`), así que añadir
  `PositionX/Y` al dominio sería ingeniería especulativa (ver ADR 0017, mismo criterio que ADR
  0016). Sin librería de gráficos nueva (SVG a mano), sin cambios de backend, componente duplicado
  entre las dos apps a propósito (ADR 0004 ya descarta código compartido entre micro frontends).
  ADR 0017.
- [x] **Bloque 9 — `feature/dynamic-pricing`** (Claude Code, Oleada 2 — tras merge del Bloque 8):
  precio de un `TicketType` ajustado por demanda con heurísticas transparentes en escalones fijos
  (`<40%` -10%, `40-70%` base, `70-90%` +15%, `≥90%` +30%), **sin** ML. **Ocupación agregada a
  nivel de `Event`, no de zona/sector** como decía el enunciado original: `Zone` (AccessControl) y
  `TicketType` (Ticketing) son bounded contexts distintos sin relación 1:1 hoy, solo comparten
  `EventId` — inventar esa relación cruzada era más superficie de la que la demo necesita (ver ADR
  0016). Nuevo evento `ZoneOccupancyChanged` publicado por `AccessControl` reutilizando el mismo
  punto de notificación que ya usa el SignalR de aforo (`SignalRZoneOccupancyNotifier`, registro
  pasado de `AddSingleton` a `AddScoped` para poder inyectar `IPublishEndpoint`). `Ticketing`
  estrena su primer consumer (`ZoneOccupancyChangedConsumer`) que mantiene una proyección local
  (`ZoneDemandSnapshot`, migración `AddZoneDemandSnapshots`) y recalcula/empuja el precio efectivo
  por un nuevo `PricingHub` — sin Inbox real, es un snapshot idempotente por naturaleza (a
  diferencia de los comandos de zona del Bloque 3/ADR 0015). `TicketTypeDto` gana
  `EffectivePrice`/`PriceTier` calculados en cada lectura, `Price` (catálogo) no se toca.
  `ticket-checkout` se suscribe a `/hubs/pricing` igual que ya hace con el aforo; nueva ruta en
  `nginx.conf`. Tests nuevos: heurística pura (`DynamicPricingTests`), `TicketingService` aplicando
  el precio dado un snapshot (`TicketingServiceTests`), consumer con harness in-memory de
  MassTransit (`ZoneOccupancyChangedConsumerTests`). Ticketing 18/18 en verde. ADR 0016.
  **Verificado en real con `docker compose up --build`**: compras reales vía el checkout (Saga
  completa) mueven el precio efectivo en vivo (35,8% ocupación → -10%, 76,8% → +15%, coherente con
  los tramos). De paso se encontró y corrigió un bug real: `ProvisionZonesForEventAsync` no
  notificaba el estado inicial de una zona recién creada, así que una zona virgen quedaba fuera
  del agregado de ocupación y la inflaba — corregido, cubierto con `ZoneServiceTests` nuevo en
   `AccessControl` (27/27 en verde). **Mergeado a `develop` (c30dbf5) y pusheado.**
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
- [ ] **Autenticación (decisión de alcance tomada, no tarea):** los servicios no se llaman por
  HTTP — se comunican por el bus de mensajes (RabbitMQ/MassTransit), así que no aplica OAuth
  client-credentials entre procesos; la auth del bus es la del broker (credenciales + vhost +
  TLS). El único HTTP es usuario → nginx → backend y no hay identidad de usuario. Documentado en
  `README.md` ("Autenticación entre servicios") como fuera de alcance, con nota de qué se haría
  si se desplegara público (secretos del broker, TLS en nginx, JWT/OIDC en el gateway).

## Hecho

- [x] Scaffolding, 3 microservicios, Saga con Outbox/Inbox, tests (unit + arquitectura +
  integración), frontend (shell + ticket-checkout + venue-ops), CI/CD, documentación completa.
- [x] Estructura de carpetas renombrada a `services/`/`ui/` (desde `src/`/`frontend/`).
- [x] Fix CRLF en `init-multiple-dbs.sh`, nomenclatura de contenedores Docker, limpieza de
  imágenes huérfanas.
- [x] Verificación manual contra `docker-compose` real de ambos caminos de la Saga.
