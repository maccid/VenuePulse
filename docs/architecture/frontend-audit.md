# Auditoría frontend — Microfrontends y arquitectura compartida

> Auditoría de los 3 microfrontends (`venue-shell`, `venue-ticket`, `venue-ops`) y del paquete
> compartido (`venue-core`). El plan (abajo) se ha implementado en la rama
> `feature/frontend-shared-architecture` y está **mergeado en `develop` (commit `6954659`)**.
> Este documento describe el estado resultante y lo que queda pendiente.
>
> Modo: `[ARCH]`/`[REVIEW]` (auditoría) + `[DEV]` (implementación).

## 1. Estado actual

| App | Paquete | Papel | Compositor |
|---|---|---|---|
| `venue-shell` | `@venue/shell` | Catálogo de eventos + panel de compra (público) | app standalone, inyecta `ticket-checkout.js` como Web Component |
| `venue-ticket` | `@venue/ticket` | Checkout/compra (remoto de shell) | Web Component light DOM, CSS escopado `.ticket-checkout` |
| `venue-ops` | `@venue/ops` | Operaciones internas del recinto | app standalone independiente |
| `venue-core` | `@venue/core` | Core compartido (dominio, api, hub, storage, state, map, componentes) | consumido por las 3 apps |

Stack: React 19, Vite 8, TypeScript ~6.0 strict, Tailwind v4 (en las 3 apps; `venue-core` lo
consume vía `@source` sin build propio), oxlint. Estado global de pedido con Zustand
(`@venue/core/state`). Sin librería de server-state, sin lazy loading / Suspense / Error Boundaries.

Comunicación entre microfrontends hoy: `venue-shell` pasa `event-id` por atributo del Web
Component y recibe `CustomEvent('back-to-catalog')`. Sin Module Federation (ADR 0004); un store
compartido real entre bundles se apoya en un singleton en `globalThis` (ver §3).

## 2. Duplicidades detectadas (dominio) — estado

### 2.1 Tipos duplicados verbatim en las 3 apps → resueltos

| Tipo | Estado | Destino |
|---|---|---|
| `EventType` / `EventStatus` / `EventRelevance` / `EventSummary` | **extraído** | `venue-core/src/domain/events.ts` |
| `TicketType` (divergente) | **extraído** | `venue-core/src/domain/ticketing.ts` |
| `ZoneOccupancy` | **extraído** | `venue-core/src/domain/zones.ts` |
| `TicketStatus` / `TicketStatusUpdate` (divergentes) | **extraído** (unión con `ProcessingPayment`, `expiresAtUtc`) | `venue-core/src/domain/tickets.ts` |

### 2.2 Tipos solo-en-una-app → centralizados como vocabulario de dominio

- `venue-ticket`: `OrderLine`, `OrderStatus`, `OrderTicketStatus`, `OrderStatusUpdate`,
  `CartLine`, `DraftLine`, `PaymentMethod`, `BuyerData`, `MAX_TICKETS_PER_PURCHASE` →
  `venue-core/src/domain/{orders,tickets,common}.ts`.
- `venue-ops`: `CreateEventRequest`, `EventRevenueDto`, `SimulationRunStatus`,
  `SimulationStartRequest`, `PredictionPoint`, `OccupancyPrediction` →
  `venue-core/src/domain/{events,ticketing,simulation,prediction}.ts`. `ScanResult` se queda en
  ops. `VenueOpsMode` se eliminó (solo lo usaba el mapa, ya migrado).

### 2.3 Lógica duplicada → resuelta

- `matchTicketType`: **unificado a la regla robusta** (exacta → contiene la más larga →
  `General` → primero) en `venue-core/src/domain/zoneTicketType.ts`.
- `pricingByZoneId`: movido junto a `matchTicketType`.
- `occupancyRatio` + `occupancyTier`: extraídos a `venue-core/src/domain/zones.ts` (reutilizados
  por el mapa y los hooks).
- Geometría SVG del plano (`standShapes.ts`, `StadiumBackground.tsx`): **unificada en
  `venue-core/src/map/`** (una sola geometría superset con luces; el mapa de ops se eliminó).
- URLs de API: centralizadas en `venue-core/src/api/routes.ts` (pero ops aún no las consume, ver §4).
- Formato de fecha de evento + `TYPE_LABEL`: extraídos a
  `venue-core/src/domain/events.ts` (`formatEventDateTime`, `eventTypeLabel`).
- Patrón de error `✖ {mensaje}` (6 copias en ops): extraído a `venue-core/src/components/InlineError.tsx`.

## 3. Estado global y comunicación

- **`useCheckoutCart` (ticket) usa `orderStore`** (`@venue/core/state`): el estado del pedido
  (orderId, status, rejectionReason, tickets) vive en un store Zustand compartido, con acciones
  `setOrder` (por parche), `updateTicketStatus` y `reset`. Se eliminó el `useRef` espejo
  (`orderRef`) que mantenía estado duplicado para los handlers de SignalR.
- El hook se dividió en dos: **`useOrderStatus`** (estado del pedido + SignalR + `sessionStorage` +
  reconciliación) y **`useCheckoutCart`** (draft, buyer, flags de UI y acciones de
  reserva/pago/cancelación).
- `createSharedStore` registra los stores en `globalThis.__venue_core_shared_stores__` (una
  instancia por página, compartida entre bundles). **No se implementó sync por
  `BroadcastChannel`** (multipestaña) — pendiente si se demuestra necesidad.
