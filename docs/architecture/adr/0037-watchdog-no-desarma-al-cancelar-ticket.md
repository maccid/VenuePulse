# ADR 0037 — El watchdog no dejaba de vigilar un ticket cancelado por el comprador

## Contexto

`ReservationWatchdogConsumer` arma un deadline al ver `TicketPurchaseRequested`/`ZoneSlotReserved`,
y solo lo desarma (`Untrack`) al ver `ZoneSlotReservationFailed` o `TicketPaymentSubmitted`. No
escuchaba `TicketPurchaseCancelled` — el evento que `CancelOrderCommand` (ADR 0034) publica para
cancelar un ticket antes de pagar.

Verificado en real contra el stack (`docker compose`, no Testcontainers): al cancelar un pedido de
3 líneas, uno de los tickets recibió `Cancelled` en `AwaitingPayment`, publicó `ReleaseZoneSlot` y
transicionó a `Confirming` (esperando `ZoneSlotReleased`). El watchdog, que seguía vigilando ese
ticket porque nadie lo desarmó, disparó `ReservationTimedOut` a los 60s. `TicketPurchaseSaga` no
tiene ninguna rama declarada para `TimedOut` en el estado `Confirming` (solo `Ignore(Cancelled)`,
`SlotConfirmed` y `SlotReleased`) — MassTransit lanzó `NotAcceptedStateMachineException`, el
ticket quedó congelado en `Confirming` para siempre: nunca llegó a `Rejected`, el pedido nunca
resolvió, y el aforo de esa línea quedó "fantasma" (reservado sin ningún ticket vivo que lo
explicara).

## Decisión

Dos cambios, cada uno cierra una mitad del problema:

1. `ReservationWatchdogConsumer` también consume `TicketPurchaseCancelled` y hace `Untrack` — un
   ticket cancelado ya no debe generar ningún timeout futuro, sea cual sea la vía de cancelación
   (evento real o `CancelOrderCommand`).
2. `TicketPurchaseSaga.Confirming` añade `Ignore(TimedOut)`, mismo patrón que el `Ignore(Cancelled)`
   que ya existía ahí — salvaguarda defensiva: si por cualquier otra vía no anticipada llegara un
   `TimedOut` mientras la Saga ya está resolviendo por pago o cancelación, no debe tumbar el
   consumer con una excepción no manejada.

## Alternativas descartadas

- **Solo el fix del watchdog (1)**: cierra el caso conocido, pero deja la Saga frágil ante
  cualquier otra vía no anticipada que genere un `TimedOut` tardío en `Confirming` — mismo tipo de
  riesgo que ya se aceptó (y se cerró) para `Cancelled` en ese estado.
- **Solo el fix de la Saga (2)**: evita la excepción, pero el ticket seguiría sin ninguna forma de
  resolver — `Ignore` no rechaza ni confirma, solo evita el crash. Necesita el fix (1) para que el
  timeout ni siquiera llegue a dispararse en el camino normal de cancelación.

## Consecuencias

- Verificado tras el fix: cancelar un pedido antes de pagar ya no dispara un timeout tardío que
  bloquee la resolución del ticket.
- Riesgo residual aceptado, mismo tipo que el resto del proyecto: `Ignore(TimedOut)` en `Confirming`
  significa que si algún día apareciera una vía real donde el ticket se queda esperando
  `SlotConfirmed`/`SlotReleased` que nunca llega (mensaje perdido de verdad, no solo watchdog mal
  desarmado), ya no hay ningún segundo timeout que lo rescate en ese estado — mismo trade-off que
  ADR 0027 acepta para `AwaitingLateReservationOutcome`.
