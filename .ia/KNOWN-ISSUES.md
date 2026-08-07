# Incidencias conocidas — VenuePulse

## Aforo no vinculado a un evento concreto (EN RESOLUCIÓN)

**Síntoma**: dos eventos distintos en el mismo recinto comparten el mismo contador de aforo
(`ConfirmedSold`/`CheckedIn` de `Zone`), porque `Zone` no tiene `EventId`. Las entradas vendidas
para un evento consumen aforo que debería pertenecer a otro evento distinto.

**Causa raíz**: `AccessControl` y `Ticketing` son bounded contexts separados (bases de datos
distintas, sin acoplamiento directo) y hoy no se coordinan para crear zonas por evento.

**Fix elegido**: arquitectura event-driven — `Ticketing` publica `EventCreated` al
sembrar/crear un evento; `AccessControl` lo consume y aprovisiona sus 3 zonas fijas
(Puerta Norte/Sur/Grada VIP) ya vinculadas a ese `EventId`, con contadores en cero.
Ver `.ia/TODO.md` para el estado de la implementación y el ADR correspondiente una vez cerrado.

**Riesgo de diseño a vigilar**: orden de arranque en `docker-compose` — si `Ticketing` publica
`EventCreated` antes de que `AccessControl` haya enlazado su consumer, el mensaje se pierde (no
hay cola aún vinculada al exchange). Mitigar con `depends_on` + healthcheck, o documentar como
limitación conocida del entorno de demo si no se puede garantizar en todos los arranques.

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