- `eventSelectionStore` y `occupancyStore` están definidos en el core pero **aún no los consume
  ninguna app** (candidatos para próximas fases).
- El prop drilling del pedido en `venue-ticket` se redujo: `App → PurchaseFlow` ya no propaga
  `orderId/orderStatus/orderRejectionReason` (vienen del store).

## 4. Gestión de datos (API)

- Core: `venue-core/src/api/` con `HttpClient` tipado (`api.get/post`), `ApiError` con
  `errorMessage()`, `queryKeys`/`mutationKeys` y `API_ROUTES` (rutas de ticketing, access-control
  y checkin).
- **`venue-ticket` usa el cliente del core** en `useCheckoutCart` (crear pedido, `GET /status`,
  pagar y cancelar `orderCancel`) y en `useEventCatalog` (eventos). Se eliminó el `fetch` crudo
  del hook de checkout.
- `venue-ops` sigue con `fetch` crudo y polling (`setInterval`: ingresos 3s, precios 3s,
  simulación 500ms) — **no migrado** (decisión: no tocar lo que funciona sin necesidad).

## 5. Persistencia

- Core: `venue-core/src/storage/` con `StorageService` + `sessionStorageService`/
  `localStorageService`/`memoryStorageService` y hooks (`usePersistedState`,
  `useSessionStorage`, `useLocalStorage`).
- `useCheckoutCart` persiste el `orderId` en `sessionStorage` vía `sessionStorageService` (mismo
  flujo que antes, sin `try/catch` repetido).

## 6. Diseño/UI

- **Decisión de alcance (mantenida): NO se construye librería de componentes atómica
  (Button/Input/...).** El core compartido es de dominio/estado/datos/infra; solo se extraen
  componentes cuando hay duplicación real.
- Componentes compartidos en `venue-core/src/components/`: `EventSummaryCard` (badge de fecha +
  meta + nombre + venue, usado en shell `EventCard`, ticket `CheckoutHeader` y ops `EventList`) e
  `InlineError`.
- Mapa compartido en `venue-core/src/map/`: `StadiumMap`/`ZoneShape` unificados (modo interactivo
  para ticket con luces/tooltip/precio; modo estático con leyenda por ocupación para ops),
  `StadiumBackground`, `standShapes` y CSS propio `map.css` (animaciones `vp-map-*`).
- Tailwind v4 en las 3 apps con `@source '../../venue-core/src'` para generar las utilidades del
  core; los componentes del core usan los tokens genéricos (`text-text`, `bg-accent-bg`,
  `text-danger`…). ticket mapeó sus tokens `tc-*` a los genéricos en su `@theme`.
- `SiteHeader` se mantiene duplicado por app (branding distinto: VP / VO) — decisión consciente.
  `Modal.tsx` sigue siendo solo de ops.

## 7. Estructura final del core (`venue-core`)

```
ui/venue-core/
  package.json          exports: ./domain, ./api, ./hub, ./storage, ./state, ./map,
                        ./map/map.css, ./components  (sin "." raíz)
  src/
    domain/             eventos, ticketing, zones, tickets, orders, occupancy,
                        simulation, prediction, common, zoneTicketType + index.ts
    api/                client (HttpClient/api/withRetry), errors, routes, keys + index.ts
    hub/                hubConnection, hubErrors, useEventScopedConnection + index.ts
    storage/            services (StorageService + impl), hooks + index.ts
    state/              createSharedStore (globalThis), eventSelectionStore,
                        occupancyStore, orderStore + index.ts
    map/                StadiumBackground, StadiumMap, ZoneShape, standShapes,
                        map.css + index.ts
    components/         EventSummaryCard, InlineError + index.ts
```

Cada área expone su barril (`index.ts`) con named exports; las apps importan solo lo que necesitan.
El core se compila contra el tsconfig de cada app (`tsc -b`) y cumple `verbatimModuleSyntax` +
`erasableSyntaxOnly`.

## 8. Implementado (commits en `develop`)

- `4a7d1d0` feat(core): `@venue/core` con domain, api, hub, storage, state, map y components.
- `6159643` refactor(shell) · `ed66a3a` refactor(ticket) · `3a1f2e0` refactor(ops): consumo del core.
- `25d1df6` docs(ui): este documento.
- `6954659` merge en `develop`, con la lógica de cancelación de pedido (de
  `feature/cancel-order-by-customer`) integrada en `useCheckoutCart` sobre `orderStore`.

Limpiezas incluidas: `venue-shell/src/types.ts` (muerto), `venue-ticket/src/{types,shared/zoneTicketType}.ts`
y `venue-ops/src/shared/zoneTicketType.ts` (duplicados), aliases `--color-tc-*` sin uso en ticket.

## 9. Pendiente

- **Verificación E2E** contra el stack real (`docker compose -f infra/docker-compose.yml up --build`):
  mapa interactivo (luces/tooltip/precio), cabecera de checkout, lista de eventos, `InlineError`,
  restauración de pedido tras recarga.
- `eventSelectionStore` / `occupancyStore` sin consumidores (decidir si se adoptan o se borran).
- Ops: migrar `fetch`/polling al cliente del core si se demuestra necesidad.
- Sync multipestaña del store (`BroadcastChannel`) si se necesita.
