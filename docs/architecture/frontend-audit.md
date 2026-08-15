# Auditoría frontend — Microfrontends y arquitectura compartida

> Auditoría de los 3 microfrontends (`venue-shell`, `venue-ticket`, `venue-ops`) y del paquete
> compartido existente (`venue-core`). Resultado: plan de extracción de un core compartido
> estructurado en carpetas y decisión de arquitectura en ADR 0034.
>
> Modo: `[ARCH]`/`[REVIEW]`. Esta rama (`feature/frontend-shared-architecture`) contiene la
> auditoría, el ADR y la primera fase de implementación (paquetes compartidos).

## 1. Estado actual

| App | Paquete | Papel | Compositor |
|---|---|---|---|
| `venue-shell` | `@venue/shell` | Catálogo de eventos + panel de compra (público) | app standalone, inyecta `ticket-checkout.js` como Web Component |
| `venue-ticket` | `@venue/ticket` | Checkout/compra (remoto de shell) | Web Component light DOM, CSS escopado `.ticket-checkout` |
| `venue-ops` | `@venue/ops` | Operaciones internas del recinto | app standalone independiente |
| `venue-core` | `@venue/core` | Utilidades SignalR | consumido por `venue-ticket` y `venue-ops` |

Stack: React 19, Vite 8, TypeScript ~6.0 strict, Tailwind v4 (en `venue-ops`), CSS BEM
(`venue-shell`/`venue-ticket`), oxlint. Sin librería de estado global, sin librería de datos
server-state, sin sistema de diseño compartido, sin lazy loading / Suspense / Error Boundaries.

Comunicación entre microfrontends hoy: `venue-shell` pasa `event-id` por atributo del Web
Component y recibe `CustomEvent('back-to-catalog')`. Sin estado compartido entre bundles.

## 2. Duplicidades detectadas (dominio)

### 2.1 Tipos duplicados verbatim en las 3 apps

| Tipo | shell | ticket | ops | Notas |
|---|---|---|---|---|
| `EventType` | ✓ | ✓ | ✓ | idéntico (`'Match' | 'Concert'`) |
| `EventStatus` | ✓ | ✓ | ✓ | idéntico (`'OnSale' | 'SalesClosed' | 'Cancelled'`) |
| `EventSummary` | ✓ | ✓ | ✓ | idéntico |
| `EventRelevance` | inline | — | ✓ | shell lo inlne en `EventSummary`, ops tiene tipo propio |
| `TicketType` | — | ✓ | ✓ | **divergen**: ticket tiene `basePrice`, ops no |
| `ZoneOccupancy` | — | ✓ | ✓ | idéntico |
| `TicketStatus` | — | ✓ | ✓ | **divergen**: ticket tiene `ProcessingPayment`, ops no |
| `TicketStatusUpdate` | — | ✓ | ✓ | **divergen**: ticket tiene `expiresAtUtc`, ops tiene `eventId`+`zoneId` |

### 2.2 Tipos solo-en-una-app (a centralizar como vocabulario de dominio)

- `venue-ticket`: `OrderLine`, `OrderStatus`, `OrderTicketStatus`, `OrderStatusUpdate`, `CartLine`,
  `DraftLine`, `PaymentMethod`, `BuyerData`, `MAX_TICKETS_PER_PURCHASE`.
- `venue-ops`: `CreateEventRequest`, `EventRevenueDto`, `SimulationRunStatus`,
  `SimulationStartRequest`, `PredictionPoint`, `OccupancyPrediction`, `ScanResult`,
  `SimulationMode`, `VenueOpsMode`.

### 2.3 Lógica duplicada

- `matchTicketType` (fusión zona↔tipo de entrada): **dos reglas distintas**
  (`venue-ticket/src/shared/zoneTicketType.ts` igualdad exacta → `General` → primero;
  `venue-ops/src/shared/zoneTicketType.ts` exacta → contiene (la más larga) → `General` → primero).
  La regla de `venue-ops` es un superconjunto de la de `venue-ticket` (KNOWN-ISSUES: la alineación
  de nombres en `DefaultTicketTypeCatalog` hace que la exacta funcione, pero la regla robusta las
  cubre también). **Se unifica a la regla robusta.**
- `pricingByZoneId`: solo en `venue-ticket`, vive junto a `matchTicketType`.
- `occupancyRatio` + `occupancyTier`: duplicados en ambos `StadiumMap`.
- Geometría SVG del plano (`standShapes.ts`, `StadiumBackground.tsx`): duplicada en `venue-ticket`
  y `venue-ops` con coordenadas casi idénticas y ajustes de label específicos por app.
- URLs de API: `/api/ticketing`, `/api/access-control`, `/api/checkin` hardcodeadas en todas las
  apps y en los `vite.config.ts` de proxy.

## 3. Estado global y comunicación

- Todo el estado es `useState` local en cada `App` y hooks; **prop drilling alto** en
  `venue-ticket`: `App → PurchaseFlow → CartPanel → DraftCart/ReservedCartSummary` (15+ props de
  estado/acciones del pedido). Mismo patrón en `venue-ops App → EventWorkspace → ...`.
- `useCheckoutCart` orquesta estado del pedido + SignalR + `sessionStorage` + polling de
  reconciliación en un solo hook de ~350 líneas.
- Comunicación cross-app solo por `CustomEvent`/atributos. Sin estado compartido.
- Al no haber Module Federation (ADR 0004), cada bundle lleva su propia copia de las librerías; un
  store compartido real entre microfrontends debe apoyarse en un singleton global (`globalThis`)
  con sync por `BroadcastChannel`.

## 4. Gestión de datos (API)

