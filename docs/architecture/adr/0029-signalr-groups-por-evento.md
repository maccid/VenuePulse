# ADR 0029 — SignalR: `Groups` por evento (y backplane Redis, descartado)

## Contexto

Los 3 hubs de SignalR (`/hubs/occupancy` en `AccessControl`, `/hubs/pricing` en `Ticketing`,
`/hubs/tickets` en `CheckIn`) hacen hoy **broadcast a todos** los clientes conectados: cada
notifier usa `hubContext.Clients.All.SendAsync(...)` sin ningún scoping. Con varios eventos en el
catálogo, un cliente viendo el evento A recibe también los mensajes del evento B — sobre-broadcast
innecesario que crece con el nº de eventos, y causa plausible de ruido/confusión con el precio en
vivo (ver Bloque 23, `.ia/TODO.md`).

Además hay un caso concreto donde el broadcast fuerza a filtrar en el cliente sin poder evitarlo:
`ticket-checkout` ya filtra `pricingChanged` por `updated.eventId === eventId` en
`useEventCatalog.ts` (y `venue-ops` mantiene diccionarios por evento en el cliente, ADR 0022) — el
scoping por evento en el servidor elimina ese trabajo y el tráfico que lo alimenta.

## Decisión

1. **Un grupo de SignalR por `EventId` en cada uno de los 3 hubs** (`"event-{eventId}"`), con dos
   métodos de hub — `JoinEvent(Guid eventId)` / `LeaveEvent(Guid eventId)` — que añaden/retiran al
   cliente del grupo. El `eventId` se pasa por **invocación desde el cliente**, no por query string:
   `ticket-checkout` (un evento fijo por página) podría entrar al grupo en `OnConnectedAsync`
   leyendo un query param, pero `venue-ops` mantiene **una sola conexión por hub** que atiende
   *varias pestañas de eventos* (ADR 0022) y necesita poder unirse/salir de varios grupos
   dinámicamente — algo que un query param fijo por conexión no permite.
2. **Los 4 notifiers envían solo al grupo**: `Clients.Group("event-{eventId}")` en
   `SignalRZoneOccupancyNotifier`, `SignalRPricingNotifier`, `SignalRTicketStatusNotifier` y
   `SignalROrderStatusNotifier`. El `eventId` ya viaja en el payload de los 4 DTOs
   (`ZoneOccupancyDto.EventId`, `eventId` de pricing, `TicketStatusDto.EventId`,
   `OrderStatusDto.EventId`), así que no hay que propagar ningún campo nuevo.
3. **El cliente re-entra al grupo tras cada reconexión** (`onreconnected`): SignalR pierde la
   pertenencia a grupos al reconectar, así que cada app vuelve a invocar `JoinEvent` con el evento
   activo. En `venue-ops` el join/leave se centraliza en un hook compartido
   (`useEventScopedConnection`) que recibe el `activeId` y mantiene el grupo sincronizado con la
   pestaña activa.

## Por qué no: backplane Redis

El backplane Redis para SignalR **se documenta, no se construye** — mismo criterio que ADR 0006 ya
aplicó a Prometheus/Seq: con una única instancia de cada API en la demo, `Groups` funciona en
memoria sin ninguna pieza de infraestructura extra. La condición explícita bajo la que sí se
justificaría es **N instancias de una misma API detrás de un load balancer** (los grupos dejarían
de ser compartidos entre instancias): escenario de roadmap documentado en el README junto a
Prometheus/Grafana/Seq, no una necesidad de esta demo.

## Alternativas descartadas

- **Query param `?eventId=` + `OnConnectedAsync`**: más simple para `ticket-checkout`, pero
  inaplicable a `venue-ops`, cuyo modelo es una conexión por hub multi-evento (ADR 0022). Elegir
  una primitiva que solo encaja en una de las dos apps sería resolver el caso simple y dejar el
  complejo sin tocar.
- **Fusionar los 3 hubs en uno solo** (realtime gateway, un único WebSocket): añade un salto de
  mensajería (servicio → RabbitMQ → hub agregado → cliente),
  un proceso nuevo con sus propios problemas de dedup/orden/pérdida, y no reduce el coste real
  (fan-out); es ortogonal a este ADR y queda como roadmap condicional.
- **Filtrar en el cliente y ya está** (estado actual): sigue funcionando, pero envía cada
  actualización a todos los clientes de todos los eventos y obliga a mantener diccionarios por
  evento que el servidor podría no enviar — el scoping en servidor es barato y elimina ese
  tráfico.

## Consecuencias / riesgo aceptado

- Un cliente que no llame a `JoinEvent` no recibe nada (hoy recibiría el broadcast completo) —
  todos los consumidores de las 3 apps se actualizan en este ADR, y es un cambio coordinado
  frontend/backend en la misma rama.
- Tras una reconexión, el grupo se pierde: si el re-join fallara (p. ej. por una carrera de
  reconexión), el cliente se quedaría mudo hasta la siguiente reconexión — se mitiga reintentando
  el join en `onreconnected`, y en `ticket-checkout` el `onreconnected` ya existía.
- Sin backplane Redis: si algún día se escala una API a varias instancias, los grupos se volverán
  no compartidos — condición de activación explícita documentada en la sección anterior.
