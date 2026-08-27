# TODO — VenuePulse

Espejo del task-tracker de la sesión. Actualizar al cerrar o abrir tareas.

> Trabajo repartido entre Claude Code y opencode en paralelo (varias IAs, varias ramas cortas vía
> `git worktree`, ver `.ia/GIT-FLOW.md`). Esta sección es la fuente de verdad compartida entre
> ambas — el plan detallado original de esta ronda vive además en
> `C:\Users\miguel.cidgarcia\.claude\plans\clever-percolating-cat.md` (solo accesible desde
> Claude Code), pero **este fichero es el que deben leer ambas IAs** antes de tocar código.

## Reparto en oleadas (ronda 4, precio en vivo del carrito + GUID de grupo + Groups en SignalR)

Orden acordado con el usuario: primero una pasada de limpieza previa a la entrevista (split de
componentes `tsx` grandes + retirada de comentarios `//` en `ui/`, y revisión de tono en `docs/` —
sin Bloque propio, reparto manual entre Claude Code y opencode, no repartido aquí) y **después**
estos 3 bloques. El Bloque 23 se ejecuta en la práctica dentro de esa misma pasada de limpieza de
`ticket-checkout` (toca los mismos ficheros que el split), pero mantiene su propio commit — varias
tareas lógicas, varios commits (ver `.ia/GIT-FLOW.md`).

