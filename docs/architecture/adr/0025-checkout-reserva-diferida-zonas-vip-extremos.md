# ADR 0025 — Checkout: zonas VIP en los fondos, reserva diferida a "Reservar" y contador

## Contexto

Petición explícita del usuario (producto) sobre la experiencia de compra de `ticket-checkout`
para partido, tras ver el plano de 4 zonas de ADR 0024 en uso:

- Las gradas de fondo (Norte/Sur, antes `Puerta Norte`/`Puerta Sur`) deben ser las gradas **VIP**,
  las más caras y con menos aforo — no las laterales (Este/Oeste), como estaban desde ADR 0024.
  El plano debe alinear Norte/Sur al ancho exacto del campo y Este/Oeste a su alto exacto, pegadas
  a los lados, con el texto de zona en varias líneas sin rotar cuando el hueco es estrecho.
- El carrito debe presentarse en 3 tarjetas (evento+volver, plano con icono y título "Mapa del
  Evento", compra), agrupando entradas repetidas de la misma zona en una sola línea con cantidad
  y subtotal.
- Añadir entradas a la selección **no debe reservar aforo real todavía** — solo lo hace un botón
  explícito "Reservar", momento en el que el título de la tarjeta pasa de "Tus Reservas" a "Tu
  compra", aparecen el resumen, el contador de tiempo restante y el formulario de datos
  personales, y el semáforo de ocupación del plano cambia de color por primera vez.
- El nombre del comprador deja de pedirse arriba del todo del formulario; se pide una sola vez,
  como campo obligatorio, junto al propio botón "Reservar" (es el dato que el backend exige para
  crear cada entrada).
- Debe verse cuánto tiempo queda de la reserva antes de que expire (hoy 30s, configurable por
  `Saga:ReservationTimeoutSeconds`), algo que hoy no se expone en ningún sitio.
- Fuera de alcance explícito de esta tarea: emitir un GUID de grupo que valide de una vez todas
  las entradas de una compra múltiple (manteniendo el GUID individual de cada una) — se anota
  como pendiente en `.ia/TODO.md`, no se construye aquí.

## Alternativas descartadas

- **Mantener los nombres/precios/aforo actuales y solo cambiar el CSS del plano** (visual puro):
  descartado porque la petición es de negocio, no solo estética — las gradas de fondo deben ser
  objetivamente las caras y escasas, lo que exige tocar `ZoneService.MatchZones` y
  `DefaultTicketTypeCatalog`, no solo `StadiumMap.tsx`.
- **Reservar solo al pagar** (carrito 100% intención local hasta el paso de pago): ya descartada
  una vez en ADR 0018 por el mismo motivo que sigue aplicando — deja la mayor ventana de carrera
  posible entre "elegir" y "que se confirme que sigue libre". Se opta por un punto intermedio:
  reservar en un momento explícito ("Reservar"), antes del paso de pago, no en cada "Añadir"
  individual ni al elegir el método de pago.
- **Persistir el deadline de la reserva** (`ExpiresAtUtc` en el agregado `Ticket` o en el estado
  de la Saga en Mongo) en vez de leerlo del tracker en memoria: descartado por ahora, mismo
  criterio que ADR 0010 — el tracker en memoria ya es la fuente de verdad que usa el propio
  watchdog para expirar reservas; añadir persistencia sin necesidad real (el valor ya se calcula
  y se lee en el mismo proceso) sería infraestructura de más para una demo.

## Decisión

**1. Swap de las 4 zonas de partido** (`AccessControl.ZoneService.MatchZones` y
`Ticketing.DefaultTicketTypeCatalog` para `Match`): `Grada VIP Norte` (8 plazas, 90€) y
`Grada VIP Sur` (7 plazas, 90€) sustituyen a `Puerta Norte`/`Puerta Sur`; `Grada Este` y
`Grada Oeste` (40 plazas cada una, 40€ vía el tipo compartido `General`) sustituyen a
`Grada Este VIP`/`Grada Oeste VIP`. Mismo mecanismo de fusión zona↔tipo por nombre de ADR
0016/0018/0024, solo cambia qué par de zonas comparte tipo y cuál tiene tipo propio.

**2. Geometría del plano** (`StadiumMap.tsx` de `ticket-checkout` y `venue-ops`): Norte/Sur usan
el mismo `x`/`width` que el campo (`FIELD.x`/`FIELD.width` = 88/164); Este/Oeste usan el mismo
`y`/`height` que el campo (`FIELD.y`/`FIELD.height` = 84/112), pegadas a sus lados. `ZoneShape`
(`ticket-checkout`) reparte el nombre de zona en varias líneas (`<tspan>` apilados, sin
`transform: rotate`) cuando el ancho de la zona es estrecho, en vez de rotar el texto.

**3. Reserva diferida a un botón "Reservar"**: `PurchaseFlow`/`App.tsx` mantienen un carrito
**borrador** (`DraftLine[]`, solo estado local, sin llamar al backend) mientras el usuario añade
entradas desde el plano — se agrupan por zona con cantidad y subtotal bajo el título **"Tus
Reservas"**. Solo al pulsar "Reservar" se dispara el mismo `POST /api/checkin/api/tickets` por
unidad que antes se disparaba en cada "Añadir" (ADR 0018 punto 3 sigue vigente *a partir de este
punto*: una vez reservado, sigue siendo una entrada real, no una intención); el título pasa a
**"Tu compra"**, aparecen el resumen agrupado de las entradas ya reservadas, el contador y el
formulario de pago. Esto es el único momento en que `occupancyChanged` (y por tanto el semáforo
del plano) refleja la nueva selección.

**4. Nombre del comprador**: se pide una única vez, como campo obligatorio junto al botón
"Reservar" (ya no arriba del todo del formulario ni deshabilitado en el paso de pago).

**5. Contador de reserva**: `ReservationTimeoutTracker` (CheckIn) gana `TryGetDeadline`;
`TicketStatusDto` gana `ExpiresAtUtc` (nulo si no hay reserva activa vigente — resuelto o nunca
trackeado), propagado por SignalR (`ticketStatusChanged`) y por el `GET /status`. El frontend
pinta una cuenta atrás en vivo (`useCountdown`) mientras haya `expiresAtUtc`.

## Consecuencias / riesgo aceptado

- **Reabre ADR 0018 (punto 3)**: entre que el usuario añade una entrada al borrador y pulsa
  "Reservar", otro comprador puede agotar la zona — exactamente el riesgo que ADR 0018 ya había
  señalado al descartar el carrito de intención local. Se acepta a petición directa del usuario,
  igual que ADR 0024 aceptó revertir una decisión previa. Mitigación parcial, no garantía: el
  popup de cantidad resta del `availableToSell` del backend las unidades ya puestas en el
  borrador para esa zona; la validación real sigue siendo la del backend al reservar (rechazo
  parcial por línea, ya soportado por el `SagaTimeline` existente).
- **Reabre ADR 0024 (naming/precio/aforo)**: de nuevo, cambio de negocio documentado y aceptado a
  petición explícita, no un descuido — este ADR es la fuente de verdad vigente sobre qué par de
  gradas es la VIP.
- El deadline de la reserva sigue viviendo solo en memoria del proceso CheckIn (ADR 0010): un
  reinicio del servicio pierde el contador visible en la UI igual que ya pierde el propio
  watchdog que expira reservas — limitación conocida, no nueva.
- `venue-ops` actualiza solo las coordenadas y nombres de zona para no romperse con el swap
  (sigue sin plano rediseñado visualmente, ver ADR 0024) — su propio rediseño queda fuera de
  alcance, igual que ya estaba.
- El GUID de grupo para validar de una vez una compra múltiple queda pendiente (`.ia/TODO.md`):
  requiere modelo de dominio nuevo en `CheckIn` (campo de grupo en `Ticket`, endpoint de
  validación que acepte código individual o grupal) no cubierto por este ADR.
