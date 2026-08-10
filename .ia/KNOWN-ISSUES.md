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

## Timeline de la Saga: por SignalR solo llega el estado final (EN REVISIÓN)

**Síntoma**: `docs/demo-script.md` promete el timeline actualizándose en vivo paso a paso
("Reservando aforo → Procesando pago → Confirmando → Confirmada"), pero `ITicketStatusNotifier`
solo se invoca desde los consumers de resultado (`TicketPurchaseOutcomeConsumers`, estados
`Confirmed`/`Rejected`). Los pasos intermedios los deriva el frontend (`SagaTimeline.tsx`) del
estado final, así que no animan individualmente en tiempo real.

**Verificado** con un cliente SignalR real vía el gateway (misma ruta `/hubs/tickets` que el
navegador): una compra feliz y una de compensación emiten un único `ticketStatusChanged` con el
estado definitivo (~40ms después del `occupancyChanged` de la reserva). El timeline de la UI sí
refleja el resultado y marca en rojo el paso que falló según `rejectionReason`, pero no hay
progresión animada entre pasos.

**Estado**: pendiente de decidir entre (a) que `CheckIn` notifique cada transición de la Saga
(`Reserving`/`ProcessingPayment`/`Confirming`) para que el timeline sea realmente en vivo, o
(b) ajustar el guion para no prometer esa progresión. Claude Code está trabajando en una mejora
relacionada — **cuando mergee, revisar si ya se emiten las transiciones y re-verificar con el
mismo procedimiento** antes de dar el ítem por cerrado.

**Actualización (Bloque 14, ADR 0018, ya mergeado a `develop`)**: la partición de la Saga en
`Reserving → AwaitingPayment → Confirming` añadió un paso intermedio real. Ahora
`ITicketStatusNotifier.NotifyStatusChangedAsync` se invoca desde **tres** consumers, no solo dos:
`TicketReadyForPaymentConsumer` (estado `AwaitingPayment`, tras `TicketAwaitingPayment` publicado
por la propia Saga) además de los ya existentes `TicketPurchaseConfirmedConsumer`/
`TicketPurchaseRejectedConsumer` (`services/CheckIn/Infrastructure/Consumers/
TicketPurchaseOutcomeConsumers.cs`). El timeline del frontend (`SagaTimeline.tsx`) ahora tiene 5
pasos y sí anima en vivo el salto "Reservando aforo → Esperando pago" por SignalR real (el salto
"Esperando pago → Confirmando" lo sigue disparando la propia acción del usuario al pulsar pagar, no
la Saga). **Sigue pendiente de re-verificar con el mismo procedimiento** (cliente SignalR real vía
`/hubs/tickets`) antes de cerrar el ítem del todo — esta nota solo confirma que el código ya emite
el paso intermedio, no que se haya vuelto a comprobar en vivo tras el merge.

## Sin estrategia de reintento si falla el aprovisionamiento de zonas al crear un evento

**Síntoma**: si `EventCreatedConsumer` (`AccessControl`) lanza una excepción al aprovisionar las
zonas de un evento recién creado (en `Ticketing`, vía `POST /api/events` desde el Bloque 15, o vía
seed data), el evento queda "vivo" en el catálogo (`Ticketing` ya confirmó su propio Outbox, eso no
falla) pero **sin ninguna zona** en `AccessControl` — nadie puede reservar aforo para él, y el
plano del checkout se vería vacío indefinidamente.

**Causa raíz**: `MessagingExtensions.cs` (wiring compartido de MassTransit) no configura ningún
`UseMessageRetry` en ningún servicio. Sin política de reintento, el comportamiento por defecto de
MassTransit ante un fallo del consumer es moverlo a la cola de error (`<cola>_error`) tras un único
intento — no se pierde, pero tampoco se reprocesa solo; hace falta intervención manual (reencolar
desde la consola de RabbitMQ, o arreglar el bug y republicar).

**Por qué es distinto del caso de la compra** (que sí tiene una estrategia real, ver más arriba y
ADR 0010/0018): `TicketPurchaseSaga` es una Saga orquestada que sabe explícitamente qué hacer si
`AccessControl` no responde (timeout) o si el pago falla tras reservar (compensación con
`ReleaseZoneSlot`). Aprovisionar zonas al recibir `EventCreated` es solo consumo "dispara y olvida"
(choreography, no Saga) — nadie espera una respuesta, así que no hay a quién avisar ni qué
compensar si falla.

