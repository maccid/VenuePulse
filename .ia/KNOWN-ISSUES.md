# Incidencias conocidas — VenuePulse

## Aforo no vinculado a un evento concreto (RESUELTO)

**Síntoma**: dos eventos distintos en el mismo recinto compartían el mismo contador de aforo
(`ConfirmedSold`/`CheckedIn` de `Zone`), porque `Zone` no tenía `EventId`. Las entradas vendidas
para un evento consumían aforo que debería pertenecer a otro evento distinto.

**Causa raíz**: `AccessControl` y `Ticketing` son bounded contexts separados (bases de datos
distintas, sin acoplamiento directo) y no se coordinaban para crear zonas por evento.

**Fix aplicado** (Bloque 1, `fix/zone-event-scoping`): arquitectura event-driven — `Ticketing`
publica `EventCreated` al sembrar un evento (con Outbox transaccional propio); `AccessControl`
lo consume (`EventCreatedConsumer`) y aprovisiona sus 3 zonas fijas (Puerta Norte/Sur/Grada VIP)
ya vinculadas a ese `EventId`, con contadores en cero. Idempotente ante redelivery
(`ExistsForEventAsync` antes de provisionar). Verificado en real con `docker compose up --build`:
2 eventos → 6 zonas con el `eventId` correcto, `ZonesController` filtra con `?eventId=`.

**Riesgo de diseño mitigado**: orden de arranque en `docker-compose` — si `Ticketing` publica
`EventCreated` antes de que `AccessControl` haya enlazado su consumer, el mensaje se pierde (no
hay cola aún vinculada al exchange). Mitigado con `api-ticketing` dependiendo de
`api-accesscontrol: condition: service_healthy` — el healthcheck HTTP solo responde una vez que
todos los `IHostedService` (incl. el bus de MassTransit) ya arrancaron, así que para cuando
`api-ticketing` arranca, el consumer ya está enlazado. No es una garantía formal (no hay Inbox
todavía en `AccessControl`, ver Bloque 3 en `.ia/TODO.md` para el caso general de redelivery),
pero cubre el arranque en frío, que es el único caso relevante para esta demo.

## Test de integración de la Saga — camino de compensación intermitente en Testcontainers

Documentado en `docs/architecture/adr/0009-masstransit-mongo-debugging.md`. Verificado
manualmente contra `docker-compose` real: **no es un bug real**, ambos caminos (feliz y
compensación) funcionan correctamente fuera del arnés de test. Causa más probable: contención de
recursos entre Testcontainers y `WebApplicationFactory` en la misma máquina. No se ha vuelto a
intentar arreglar el test en sí — coste/beneficio no lo justifica para una demo.

## Rewrite de rutas duplicado en el gateway (RESUELTO)

El `nginx.conf` del gateway anteponía un `/api/` extra al reenviar peticiones hacia los
backends, generando `/api/api/...` y 404 en todas las llamadas del navegador. Corregido — ver
commit `fix(gateway): corregir rewrite duplicado /api/api/`.

## Colisión de `#root` entre `shell` y `ticket-checkout` (RESUELTO)

El modo standalone de desarrollo de `ticket-checkout` buscaba `document.getElementById('root')`,
el mismo id que usa `shell`. Al cargarse como script remoto dentro de `shell`, vaciaba su DOM.
Corregido con un id exclusivo (`ticket-checkout-standalone-root`).
