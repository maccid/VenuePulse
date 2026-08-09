# ADR 0018 — Carrito multi-entrada, Saga partida en `AwaitingPayment` y `EventType`

## Contexto

Bloque de mejoras pedido tras el Bloque 13 (wizard de 3 pasos + plano): el checkout seguía sin
resolver varias cosas que se sentían poco realistas —

- Se compraba una entrada a la vez, no un carrito con varias.
- El aforo no se bloqueaba hasta que el "pago" (mockeado) ya se había resuelto solo, en la misma
  llamada — no había una espera real a que el cliente eligiera cómo pagar.
- Se podía elegir un `TicketType` ("Normal") y una `Zone` ("Grada VIP") sin ninguna relación entre
  ambos, aunque son bounded contexts separados a propósito (ADR 0016).
- El plano del recinto (`StadiumMap`, ADR 0017) solo tenía la plantilla de partido; un concierto
  mostraba las mismas zonas de "Puerta Norte/Sur", que no encajan con tener pista y escenario.
- `venue-ops` coloreaba su plano por `checkedIn` (torniquete), una señal que no se mueve durante la
  fase de venta — mientras se compran entradas, el plano de operaciones se veía vacío.

## Alternativas descartadas

- **Carrito como intención local, sin reservar hasta pagar**: primera idea considerada. Se
  descartó porque no resuelve el problema real ("que no me quiten la entrada mientras decido") —
  entre añadir al carrito y pagar, otro comprador podría agotar la zona. La alternativa elegida
  (reservar de verdad al añadir) es más simple de razonar y más honesta.
- **Relación real Zone↔TicketType en el backend** para que "tipo de entrada" y "zona" sean
  siempre coherentes: descartado por el mismo motivo que ADR 0016 — son bounded contexts
  distintos, e inventar esa relación cruzada es más superficie de la que la demo necesita.
- **Geometría de zona dinámica por evento** (coordenadas en base de datos) para el plano de
  concierto: descartado igual que en ADR 0017 — las zonas son un puñado fijo por tipo de evento,
  un mapeo estático en frontend basta.

## Decisión

**1. `EventType` (`Match` | `Concert`) en `Event` (Ticketing)**, propagado en `EventCreated` y
consumido por `AccessControl` para elegir el set de zonas a aprovisionar:
- Partido: `Puerta Norte`(40), `Puerta Sur`(40), `Grada VIP`(15) — igual que antes.
- Concierto: `Pista`(60), `Grada Norte`(20), `Grada Sur`(20).

Los nombres de la zona "especial" de cada tipo (`Grada VIP`, `Pista`) coinciden a propósito con el
`TicketType` correspondiente del catálogo de ese evento — es lo que permite el punto 3 sin tocar
el backend.

**2. La Saga de compra se parte en tres fases reales, no dos**: `Reserving` → `AwaitingPayment` →
`Confirming`. Antes, `PurchaseRequested` ya llevaba `SimulateFailure` decidido de antemano y la
Saga confirmaba/liberaba sola en cuanto AccessControl reservaba. Ahora:
- `TicketPurchaseRequested` ya no lleva método de pago, solo reserva.
- Al llegar `ZoneSlotReserved`, la Saga pasa a `AwaitingPayment` y se queda esperando — el aforo ya
  está reservado de verdad en este punto (visible en vivo en cualquier plano).
- Nuevo endpoint `POST /api/checkin/api/tickets/{id}/pay` (`{ method: "Card" | "Bizum" }`) publica
  `TicketPaymentSubmitted`, que la Saga consume en `AwaitingPayment`: Tarjeta confirma, Bizum
  dispara la compensación real (`ReleaseZoneSlot`) que ya existía.
- `Ticket` (CheckIn) gana el estado real `AwaitingPayment` (no solo interno de la Saga) para que la
  Api pueda rechazar con un error limpio un `/pay` prematuro, en vez de arriesgarse a que el evento
  llegue a la Saga en un estado que no lo espera.
- El watchdog de timeout (Bloque 2/ADR 0010) se rearma al entrar en `AwaitingPayment` en vez de
  dejar de vigilar — si el cliente nunca paga, se libera el aforo igual que si hubiera fallado el
  pago (motivo `PaymentTimedOut`, distinto de `ReservationTimedOut`).

Esto no es una desviación del patrón Saga, es más fiel a él: un Saga real coordina pasos que
pueden tardar (incluida una acción humana) y compensa si algo falla a mitad de camino — la versión
anterior, que resolvía todo en una llamada, era la simplificación.

**3. El carrito son entradas ya reservadas de verdad, no una cesta pendiente de pago.** Clicar una
zona en el plano abre un popup (precio de catálogo tachado, `effectivePrice` destacado, tier,
aforo, selector de cantidad); "Añadir" dispara ya `POST /api/checkin/api/tickets` por cada unidad
— cada línea del carrito es un ticket real en `AwaitingPayment`. El nombre del comprador se pide
una vez, antes de poder añadir nada (lo exige el backend al crear la entrada); apellidos/correo en
el paso de pago son solo visuales, no los persiste nada. Al elegir Tarjeta/Bizum se llama a
`/pay` para cada línea en `AwaitingPayment` a la vez. **Límite de 10 entradas por compra**
(barrera de producto, no del backend) para no vaciar una zona entera de una sentada en la demo.

**4. Fusión zona↔tipo de entrada solo en el frontend**, por coincidencia exacta de nombre
(`zone.name === ticketType.name`, si no `"General"`, si no el primero) — sin relación real en el
backend. Funciona siempre gracias al punto 1 (los nombres se eligen para que coincidan).

**5. `StadiumMap` elige plantilla mirando los propios nombres de zona** (si existe una zona
`"Pista"` → plantilla concierto: pista central + barra de "Escenario" decorativa, sin zona real
detrás; si no → plantilla partido con campo y gradas). No hace falta pasar el tipo de evento como
prop nueva por el Web Component.

**6. `venue-ops` colorea su plano por ocupación de VENTA** (`capacity - availableToSell`), no por
`checkedIn` — mismo dato (`occupancyChanged`) que ya recibía, solo cambia qué ratio calcula. Nuevo
panel "Actividad en vivo" (`ActivityFeed`) suscrito a `/hubs/tickets`, mostrando las últimas
reservas/confirmaciones/rechazos por zona — para esto `TicketStatusDto` gana `EventId`/`ZoneId`
(ya estaban en memoria en `Ticket`, sin query nueva).

## Consecuencias / riesgo aceptado

- El nombre del comprador se fija en el momento de crear cada entrada; si se cambia después en el
  paso de pago, ese cambio es solo cosmético para esa sesión de checkout, no se re-envía al
  backend. Aceptado: no hay caso de uso real en la demo que lo necesite.
- Apellidos/correo en el formulario de pago son campos de UI puros — no hay forma de que
  `venue-ops` ni ningún reporte los vea. Si algún día hace falta un dato de contacto real,
  `Ticket`/`PurchaseTicketCommand` necesitarán un campo nuevo — de momento no hay ese caso de uso.
- Igual que en ADR 0017, si las zonas dejan de ser fijas por tipo de evento, la fusión por nombre
  (punto 4) y las plantillas del plano (punto 5) dejan de cubrir el caso y hay que revisitarlas.
- `AwaitingPayment` es un estado real y persistido, no solo un paso de la Saga — coherente con que
  la Api pueda validar contra él, pero es una tabla más de estados que mantener sincronizada con
  la máquina de estados de la Saga (misma naturaleza de acoplamiento que ya existía entre
  `Ticket.Status` y `TicketPurchaseSagaState`).
