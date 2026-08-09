# ADR 0016 — Precio dinámico en `Ticketing` por ocupación agregada del evento

## Contexto

Bloque 9 del plan de mejoras: el precio de un `TicketType` debe reaccionar a la demanda con
heurísticas transparentes (no ML, ver `.ia/PRODUCT-VISION.md`), visible en vivo en el checkout.
Depende del Bloque 8 (motor de simulación en `AccessControl`, ya en `develop`), que es hoy la
única fuente real de demanda cambiante en el demo.

El problema: `Zone` (aforo, en `AccessControl`) y `TicketType` (precio, en `Ticketing`) son
conceptos de bounded contexts distintos que solo comparten `EventId` — no existe ni ha existido
nunca una relación 1:1 entre "esta zona" y "este tipo de entrada" (el checkout ya los trata como
dos selects independientes, `zoneId` y `ticketTypeId` viajan por separado en la compra). Pedirle
al modelo una ocupación "del sector" tal y como está escrito el TODO habría exigido inventar esa
relación cruzando ambos servicios — más superficie de la que la demo necesita.

## Alternativas descartadas

- **Ocupación por sector/zona** (leer literalmente el TODO): exigiría un nuevo campo
  `TicketTypeId` en `Zone` o viceversa, coordinando altas en dos servicios distintos para una
  relación que hoy no existe en el dominio ni la necesita el checkout. Descartado por
  sobreingeniería para esta ronda — candidato futuro si el catálogo alguna vez vincula
  explícitamente tipo de entrada y puerta de acceso.
- **Llamada HTTP síncrona `Ticketing` → `AccessControl`** para leer `GET /api/zones?eventId=` en
  cada `GetTicketTypes`: rompe el aislamiento de bounded contexts ya establecido (Bloque 1/3), y
  añade una dependencia síncrona en el camino de lectura del catálogo que hoy no existe.
- **ML/regresión sobre el histórico de ventas**: descartado por producto — heurísticas
  transparentes y defendibles en vivo, no una caja negra (ver `.ia/PRODUCT-VISION.md`).

## Decisión

Ocupación agregada **a nivel de `Event`** (suma de todas las zonas conocidas), no por sector:

- **`ZoneOccupancyChanged`** (nuevo evento de integración en `Core.Messaging.Contracts`):
  `AccessControl` lo publica cada vez que cambia el aforo de una zona, reutilizando el único punto
  de notificación que ya existe para eso — `SignalRZoneOccupancyNotifier` (implementación de
  `IZoneOccupancyNotifier`) ahora también publica vía `IPublishEndpoint`, en vez de repartir el
  publish entre los 4 consumers de zona + el motor de simulación que mutan una zona. Registro del
  notifier pasado de `AddSingleton` a `AddScoped` porque `IPublishEndpoint` es scoped.
- **`ZoneDemandSnapshot`** (`Ticketing.Infrastructure`, no `Domain`): proyección local por
  `ZoneId`, última foto conocida de aforo. Vive en Infrastructure porque no tiene ningún invariante
  de negocio que razonar — solo "el último dato recibido" (mismo criterio que separa `Guard` de
  datos de transporte en ADR 0011). Alimentada por `ZoneOccupancyChangedConsumer`, el primer
  consumer de `Ticketing` (hasta ahora el servicio solo publicaba `EventCreated`, nunca consumía).
- **Sin Inbox real** (a diferencia de los comandos de zona en `AccessControl`, ver ADR 0015):
  `ZoneOccupancyChanged` es un snapshot que se sobrescribe con el upsert, no un contador que se
  incrementa — procesarlo dos veces no cambia el resultado, así que la protección extra no compra
  nada.
- **`DynamicPricing`** (`Ticketing.Domain`, puro, sin EF/MassTransit): tramos fijos sobre la
  ocupación vendida (`1 - disponible/aforo`, agregada por evento) — `<40%` -10%, `40-70%` precio
  base, `70-90%` +15%, `≥90%` +30%. Sin datos de ocupación todavía (evento sin simular) → precio
  base. `TicketTypeDto.Price` sigue siendo el precio de catálogo (persistido); `EffectivePrice`/
  `PriceTier` se recalculan en cada lectura (`TicketingService.GetTicketTypesAsync`), nunca se
  persisten — un cambio de heurística no exige migrar datos históricos.
- **Precio en vivo**: nuevo `PricingHub` (SignalR) en `Ticketing.Api`, empujado por
  `ZoneOccupancyChangedConsumer` tras recalcular. `ticket-checkout` se suscribe igual que ya hace
  con `occupancyChanged`. Nueva ruta `/hubs/pricing` en el gateway Nginx.
- **Bug encontrado y corregido durante la verificación E2E real (`docker compose up`)**:
  `ProvisionZonesForEventAsync` no notificaba el estado inicial de una zona recién creada —
  Ticketing solo se enteraba de una zona la primera vez que cambiaba, así que una zona virgen
  (100% libre) quedaba fuera del agregado y el % de ocupación calculado se inflaba (verificado en
  real: 33,7% de ocupación real se leía como 80%, porque solo 1 de las 3 zonas del evento había
  publicado nunca un `ZoneOccupancyChanged`). Corregido notificando cada zona justo al
  aprovisionarla, cubierto con `ZoneServiceTests` nuevo en `AccessControl`.

## Consecuencias / riesgo aceptado

- El precio reacciona a la ocupación **global del evento**, no de la zona/puerta concreta por la
  que se compra — una limitación conocida y documentada, no un descuido: es la única lectura
  honesta posible sin inventar una relación Zone↔TicketType que no existe en el dominio.
- `zone_demand_snapshots` no tiene TTL/purga — aceptable para una demo (mismo riesgo aceptado que
  `processed_messages` en ADR 0015).
- Verificado con el harness in-memory de MassTransit (`ZoneOccupancyChangedConsumerTests`, sin
  broker real) + tests unitarios de `DynamicPricing`/`TicketingService`: la heurística por tramos,
  el upsert de la proyección y el recálculo del precio efectivo quedan cubiertos sin Postgres ni
  RabbitMQ reales.