**Riesgo aceptado, sin acción por ahora**: mismo criterio que el resto de huecos documentados aquí
— añadir `UseMessageRetry` con backoff, alertas sobre la cola de error, o un job de reconciliación
que detecte eventos sin zonas serían las mitigaciones típicas en producción, pero se descartan por
ahora por no tener necesidad real demostrada en esta demo (ADR 0006). Candidato a revisar si algún
día se demuestra que el fallo ocurre con frecuencia suficiente para justificarlo.

## `ticket-checkout`: la fusión zona↔tipo de entrada no encontraba `Grada VIP` → `VIP` (RESUELTO)

**Síntoma**: `ui/ticket-checkout/src/zoneTicketType.ts` (`matchTicketType`) fusiona zona (`Zone`,
AccessControl) con tipo de entrada (`TicketType`, Ticketing) por **igualdad exacta** de nombre. Con
los datos reales del seed y de `DefaultTicketTypeCatalog` (partido: zona `Grada VIP`, tipo de
entrada `VIP`), esa igualdad nunca se cumple — la zona `Grada VIP` no encuentra ningún tipo cuyo
nombre sea exactamente `"Grada VIP"`, así que cae al *fallback* `General`. Un fan que compra en
`Grada VIP` vería (y pagaría) el precio de `General`, no el de `VIP` — el popup de compra
(`ZonePopup.tsx`) y el precio final estarían mal para esa zona en concreto. Las zonas de "puerta"
(`Puerta Norte`/`Sur`) no se ven afectadas porque su *fallback* a `General` ya es el resultado
correcto por casualidad.

**Verificado contra el stack real** (`docker compose up --build`, Bloque 18) consultando
`GET /api/ticketing/api/events/{id}/ticket-types` y `GET /api/access-control/api/zones?eventId=`
para un evento de partido: tipos `General`/`VIP`, zonas `Puerta Norte`/`Puerta Sur`/`Grada VIP` — el
único nombre de zona que coincide con su tipo por igualdad exacta es el caso de concierto (`Pista`
zona = `Pista` tipo); `Grada VIP` nunca coincide con `VIP` por igualdad exacta.

**Causa raíz**: el nombre de la zona "especial" de cada `EventType` se eligió como `Grada VIP`/
`Pista` para que coincidiera con su tipo de entrada correspondiente (ver comentario de
`ZoneService.cs`), pero solo funciona por coincidencia exacta cuando ambos nombres son literalmente
idénticos (`Pista`=`Pista`) — no cuando el nombre de zona es más largo (`Grada VIP`≠`VIP`).

**Sin corregir desde `venue-ops` (Bloque 18)**: `zoneTicketType.ts` vive en `ticket-checkout`, que
`opencode` está tocando en paralelo (Bloques 19/20) sobre la misma base de datos del stack —
corregirlo desde otra rama arriesgaría un conflicto de merge sin necesidad real. La copia nueva
equivalente en `venue-ops` (Bloque 18, `ui/venue-ops/src/zoneTicketType.ts`, para estimar ingresos
con venta simulada) ya nace con la regla corregida: igualdad exacta → nombre de tipo de entrada
contenido en el nombre de zona (el más largo que coincida) → `General`/primero.

**Resuelto en el Bloque 19+20 (mergeado a `develop`) por otra vía**: en vez de cambiar la regla de
fusión, se alineó `DefaultTicketTypeCatalog` con el set de zonas 3+3 (ADR 0018) — el tipo de entrada
"especial" de cada `EventType` ahora se llama igual que su zona (`Grada VIP` en partido, `Pista` en
concierto, y `Grada Norte`/`Grada Sur` que ya coincidían), de modo que la **igualdad exacta** de
`matchTicketType` vuelve a casar sin tocar la regla. Queda como nota histórica; si algún día se
añade una zona sin tipo de entrada homónimo, la regla de `venue-ops` (`includes`) sigue siendo el
candidato a portar a `ticket-checkout`.
