# ADR 0037 — TimedOut tardío rompía TicketPurchaseSaga en estado Confirming

## Contexto

`TicketPurchaseSaga.During(Confirming)` ya maneja el caso de un `Cancelled` que llega tarde (tras
la propia transición a `Confirming`), ignorándolo con `Ignore(Cancelled)` — el pago o la
liberación de aforo ya están en marcha por su propio camino, así que un `Cancelled` adicional no
aporta nada. Le faltaba el mismo tratamiento para `TimedOut`.

El escenario real (confirmado por dos pruebas independientes contra el stack real): un ticket que
llega a `Confirming` por la vía de `Cancelled` (evento cancelado, o cascada de cancelación de
líneas hermanas de un pedido, ver ADR 0028/0034) nunca pasó por `PaymentSubmitted` — y es
justamente ese evento el que hace que `ReservationWatchdogConsumer` deje de vigilar el ticket
(`tracker.Untrack`). El watchdog sigue con el deadline de `AwaitingPayment` armado. Si ese deadline
vence mientras la Saga ya está en `Confirming` esperando la respuesta de `AccessControl`
(`ZoneSlotReleased`/`ZoneSlotConfirmed`), el evento `TimedOut` llega a un estado sin ninguna rama
declarada para él, y MassTransit lo trata como fallo de la máquina de estados
(`NotAcceptedStateMachineException`/`UnhandledEventException` según el punto exacto del pipeline).
El ticket queda huérfano — nunca resuelve a `Completed` ni `Rejected` — y el pedido que lo contiene
nunca cierra.

## Decisión

`Ignore(TimedOut)` en `During(Confirming)`, mismo patrón y mismo razonamiento que
`Ignore(Cancelled)` ya existente ahí: el resultado real (confirmar o liberar el aforo) ya está en
curso por otro camino, así que un timeout tardío no debe tratarse como fallo.

## Alternativas descartadas

- **Hacer que la cascada de cancelación (`TicketPurchaseCancelled`) también dispare un
  `tracker.Untrack`**: cerraría la ventana solo para ese origen concreto, dejando sin cubrir
  cualquier otro camino futuro que lleve a `Confirming` sin pasar por `PaymentSubmitted`.
  `Ignore(TimedOut)` es la solución genérica correcta para el estado, no para el origen.
- **Registrar el evento pero no actuar sobre él** (rama explícita `When(TimedOut).Then(log)` sin
  transición): funcionalmente equivalente a `Ignore`, pero `Ignore` ya expresa exactamente la
  intención (evento reconocido, sin efecto) sin código de más.

## Consecuencias

- Verificado con un test de Saga (harness in-memory) que reproduce la secuencia exacta:
  `ZoneSlotReserved` → `TicketPurchaseCancelled` (transiciona a `Confirming`, publica
  `ReleaseZoneSlot`) → `ReservationTimedOut` tardío (ahora ignorado) → `ZoneSlotReleased` resuelve
  igual que sin el timeout tardío, con el motivo de rechazo original intacto.
- Mismo tipo de riesgo residual ya aceptado en el resto del proyecto para ventanas de carrera
  (ADR 0010/0027): esto no evita que el watchdog dispare tarde, solo evita que ese disparo tardío
  rompa la Saga.