- `fetch` crudo en todas las apps, sin cliente tipado, sin manejo uniforme de errores (muchas
  llamadas no comprueban `response.ok`), sin cancelación, sin retry, sin caché.
- `venue-ops` hace polling con `setInterval`: ingresos (3s), precios (3s), simulación (500ms).
- Realtime por SignalR vía `@venue/core` (`createHubConnection`, `useEventScopedConnection`).

## 5. Persistencia

- Solo `useCheckoutCart` persiste (el `orderId` en `sessionStorage`, a mano, con key
  `venuepulse.ticket-checkout.order.{eventId}` y try/catch repetido). Sin capa de storage.

## 6. Diseño/UI

- Sin sistema de diseño. Cada app define sus tokens y CSS por separado (`.ticket-checkout` BEM,
  `venue-ops` Tailwind). `Modal.tsx` existe solo en `venue-ops`. `SiteHeader` duplicado entre
  `venue-shell` y `venue-ops`.
- **Decisión de alcance del propietario: NO se construye librería de componentes atómica
  (Button/Input/...) — la granularidad UI compartida se queda fuera; el core compartido es de
  dominio/estado/datos/infra, no de presentación.**

## 7. Plan de extracción (core compartido)

**Decisión del propietario: no se crean paquetes nuevos — todo el core compartido vive en
`venue-core` (`@venue/core`), ya existente, estructurado en carpetas por contexto y expuesto con
subpaths de `exports`.** Cada área es un directorio con su propio barril:

```
ui/venue-core/
  src/
    hubConnection.ts            (existente, SignalR)
    hubErrors.ts                (existente)
    useEventScopedConnection.ts (existente)
    domain/                     @venue/core/domain   — modelo de dominio compartido
      events/                   EventType, EventStatus, EventRelevance, EventSummary, CreateEventRequest
      zones/                    ZoneOccupancy, occupancyRatio/occupancyTier, matchTicketType, pricingByZoneId
      tickets/                  TicketStatus, TicketStatusUpdate, PaymentMethod, BuyerData, CartLine, DraftLine
      orders/                   OrderLine, OrderStatus, OrderTicketStatus, OrderStatusUpdate, isResolvedOrderStatus
      occupancy/                OccupancyStatus, SeatStatus, SeatAvailability (estados de aforo/asiento)
      ticketing/                TicketType, PricingChanged, EventRevenueDto
      simulation/               SimulationRunStatus, SimulationStartRequest, PredictionPoint, OccupancyPrediction
      common/                   constantes (MAX_TICKETS_PER_PURCHASE) y guards
      index.ts
    api/                        @venue/core/api      — cliente HTTP tipado, errores, keys, rutas
      http/                     HttpClient, ApiError, ApiResponse
      keys/                     queryKeys, mutationKeys
      routes/                   rutas de backend (/api/ticketing, /api/access-control, /api/checkin)
      index.ts
    storage/                    @venue/core/storage  — capa de persistencia + hooks
      services/                 StorageService, SessionStorageService, LocalStorageService, MemoryStorageService
      hooks/                    useLocalStorage, useSessionStorage, usePersistedState
      index.ts
    state/                      @venue/core/state    — Zustand: stores con singleton global
      registry/                 createSharedStore (globalThis + BroadcastChannel)
      stores/                   eventSelectionStore, occupancyStore, orderStore
      index.ts
    index.ts                    barril raíz: re-exporta SignalR (compatibilidad, sin breaking change)
```

`package.json` de `venue-core` gana los subpaths de export (`"."`, `"./domain"`, `"./api"`,
`"./storage"`, `"./state"`) y la dependencia `zustand`. Las apps importan solo lo que necesitan
(`@venue/core/domain`, etc.), así que Vite no incluye Zustand en apps que solo usan dominio.

Fases: (1) `domain` + migración de tipos en las 3 apps; (2) `api` + `storage` y adopción donde
eliminan duplicación; (3) `state` (Zustand) para el estado de pedido (prop drilling de
`venue-ticket`) y selección de evento; (4) siguiente ronda: geometría del plano y tokens de
diseño si se demuestra necesidad.

## 8. Mapa de correcciones que esta rama introduce

| Duplicidad | Acción | Fichero destino |
|---|---|---|
| `EventType/EventStatus/EventRelevance/EventSummary` ×3 | extraer + importar | `venue-core/src/domain/events` |
| `TicketType` ×2 (divergente) | extraer unión, importar | `venue-core/src/domain/ticketing` |
| `ZoneOccupancy` ×2 | extraer + importar | `venue-core/src/domain/zones` |
| `TicketStatus`/`TicketStatusUpdate` ×2 (divergentes) | extraer unión, importar | `venue-core/src/domain/tickets` |
| tipos de pedido (solo ticket) | mover a dominio compartido | `venue-core/src/domain/orders` |
| DTOs de ops (revenue, simulación, predicción) | mover a dominio compartido | `venue-core/src/domain/ticketing`, `venue-core/src/domain/occupancy`, `venue-core/src/domain/simulation` |
| `matchTicketType` ×2 (reglas distintas) | unificar a regla robusta | `venue-core/src/domain/zones/zoneTicketType.ts` |
| `pricingByZoneId` (solo ticket) | mover | `venue-core/src/domain/zones/zoneTicketType.ts` |
| `occupancyRatio`/`occupancyTier` ×2 | extraer | `venue-core/src/domain/zones/occupancy.ts` |
| URLs de API | centralizar | `venue-core/src/api/routes` |
| `sessionStorage` a mano | capa de storage | `venue-core/src/storage` |
| Prop drilling de pedido (ticket) | Zustand | `venue-core/src/state/stores/orderStore.ts` |
