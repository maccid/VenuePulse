# Incidencias conocidas — VenuePulse

## Referencias a `olix` eliminadas del contexto de trabajo (LIMPIADO EN feature/simulation-engine)

Se ha quitado la palabra "olix" de `.ia/PRODUCT-VISION.md` y `.ia/TODO.md` (la única mención
remanente en `feature/simulation-engine`). El objetivo es que el proyecto no mencione por nombre
el proyecto privado de referencia — se habla de "otros proyectos privados del autor" sin nombrarlo.

**Pendiente en `develop`** (fuera del alcance de esta rama): `docs/architecture/adr/0011-core-shared-patterns.md:9`
(línea creada por Claude Code en `feature/core-shared-patterns`, mergeada a `develop`) todavía
cita `olix.workspace` por nombre. Queda anotado aquí para que quien lo mergee/trabaje esa rama lo
limpie — no se toca desde `feature/simulation-engine` para no chocar con esa rama paralela.

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
`api-ticketing` arranca, el consumer ya está enlazado. No es una garantía formal para la pérdida
de mensajes en el arranque en frío (sigue sin resolverse, pero es el único caso relevante para
esta demo); el caso general de **redelivery** (mensaje que sí llega pero se reprocesa) ya está
cubierto — ver Inbox real en `AccessControl` (Bloque 3, ADR 0015).

## Test de integración de la Saga — camino de compensación intermitente en Testcontainers

Documentado en `docs/architecture/adr/0009-masstransit-mongo-debugging.md`. Verificado
manualmente contra `docker-compose` real: **no es un bug real**, ambos caminos (feliz y
compensación) funcionan correctamente fuera del arnés de test. Causa más probable: contención de
recursos entre Testcontainers y `WebApplicationFactory` en la misma máquina. No se ha vuelto a
intentar arreglar el test en sí — coste/beneficio no lo justifica para una demo.

**Reconfirmado en el Bloque 14** (partición de la Saga en `AwaitingPayment`, ver ADR 0018):
`Bizum_compensa_y_rechaza_la_entrada` (antes `Fallo_de_pago_simulado_...`) sigue reproduciendo la
misma intermitencia, ahora con diagnóstico más fino (log a fichero, no solo `Console.WriteLine` —
xUnit no captura la salida de consola de hilos de fondo de MassTransit en este runner). Traza real
observada: el doble de `AccessControl` del test recibe `ReserveZoneSlot` y responde
`ZoneSlotReserved` en ~1s, pero la Saga del host de pruebas no llega a procesarlo a tiempo y el
watchdog la rechaza por `ReservationTimedOut` (aunque se subió el timeout a 60s solo para este
test). **Verificado a mano varias veces contra el stack real** (`docker compose up --build`,
contenedores persistentes en vez de efímeros): tarjeta confirma, bizum compensa correctamente — el
código de producción funciona, la intermitencia sigue siendo del arnés de Testcontainers.

## Rewrite de rutas duplicado en el gateway (RESUELTO)

El `nginx.conf` del gateway anteponía un `/api/` extra al reenviar peticiones hacia los
backends, generando `/api/api/...` y 404 en todas las llamadas del navegador. Corregido — ver
commit `fix(gateway): corregir rewrite duplicado /api/api/`.

## Colisión de `#root` entre `shell` y `ticket-checkout` (RESUELTO)

El modo standalone de desarrollo de `ticket-checkout` buscaba `document.getElementById('root')`,
el mismo id que usa `shell`. Al cargarse como script remoto dentro de `shell`, vaciaba su DOM.
Corregido con un id exclusivo (`ticket-checkout-standalone-root`).
