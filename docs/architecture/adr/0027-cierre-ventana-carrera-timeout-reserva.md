# ADR 0027 — Cierre de la ventana de carrera del timeout de reserva (ADR 0010)

## Contexto

ADR 0010 introdujo un watchdog en memoria que rechaza la compra (`ReservationTimedOut`) si
`AccessControl` no responde a `ReserveZoneSlot` dentro del deadline. Ese mismo ADR documentaba un
riesgo aceptado sin resolver: si la respuesta real de `AccessControl` (`ZoneSlotReserved`) llega
justo después de que el watchdog ya haya publicado `ReservationTimedOut`, la Saga (en
`TicketPurchaseSaga`) ya había finalizado en `Rejected` — `SetCompletedWhenFinalized()` borra la
instancia de Mongo al finalizar, así que ese `ZoneSlotReserved` tardío no tiene a qué
correlacionar y se descarta.

Esto no era solo teórico: es exactamente el bug reportado por un usuario real (zona "Grada VIP
Norte" con aforo reservado permanentemente sin ningún ticket confirmado) y también la causa más
probable de la intermitencia ya documentada en `.ia/KNOWN-ISSUES.md` para el test
`Bizum_compensa_y_rechaza_la_entrada` (el doble de `AccessControl` en Testcontainers responde
rápido, pero a veces no a tiempo para la Saga real del host de pruebas).

**Consecuencia del bug**: `AccessControl` sí incrementó `Zone.Reserved` (ver `Zone.cs`) al
procesar `ReserveZoneSlot`, pero nadie vuelve a publicar `ReleaseZoneSlot` para esa reserva — el
contador queda inflado para siempre, reduciendo el aforo vendible de la zona sin que exista
ningún ticket `Confirmed` ni `Rejected` pendiente que lo explique.

## Decisión

Cerrar la ventana en la propia Saga, sin nueva infraestructura (mismo criterio que ADR 0010 y ADR
0006): en vez de rechazar y finalizar inmediatamente en `When(TimedOut)` dentro de `Reserving`, la
Saga transiciona a un nuevo estado intermedio, `AwaitingLateReservationOutcome`, que sigue
escuchando la respuesta real de `AccessControl`:

- `ZoneSlotReserved` llega tarde → confirma que el aforo SÍ se reservó → se publica
  `ReleaseZoneSlot` para compensarlo (igual que el resto de caminos de compensación) y se
  transiciona a `Confirming`, que ya sabe resolver el rechazo final al ver `ZoneSlotReleased`.
- `ZoneSlotReservationFailed` llega tarde → confirma que el aforo nunca se reservó → se rechaza
  directamente, sin publicar `ReleaseZoneSlot` (nada que compensar).

En ambos casos se usa el mismo `RejectionReason = "ReservationTimedOut"` fijado en el momento del
timeout original, no el motivo interno de `AccessControl` — desde la perspectiva del fan, lo que
falló fue que la compra tardó demasiado, no el detalle interno de por qué.

## Alternativas descartadas

- **Segundo watchdog/deadline propio para `AwaitingLateReservationOutcome`**: cerraría también el
  caso en que la respuesta de `AccessControl` no solo tarda sino que se pierde del todo (broker
  caído, proceso muerto). Descartado por ahora: requeriría un mensaje nuevo solo para rearmar el
  tracker sin caer en un bucle de re-armado infinito (el propio `ReservationTimedOut` no puede
  reutilizarse tal cual para esto sin ese cuidado), infraestructura no justificada todavía para una
  demo — ver "Consecuencias" para el riesgo residual que esto deja aceptado.
- **`RowVersion`/concurrencia optimista en `Zone`**: se detectó como causa secundaria agravante
  (lost update entre `Reserve`/`Release`/`Confirm` concurrentes sobre la misma zona), pero no
  explica por sí sola el aforo fantasma permanente — es un problema de descuadres puntuales, no de
  reservas huérfanas para siempre. Queda fuera de alcance de este ADR; candidato a un ADR propio si
  se demuestra necesidad real (mismo criterio ADR 0006).
- **Idempotencia en `PurchaseTicketCommand`** (una Saga/reserva por fan+zona en curso): mitigaría
  que un reintento del usuario multiplique reservas simultáneas sobre la misma zona, pero no es la
  causa raíz del aforo fantasma — es un agravante que hace que el mismo bug escale más rápido en
  zonas pequeñas. Fuera de alcance de este ADR.

## Consecuencias / riesgo residual aceptado

- El caso reportado (respuesta tardía pero real) queda resuelto: el aforo ya no se queda fantasma
  solo porque `AccessControl` tardó más que el watchdog.
- **Riesgo residual documentado**: si la respuesta de `AccessControl` no solo tarda sino que se
  pierde del todo (el proceso está caído, el mensaje nunca llega), la Saga se queda esperando en
  `AwaitingLateReservationOutcome` indefinidamente — mismo tipo de trade-off ya aceptado en el
  resto del proyecto (arranque en frío, redelivery general) en vez de añadir infraestructura nueva
  para un caso no demostrado en esta demo. Candidato a revisar junto con el Inbox de `AccessControl`
  (ADR 0015) si algún día se demuestra necesidad real.
- Efecto colateral esperado (no verificado todavía en real): al dejar de rechazar de forma prematura
  cuando la respuesta de `AccessControl` solo iba unos milisegundos/segundos tarde, esto también
  debería reducir (no necesariamente eliminar) la intermitencia ya documentada en
  `.ia/KNOWN-ISSUES.md` para `Bizum_compensa_y_rechaza_la_entrada` en el arnés de Testcontainers —
  pendiente de reconfirmar con el mismo procedimiento que esa nota describe.