**Criterio de comentarios en `tsx` para esa pasada (decisión explícita del usuario, solo frontend —
`RULES.md`/backend C# no cambian, siguen en "solo WHY no obvio")**: cero comentarios `//` en el
código. Primero se intenta que el propio código lo diga solo (renombrar variables/funciones o
extraer un helper con nombre claro — p. ej. `earliestExpiry` en `CartPanel.tsx` pasa a
`earliestActiveExpiry` en vez de llevar un comentario al lado). Solo si tras ese renombrado sigue
quedando una decisión real de arquitectura (no de nomenclatura) sin cubrir en ningún ADR, esa
decisión se documenta en un ADR — no se inventa un documento nuevo de "notas de frontend" aparte.
La mayoría de los comentarios actuales ya referencian una decisión cubierta por un ADR existente
(p. ej. "sin librería de iconos — ver ADR 0017") o son notas de proceso ("ver feedback de
usuario") sin valor para un lector externo — esos se borran sin más, sin destino en documentación.

Reserva de ADR (próximo libre tras `0027`): `0028` → Bloque 24 (GUID de grupo). `0029` → Bloque 25
(Groups por evento en SignalR + roadmap de backplane Redis).

- [x] **Bloque 23 — `fix/checkout-draft-live-price`** (`ticket-checkout`, Claude Code, **mergeado a
  `develop`**): el precio de
  una línea del carrito-borrador se congela en el instante de pulsar "Añadir"
  (`App.tsx:207`, `unitPrice: ticketType.effectivePrice` guardado en `DraftLine`) y ya no seguía el
  precio dinámico en vivo de `/hubs/pricing` mientras la línea seguía sin reservar — a diferencia
  del plano/popup de zona, que sí lee `ticketTypes` en cada render y por eso su precio "normal" sí
  cambia con la demanda (ADR 0016). Al pulsar "Reservar" (`App.tsx:253`) se enviaba al backend ese
  `unitPrice` ya obsoleto, no el efectivo del momento real de reserva. **No requiere pre-reserva de
  aforo** (se descartó explícitamente — contradiría el borrador puramente local sin llamar al
  backend que fijó ADR 0025, y sería la sobre-ingeniería que el proyecto evita). Fix: mientras la
  línea siga en `draft` (no reservada), su precio mostrado se deriva en cada render del
  `effectivePrice` vivo de `ticketTypes` por `ticketTypeId` (no de una copia guardada al añadir);
  solo en el instante en que `onReservar` arma la llamada real al backend se captura el precio
  vigente en ese momento, que a partir de ahí sí queda inmutable como `Ticket.Price` (ADR 0021, sin
  cambios ahí). Ficheros: `App.tsx`, `CartPanel.tsx`, `PurchaseFlow.tsx`. Sin ADR nuevo (bug fix,
  no decisión de arquitectura — mismo criterio que el Bloque 1). Build/lint en verde. **Verificación
  pendiente**: `docker compose up --build` — falta confirmar en real que el precio del borrador
  sube/baja en vivo con la ocupación antes de pulsar "Reservar".

- [x] **Bloque 24 — `feature/checkin-group-guid`** (`CheckIn`) — **absorbido y sustituido por
  `feature/checkin-purchase-order` (ADR 0028)**: el problema real detrás de este bloque (comprar
  varias entradas en una misma compra) resultó no ser solo "falta un código de grupo para
  escanear" — el usuario reportó, al revisar el flujo real, que comprar N entradas hoy dispara N
  peticiones independientes sin ninguna garantía de todo-o-nada (si 1 de 10 falla, te quedas con
  9) y sin validar que el precio siga siendo el aceptado. El `GroupId` que resuelve esto nace como
  parte de la `PurchaseOrderSaga` nueva (ADR 0028), que orquesta la reserva/pago/confirmación
  atómica de las N entradas de un pedido — el mismo `GroupId` es el código de grupo que este
  bloque quería para el escaneo conjunto. **Alcance completado (2026-08-26)**: `POST
  /api/orders/{orderId}/check-in` (`CheckInOrderCommand`, reutiliza `GetByGroupIdAsync` ya
  existente) hace check-in de todas las entradas `Confirmed` del pedido, saltando sin más las que
  ya tuvieran check-in (idempotencia por precondición de estado, no por tabla de mensajes
  procesados — mismo tipo de garantía que el Inbox pero sin necesitarlo). `venue-ops` enruta de
  forma transparente en `/venue-ops/scan`: prefijo `group-` → este endpoint, GUID suelto →
  check-in individual de siempre. Verificado en real contra el stack: ambas rutas funcionan.
  Sin ADR propio — no es una decisión de arquitectura nueva, solo cierra el hueco que ya
  documentaba ADR 0028.

- [x] **Bloque 25 — `feature/signalr-event-groups`** (los 3 hubs — `CheckIn` `/hubs/tickets`,
  `AccessControl` `/hubs/occupancy`, `Ticketing` `/hubs/pricing` —, **implementado en rama por
  opencode**): hoy los 3 hubs
  hacen broadcast a **todos** los clientes conectados sin `Groups`, así que un cliente viendo el
  evento A recibe también los mensajes del evento B (sobre-broadcast) — esto es además una causa
  plausible de ruido/confusión con el precio en vivo que motivó la aclaración del Bloque 23, aunque
  el bug de ese bloque es un problema distinto (congelado en frontend, no de scoping del hub).
  Implementado el scoping real por `Groups`: un grupo `"event-{eventId}"` por hub con métodos
  `JoinEvent`/`LeaveEvent` invocados desde el cliente (no query param — `venue-ops` usa una sola
  conexión por hub multi-evento, ADR 0022), los 4 notifiers pasan de `Clients.All` a
  `Clients.Group`, y cada app re-entra al grupo en `onreconnected` (SignalR pierde grupos al
  reconectar). Frontend: `ticket-checkout` (`useEventCatalog` occupancy+pricing,
  `useCheckoutCart` tickets) y `venue-ops` (hook compartido nuevo `useEventScopedConnection`
  centralizando join/leave por pestaña activa en `useZonesByEvent`/`useTicketActivityFeed`).
  ADR 0029 registra ambas mitades: la decisión tomada (Groups por evento, con `JoinEvent`/`LeaveEvent`
  en vez de query param y el porqué) y la descartada por
  ahora (Redis), con la condición explícita bajo la que sí se justificaría (N instancias de una
  misma API detrás de un load balancer, escenario de roadmap documentado en el README junto a
  Prometheus/Seq/Grafana). Build backend y lint/build de `ticket-checkout`+`venue-ops` en verde.
  **Verificación E2E pendiente**: `docker compose up --build` — confirmar en real que un cliente
  del evento A ya no recibe actualizaciones del evento B (aforo/precios/entradas).

- [ ] **Bloque 26 — ADR 0028, `PurchaseOrderSaga` (pedidos multi-entrada atómicos)** (Claude Code,
  **en curso**): reportado por el usuario tras probar el flujo real de compra — comprar N entradas
  dispara N peticiones independientes sin atomicidad (si 1 de 10 falla, hoy te quedas con 9) ni
  validación de que el precio siga siendo el aceptado; además una entradas huérfanas por un fallo
  de red sin capturar en el frontend explican por qué a veces "se liberan sitios sin razón" (ver
  ADR 0028 para el diagnóstico completo). Absorbe y sustituye el Bloque 24 (ver arriba). Reparto en
  3 ramas (worktrees), sin dependencia real entre sí salvo el contrato ya congelado en el plan:
  - `fix/accesscontrol-zone-rowversion` (`AccessControl`): `RowVersion` + reintento en
    `ZoneService` ante `DbUpdateConcurrencyException` — cierra el gap de concurrencia que el
    propio ADR 0027 dejó abierto.
  - `feature/checkin-purchase-order` (`CheckIn` + `Core.Messaging.Contracts`): `PurchaseOrderSaga`
    orquestadora (Mongo, sin tocar `TicketPurchaseSaga`), validación de precio síncrona contra
    `Ticketing`, `OrdersController`.
  - `feature/checkout-order-atomic` (`ticket-checkout`): `useCheckoutCart` a 1 pedido en vez de N
    peticiones sueltas, estado agregado de pedido visible en el carrito.

## Reparto en oleadas (ronda 3, gestión de eventos + rediseño Operaciones/Checkout)

Plan detallado en `C:\Users\miguel.cidgarcia\.claude\plans\hazy-stirring-melody.md` (solo
accesible desde Claude Code, igual que el plan de la ronda 2) — **este fichero es el que deben
leer ambas IAs** antes de tocar código. Objetivo: alta de eventos desde Operaciones (tabs estilo
Chrome), ciclo de vida del evento (venta abierta/cerrada/cancelada con reembolso vía RabbitMQ),
zonas más realistas (4 zonas en partido, 5 en concierto) con precio por zona, selector genérico
de "relevancia" del evento como multiplicador de precio, dinero/ingresos visibles en Operaciones,
predicción solo cuando hay ventas, rediseño de `venue-ops` (modos Ventas/Ocupación en grid) y de
`ticket-checkout` (cabecera de evento, plano en sección propia, total de pago agrupado), y delay
artificial en "Procesando pago". Decisiones ya fijadas (ver el plan para el detalle): el filtro
de "no pasados" solo aplica al catálogo de `shell` (Operaciones ve siempre todo); un evento solo
se cancela mientras está `OnSale`; set de zonas por tipo de evento resuelto al mergear con el
Bloque 15 (partido 3 zonas: `Puerta Norte`/`Puerta Sur` + `Grada VIP`; concierto 3 zonas: `Pista`
+ `Grada Norte`/`Grada Sur` — `Grada VIP`/`Pista` casan por nombre con los `TicketType`, ver
Blque 16 en Oleada 1); Mongo sigue reservado solo para el estado de la Saga, el
dinero vive en `Ticket.Price` (CheckIn).

> **Actualización (Bloque 21, ADR 0024):** el set de 3 zonas de partido de este párrafo quedó
> revertido — ahora son 4 (`Puerta Norte`/`Puerta Sur` + `Grada Este VIP`/`Grada Oeste VIP`), a
> petición directa del usuario. Concierto no cambia.

> **Actualización (ADR 0026):** el set de 3 zonas de concierto de este párrafo también quedó
> revertido — ahora son 6 (`Grada 1`/`Grada 2`/`Grada 3` + `Pista` + `VIP 1`/`VIP 2`, precio
> Grada < Pista < VIP), a petición directa del usuario. Partido no cambia respecto a la
> actualización anterior. `StadiumMap` (ambas apps) pasa a un óvalo exterior compartido entre
> partido y concierto, con las 3 gradas de concierto reutilizando literalmente las curvas de
> Norte/Sur/Oeste de partido.

Reserva de ADR para esta ronda (próximo libre tras `0018`): `0019` → Bloque 15 (Ticketing,
Claude Code). `0020` → Bloque 16 (AccessControl, opencode, 2 sub-fases). `0021` → Bloque 17
(CheckIn, Claude Code). `0022` → Bloque 18 (venue-ops, Claude Code). `0023` → Bloque 19
(ticket-checkout, opencode). Bloque 20 (shell, opencode, se cuela en la rama del 19) sin ADR.

- **Oleada 1 (en paralelo desde ya)**:
  - [x] Claude Code → **Bloque 15** `feature/event-lifecycle-pricing` (`Ticketing`, **mergeado a
    `develop`**): `EventStatus` (`OnSale`/`SalesClosed`/`Cancelled`, transiciones solo válidas
    desde `OnSale`) y `EventRelevance` (`Standard`/`Special` ×1.5) en `Event`. `POST /api/events`
    (alta desde Operaciones, primer endpoint de escritura de `Event`, mismo patrón
    Add→Publish→SaveChanges que ya usa CheckIn), `POST /events/{id}/close-sales`,
    `POST /events/{id}/cancel` (publica `EventCancelled` nuevo en
    `Core.Messaging.Contracts.EventLifecycleMessages.cs`, todavía sin consumers — los añaden los
    Bloques 16/17). `EventSummaryDto` con `Status`/`Relevance`, relevancia aplicada antes y fuera
    de `DynamicPricing` (sin tocar ese fichero). `DefaultTicketTypeCatalog` con los tipos de
    entrada por defecto por `EventType` (partido: `General`/`VIP`; concierto: `Pista VIP`/`Pista`/
    `Grada Frontal`/`Grada Lateral`), migración `AddEventLifecycleAndRelevance` (aditiva). ADR
    0019. Tests: Ticketing 33/33 (harness in-memory de MassTransit para `IPublishEndpoint` real),
    sin regresión en el resto (CheckIn 15/15, AccessControl 39/39, Architecture 7/7). **Verificado
    en real con `docker compose up --build`**: alta/cierre/cancelación de evento y precio efectivo
    con relevancia aplicada (95€→128,25€ con `Special` ×1.5 y tier "Demanda baja" -10%) probados
    contra el gateway real, sin romper el pipeline existente de zonas/pricing dinámico.
  - [x] opencode → **Bloque 16** `feature/access-control-cancellation` (`AccessControl`, 2
    sub-fases, **mergeado a `develop`**):
    - **Sub-fase A** (zonas realistas por tipo de evento): se implementó 4 en partido/5 en
      concierto, pero al mergear con el Bloque 15 se resolvió **a favor del set de develop**
      (3+3, ver commit `fix: keep develop zone set after merge`): partido `Puerta Norte/Sur`(40)
      + `Grada VIP`(15); concierto `Pista`(60) + `Grada Norte/Sur`(20) — `Grada VIP`/`Pista`
      casan por nombre con los `TicketType` de Ticketing para que el frontend fusione zona+tipo
      sin relación cruzada en el backend. Sin ADR propio (las decisiones de zonas quedan en el
      plan y en el comentario de `ZoneService.cs`).
    - **Sub-fase B** (cancelación, ADR 0020): `Zone.Cancelled` (flag idempotente, guarda solo
      con gente dentro), `CancelEventZonesAsync` + consumer de `EventCancelled`,
      `ReleaseConfirmedZoneSlot` (nuevo contrato, decrementa `ConfirmedSold` ticket a ticket;
      lo publica CheckIn en el Bloque 17) + consumer, `TryReserveSlotAsync` rechaza la reserva
      en zona cancelada, migración aditiva `AddZoneCancelled`, `ZoneOccupancyDto.Cancelled`
      expuesto (TS de `venue-ops`/`ticket-checkout` actualizado). Tests: AccessControl 46/46,
      sin regresión (Ticketing 33/33, CheckIn 15/15, Architecture 7/7).
- **Oleada 2 (tras merge de Bloque 15 y 16-A a `develop`)**:
  - [x] Claude Code → **Bloque 17** `feature/checkin-revenue-cancellation` (`CheckIn`, **mergeado a
    `develop`**).
    **Implementado en rama** (Bloque 16 ya mergeado completo — sub-fases A y B — antes de empezar,
    así que `EventCancelled` y `ReleaseConfirmedZoneSlot` ya tenían consumer al otro lado):
    `Ticket.Price` (decimal inmutable, fijado en la compra sin revalidar contra `Ticketing`,
    migración aditiva `AddTicketPrice`); `TicketStatus` gana `ProcessingPayment` (transitorio,
    entre `AwaitingPayment` y `Confirmed`/`Rejected`) y `Refunded` (terminal, distinto de
    `Rejected` porque la entrada sí llegó a completarse). Cascada de cancelación
    (`EventCancelledConsumer`, nunca muta el `Ticket` — solo publica `TicketRefunded` para
    `Confirmed` o `TicketPurchaseCancelled` para `Pending`/`AwaitingPayment`; la mutación la hace
    `TicketRefundedConsumer` o la propia Saga) más dos ramas nuevas en `TicketPurchaseSaga`
    (`When(Cancelled)` en `Reserving` y en `AwaitingPayment`, reutilizando el mismo camino de
    resolución que ya usaban las ramas `TimedOut`; `Ignore(Cancelled)` en `Confirming` para la
    ventana de carrera con un pago ya en curso). Delay configurable de "Procesando pago"
    (`Saga:PaymentProcessingDelaySeconds`, default 3s, publicando `TicketProcessingPayment` antes
    del `Task.Delay` dentro del propio `ThenAsync`, a 0 en tests). `GET /api/tickets/events/{id}/revenue`
    agregando `Ticket.Price` por estado (confirmados vs. pendientes; `Refunded`/`Rejected` fuera
    por construcción). ADR 0021. Tests: 101/101 en verde (unitarios de dominio/Saga + integración
    de extremo a extremo con Testcontainers, incluida la cascada de cancelación real), sin
    regresión en el resto del solution.
  - [x] opencode → **Bloque 16, sub-fase B** (`AccessControl`, cancelación) — **mergeado a
    `develop`**, ver Oleada 1.
- **Oleada 3 (en paralelo desde ya)** — solo frontend, apps independientes sin build
  compartido (ADR 0004), sin riesgo de choque entre IAs:
  - [x] Claude Code → **Bloque 18** `feature/venue-ops-tabs-redesign` (`venue-ops`,
    **implementado en rama**): `EventTabs` (pestañas estilo Chrome + pestaña `+` fija, insignia si
    `Status !== OnSale`) sustituye al `<select>`, sin filtro de "pasados" (Operaciones ve siempre
    todo). `EventCreateForm` (nombre/tipo/fecha/relevancia) sobre `POST /api/events` del Bloque 15;
    si el catálogo está vacío la app aterriza directamente en el formulario. Carga perezosa por
    pestaña: `App` mantiene un único socket de cada hub (`/hubs/occupancy`, `/hubs/tickets`) y
    reparte los mensajes por `eventId` en un diccionario, pero solo pide zonas/ingresos la primera
    vez que se activa una pestaña; solo la pestaña activa monta su `EventWorkspace` (evita sondeos
    de `SimulationPanel` en segundo plano en pestañas ya visitadas). `ModeToggle` (Ventas/Ocupación)
    decide el grid dentro de la pestaña: Ventas trae `SimulationPanel` (fase `Sales`),
    `RevenueWidget` (nuevo, sobre el endpoint de ingresos del Bloque 17), `ActivityFeed`,
    `StadiumMap` coloreado por venta y los botones "Cerrar venta"/"Cancelar evento" (solo si
    `OnSale`, con confirmación); Ocupación trae `ZonesDashboard` (ya sin plano embebido),
    `SimulationPanel` (fase por defecto `Arrivals` en cuanto el evento deja `OnSale`, vía
    `defaultMode` + `key` para remontar sin esperar a un cambio de modo manual), `ScanPanel`,
    `StadiumMap` coloreado por check-in y `PredictionPanel` condicionado a
    `zones.some(confirmedSold > 0)` (**no** a los ingresos de `CheckIn` — verificado en real que el
    motor de simulación, camino principal de la demo, mueve `ConfirmedSold` sin pasar nunca por
    `CheckIn`, así que condicionar a `revenue.confirmedCount` dejaba el panel sin montarse jamás en
    una demo solo-simulador). `StadiumMap` gana la prop `mode: 'sales' | 'checkin'` en vez de
    duplicarse. "Cerrar venta"/fase de simulación siguen desacoplados a propósito (mismo criterio
    que ADR 0019), el único puente es ese `defaultMode` en el frontend. ADR 0022. Build (`tsc -b` +
    `vite build`) y lint (`oxlint`) en verde. **Verificado en real con `docker compose up --build`**
    (Playwright headless contra el gateway): alta de evento desde la pestaña `+` con sus zonas
    apareciendo en vivo tras el aprovisionamiento asíncrono, `ModeToggle` Ventas/Ocupación,
    simulación de ventas+llegadas con `PredictionPanel` apareciendo correctamente tras el fix de
    esta misma verificación, y "Cerrar venta" (insignia en la pestaña, botones ocultos, fase por
    defecto del simulador en Ocupación pasando a `Arrivals`). **Ampliación pedida en la propia
    verificación** (0€ de ingresos con el simulador "parece roto" en el camino principal de la
    demo): `RevenueWidget` gana una tercera cifra `estimatedRevenue` = Σ `zone.confirmedSold` ×
    `ticketType.effectivePrice` (nuevo `ui/venue-ops/src/zoneTicketType.ts`, fusión zona↔tipo de
    entrada por nombre igual que `ticket-checkout` pero con una regla más robusta —
    `zoneName.includes(ticketType.name)`, no solo igualdad exacta), etiquetada como estimación y
    nunca mezclada con los dos números reales. Verificado en real contra "Derbi de la ciudad": 14
    Grada VIP × 234€ + 37 Puerta Norte × 58,50€ + 38 Puerta Sur × 58,50€ = 7.663,50€, coincide
    exacto. **Bug real encontrado de paso en `ticket-checkout` (no corregido desde esta rama, ver
    `.ia/KNOWN-ISSUES.md`)**: su fusión zona↔tipo por igualdad exacta nunca encuentra `Grada VIP` →
    `VIP`, cae a `General` — un fan que compra ahí vería y pagaría el precio de `General`. **Sin
    probar**: cancelación con reembolso real de principio a fin — exigiría una compra real vía
    `ticket-checkout`, app que opencode está tocando en paralelo sobre la misma base de datos
    compartida del stack; se deja para la verificación conjunta de ronda que se hará al mergear con
    los Bloques 19/20.
  - opencode → **Bloque 19 + 20** `feature/checkout-header-total` (`ticket-checkout` + `shell`,
    misma rama): `CheckoutHeader` con info de evento + botón volver (evento `CustomEvent` hacia
    `shell`), plano en sección propia, total visible en el botón de pago (el pago ya se agrupa a
    nivel funcional), filtro de "no pasados" en el catálogo de `shell`.
    **Implementado en rama** (ADR 0023): `CheckoutHeader` obtiene el evento con `GET /api/events`
    (sin endpoint nuevo) y despacha `CustomEvent('back-to-catalog', {bubbles, composed})` que
    `CheckoutPanel` (shell) escucha — en standalone, `main.tsx` escucha y recarga sin `eventId`.
    El plano vive en su propia sección más amplia (CSS/grid, sin cambios de props) y el botón de
    pago muestra "Pagar todo — {total} €". Si el evento no está `OnSale`, la compra se sustituye
    por un aviso. `shell`: `EventSummary` gana `Status`/`Relevance`; el catálogo oculta
    `Cancelled` y `SalesClosed` con fecha ya alcanzada, y un `SalesClosed` con fecha futura se
    muestra con badge "Ventas cerradas" y botón deshabilitado. De paso, al verificar el
    catálogo de `TicketType` contra las zonas 3+3 se detectó y corrigió que estaba desalineado
    (partido `General`/`VIP`, concierto `Pista VIP`/`Pista`/`Grada Frontal`/`Grada Lateral` →
    alineado con ADR 0018: partido `General`/`Grada VIP`, concierto `Pista`/`Grada Norte`/
    `Grada Sur`), con tests de Ticketing actualizados.
- **Diferido al final, sin bloquear nada**: SVG del `StadiumMap` más realista visualmente
  (asientos, escenario) — pospuesto explícitamente por el usuario.
- [x] **Bloque 21 — `feature/match-map-4-zones`** (Claude Code, petición directa del usuario tras
  cerrar la ronda 3): **revierte la decisión de 3 zonas de partido del Bloque 15/16** — vuelve a
  4 zonas reales y comprables (`Puerta Norte`/`Puerta Sur` + `Grada Este VIP`/`Grada Oeste VIP` en
  vez de una `Grada VIP` única), con `DefaultTicketTypeCatalog` de partido pasando de 2 a 3 tipos
  a juego. `StadiumMap` de `ticket-checkout` (partido) rediseñado a petición del usuario: fondo
  gris de estadio, campo verde con líneas realistas (áreas, círculo central, punto de penalti,
  decorativo), y las 4 gradas en azul fijo con una luz de estado pequeña y parpadeante (CSS) en
  vez del semáforo verde/naranja/rojo que sigue usando el concierto (sin tocar, rediseño propio
  pendiente). `StadiumMap` de `venue-ops` solo actualiza coordenadas de las 4 zonas nuevas, sin
  tocar su semáforo. `CheckoutHeader` pasa a fila con el botón "Volver al catálogo" pegado a la
  derecha. ADR 0024. Build/tests de `ticket-checkout`/`venue-ops` en verde; tests de
  `AccessControl`/`Ticketing` actualizados a las 4/3 zonas y tipos nuevos.
- [x] **Bloque 22 — `feature/checkout-reserva-diferida`** (Claude Code, petición directa del
  usuario): invierte qué par de gradas es la VIP (`Grada VIP Norte`/`Grada VIP Sur`, 8/7 plazas,
  90€, sustituyen a `Puerta Norte`/`Sur`; `Grada Este`/`Grada Oeste`, 40/40 plazas, 40€ vía
  `General`, sustituyen a las VIP laterales) y alinea la geometría del plano exactamente al campo
  (Norte/Sur a su ancho, Este/Oeste a su alto), con el nombre de zona en varias líneas sin rotar
  cuando el hueco es estrecho. `ticket-checkout` pasa a 3 tarjetas (evento, plano con icono
  "Mapa del Evento", compra) y a un carrito **borrador** local (sin llamar al backend) que se
  agrupa por zona ("Grada VIP Norte × 5 — 450,00 €") bajo el título "Tus Reservas"; solo al
  pulsar "Reservar" se reserva de verdad cada unidad (título pasa a "Tu compra", aparece el
  contador y el semáforo del plano cambia de color). Nombre del comprador pedido una vez, junto al
  botón "Reservar". Contador de reserva real: `ReservationTimeoutTracker` (CheckIn) gana
  `TryGetDeadline` tras el puerto `IReservationTimeoutReader` (Application), `TicketStatusDto`
  gana `ExpiresAtUtc` propagado por SignalR y por `GET /status`; frontend con `useCountdown`.
  ADR 0025 (reabre ADR 0018 punto 3 y ADR 0024). Build backend + frontend (`ticket-checkout`) en
  verde. **Pendiente de esta misma tarea** (fuera de alcance a petición explícita): GUID de grupo
  compartido para validar de una vez una compra múltiple — ver sección "Pendiente de decidir".

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
**`0018` → Bloque 14 (Claude Code, ya usado — ver
`docs/architecture/adr/0018-carrito-saga-partida-tipo-evento.md`).**
Próximo libre para nuevas ramas: `0019`.

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
- [x] **Bloque 12 — `feature/ui-tailwind`** (Claude Code): Tailwind v4 + `@tailwindcss/vite` ya
  montado en las 3 apps, tokens `--vp-*`/`--tc-*` mapeados a `@theme` (conviven con el CSS BEM
  existente en `shell`/`ticket-checkout`, sin tocar). Todo lo **nuevo/reescrito** en el Bloque 14
  (catálogo, carrito, popup, formulario de pago, plano, feed de actividad) ya usaba Tailwind
  directamente. **Cerrado en esta pasada** (ampliación de alcance sobre lo que decía este párrafo:
  al revisar `venue-ops/src` completo aparecieron más componentes en BEM de los que constaban aquí,
  añadidos en bloques posteriores al 14/18/21/22): `SiteHeader`, `ScanPanel`, `SimulationPanel`/
  `SimulationControls`, `PredictionPanel`/`PredictionChart`, `ZoneGauge` (el subconjunto original
  de este párrafo) + `ActivityFeed`, `EventCreateForm`, `EventList`, `PhaseIndicator`,
  `RevenueWidget`, `SalesLayout`, `EventWorkspace`, `OccupancyGrid`, `ZonesDashboard`,
  `StadiumMap`/`StadiumBackground`/`ZoneShape`, `Modal`, `App` (layout raíz). `venue-ops/App.css`
  queda solo con `.card` (único primitivo compartido que se mantiene a propósito, igual que en el
  resto de migraciones de esta ronda, en vez de repetir sus 4 utilidades en cada sitio que lo usa);
  `ops-detail`/`ops-new-event`/`sales-layout__panel` (duplicaban exactamente el estilo de `.card`)
  se reemplazan por `card` en vez de reescribirse en Tailwind. `index.css` gana el alias
  `--color-blue`/`--color-blue-hover` (patrón ya existente, faltaba ese token). Build (`tsc -b` +
  `vite build`) y lint (`oxlint`) en verde; verificado que ninguna clase BEM eliminada sigue
  referenciada en ningún `.tsx` (grep exhaustivo). **Verificado visualmente** con Chrome headless
  (estado vacío de `venue-ops`: cabecera, listado de eventos, tarjeta de detalle) — sin backend
  levantado, así que `ZoneGauge`/`ActivityFeed`/`StadiumMap`/`Modal`/`PhaseIndicator`/
  `RevenueWidget`/`PredictionPanel` (dependen de datos reales de evento) **quedan sin comprobación
  visual real**, pendiente de una pasada con `docker compose up --build`. `shell`/`ticket-checkout`
  no tocados en esta pasada (siguen en BEM, fuera de alcance de este bloque desde el principio).
- [x] **Bloque 14 — `feature/checkout-cart-tailwind`** (Claude Code, ampliación de alcance pedida
  al revisar el Bloque 13 ya mergeado): más realismo en la compra y en operaciones, resuelto sin
  sobreingeniería. Cambios:
  - **`EventType` (`Match`/`Concert`)** en `Event` (Ticketing), propagado en `EventCreated`;
    `AccessControl` aprovisiona un set de zonas distinto por tipo (partido: `Puerta Norte/Sur` +
    `Grada VIP`; concierto: `Pista` + `Grada Norte/Sur`) — los nombres de la zona "especial" de
    cada tipo coinciden a propósito con su `TicketType` correspondiente.
  - **Saga de compra partida en tres fases reales** (`Reserving` → `AwaitingPayment` →
    `Confirming`), no dos: el aforo se reserva de verdad antes de saber el método de pago, en vez
    de decidir todo en una sola llamada. Nuevo endpoint `POST /api/checkin/api/tickets/{id}/pay`
    (`Card`/`Bizum`) y evento `TicketPaymentSubmitted`. `Ticket` gana el estado real
    `AwaitingPayment` (no solo interno de la Saga). El watchdog de timeout (ADR 0010) se rearma
    para vigilar también esta espera. Es más fiel al patrón Saga, no una desviación.
  - **Carrito multi-entrada**: clic en zona del plano → popup (precio tachado/`effectivePrice`
    destacado, tier, aforo, cantidad) → "Añadir" dispara ya la compra real por unidad (el aforo se
    bloquea al instante, no al final). Límite de 10 entradas por compra. Nombre del comprador una
    vez al principio (lo exige el backend al crear cada entrada); apellidos/correo en el paso de
    pago son solo visuales. Tarjeta/Bizum decide el resultado de cada línea en `AwaitingPayment`.
  - **Fusión zona↔tipo de entrada solo en frontend**, por coincidencia de nombre — sin relación
    real en el backend (mismo criterio que ADR 0016).
  - **`StadiumMap`** (ambas apps) elige plantilla mirando los propios nombres de zona (zona
    `"Pista"` → concierto: pista + escenario decorativo; si no → partido).
  - **`venue-ops`**: plano coloreado por ocupación de **venta** (`capacity - availableToSell`), no
    por `checkedIn` (que no se mueve durante la fase de venta); nuevo panel "Actividad en vivo"
    (`ActivityFeed`) suscrito a `/hubs/tickets`. `TicketStatusDto` gana `EventId`/`ZoneId`.
  - `shell`: catálogo de eventos rediseñado (badge de fecha, fila con hora/tipo/título/subtítulo,
    botón "Entradas"), filtro de fechas. `OccupancyWidget` ("Aforo en vivo") retirado — el plano
    interactivo del checkout lo sustituye mejor.
  ADR 0018. Build/lint/tests unitarios en verde en las 3 apps + backend (79 tests .NET). Pendiente
  la verificación E2E real con `docker compose up --build`.
- [x] **Bloque 13 — `feature/purchase-wizard-venue-map`** (Claude Code, nuevo — ampliación de
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
  ADR 0017. Mergeado `develop` (Bloque 11) dentro de esta rama antes de cerrarla, sin conflictos
  reales. **Bug real encontrado y corregido durante la verificación manual en real** (no en
  standalone): `ticket-checkout.css` nunca se enlazaba en la página cuando `ticket-checkout` se
  carga como Web Component remoto dentro de `shell` (el flujo real de compra) — solo la propia
  `index.html` de `ticket-checkout` en modo standalone lo enlazaba. Pasaba desapercibido porque
  `<select>`/`input`/`button` heredaban por accidente los estilos globales de `shell` (sin Shadow
  DOM, ADR 0004); el SVG del plano, en cambio, no tenía ningún estilo de respaldo y caía a negro.
  Corregido en `main.tsx` inyectando el `<link>` al propio CSS (`new URL('./ticket-checkout.css',
  import.meta.url)`) en `connectedCallback`. Verificado con Chrome headless (CDP) contra el flujo
  real embebido en `shell`, no solo el modo `?eventId=` standalone.
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
- [x] Verificar manualmente desde el navegador (no solo API) el camino feliz y el de
  compensación, con el timeline de la Saga por SignalR — único ítem sin marcar en el checklist
  de `docs/demo-script.md`. **Verificado el canal (SignalR vía gateway, camino feliz y
  compensación) y, el 2026-08-26, la visual del timeline en el navegador contra `develop` real
  (ver `.ia/KNOWN-ISSUES.md`). Cerrado.**
- [x] **Timeline de la Saga — ¿"en vivo" real o estado final?** (RESUELTO): la partición de la
  Saga en `Reserving → AwaitingPayment → Confirming` (ADR 0018) añadió el paso intermedio real;
  re-verificado en el navegador el 2026-08-26 que anima en vivo (ver `.ia/KNOWN-ISSUES.md`).

**Verificado durante la revisión (sin acción):** MediatR excluido de `Ticketing`/`AccessControl`
es la decisión correcta (ADR 0003, forzado por `Architecture.Tests`); FluentAssertions ya se usa en
los tests de los 3 servicios (ADR 0008 se cumple); el modelo `Zone` (`Capacity`/`Reserved`/
`ConfirmedSold` vs `CheckedIn`) ya es coherente — lo único que faltaba era el scoping por evento
(Bloque 1).

## Pendiente de decidir

- [ ] **GUID de grupo para compras múltiples** — promovido a **Bloque 24** (ver "Reparto en oleadas
  ronda 4" más arriba), fuera de alcance del Bloque 22 a petición explícita del usuario.
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
