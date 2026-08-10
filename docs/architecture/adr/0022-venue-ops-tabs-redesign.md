# ADR 0022 — Rediseño de `venue-ops`: pestañas, carga perezosa y dos modos

## Contexto

Bloque 18 de la ronda 3 (`.ia/TODO.md`): con el ciclo de vida del evento (Bloque 15), la
cancelación con reembolso (Bloque 16/17) e ingresos (Bloque 17) ya mergeados a `develop`,
`venue-ops` seguía con un `<select>` de evento y todo apilado verticalmente (escaneo, aforo,
simulación, predicción, actividad) sin distinguir "vender" de "controlar el acceso", y sin forma de
dar de alta/cerrar/cancelar un evento desde la propia Operaciones.

## Decisión 1 — Pestañas estilo Chrome en vez de `<select>`, con una pestaña "+" fija

`EventTabs` sustituye al selector. Sin filtro de "pasados" (decisión ya confirmada con el usuario
en la ronda: Operaciones ve siempre todo, incluidos eventos cerrados/cancelados/pasados — a
diferencia del catálogo de fans en `shell`). Cada pestaña muestra una insignia si
`Status !== OnSale` ("Venta cerrada"/"Cancelado"). La pestaña "+" no representa un evento real: es
un id centinela (`NEW_EVENT_TAB_ID`) que activa `EventCreateForm` (nombre, tipo, fecha, relevancia)
sobre `POST /api/events` (Bloque 15/ADR 0019). Si el catálogo está vacío al arrancar, la app aterriza
directamente en el formulario de alta en vez de en una pestaña inexistente.

## Decisión 2 — Carga perezosa por pestaña, un único socket de cada hub a nivel de `App`

Zonas e ingresos de un evento solo se piden la primera vez que su pestaña se activa
(`zonesByEvent`/estado en `App`, comprobado antes de disparar el `fetch`). Los dos hubs de SignalR
(`/hubs/occupancy`, `/hubs/tickets`) se conectan una sola vez en `App`, no uno por pestaña: cada
mensaje se enruta por `eventId` a un diccionario `Record<eventId, ...>`, y solo actualiza pestañas
ya cargadas (si `zonesByEvent[eventId]` es `undefined`, el mensaje se ignora — nadie lo ha pedido
todavía). Esto reproduce el mismo criterio ya usado en Bloques anteriores para no montar N
suscripciones redundantes.

**Caso real cubierto**: crear un evento nuevo desde la propia pestaña "+" deja `zonesByEvent[id]`
vacío hasta que `AccessControl` aprovisiona sus zonas de forma asíncrona (`EventCreatedConsumer`,
ver `.ia/KNOWN-ISSUES.md`). El handler de `occupancyChanged` no solo actualiza zonas ya conocidas
por `zoneId`, también **añade** las que todavía no estaban en el array — si solo actualizara por
coincidencia, un evento recién creado nunca vería aparecer sus zonas sin recargar la pestaña a mano.

Solo la pestaña activa monta su `EventWorkspace` (las demás se desmontan) — evita que el
`SimulationPanel` de cada pestaña visitada siga sondeando su endpoint de estado cada 500ms en
segundo plano. El estado de zonas/actividad vive en `App`, así que volver a una pestaña ya visitada
no repite el `fetch` inicial y conserva lo que llegó por socket mientras estaba en background.

## Decisión 3 — Dos modos (`ModeToggle`) en vez de apilado vertical, `StadiumMap` compartido con `mode`

`ModeToggle` (Ventas/Ocupación) decide qué grid se renderiza dentro de la pestaña activa:

- **Ventas**: `SimulationPanel` (arranca en fase `Sales`), `RevenueWidget` (nuevo, consume
  `GET /api/tickets/events/{id}/revenue` del Bloque 17), `ActivityFeed`, `StadiumMap` coloreado por
  venta, y los botones "Cerrar venta"/"Cancelar evento" (solo si `Status === OnSale`, con
  confirmación vía `window.confirm` — sin librería de modales nueva).
- **Ocupación**: `ZonesDashboard` (ahora solo el grid de `ZoneGauge`, ya no embebe el plano),
  `SimulationPanel` (fase por defecto condicionada, ver Decisión 4), `ScanPanel`, `StadiumMap`
  coloreado por check-in, y `PredictionPanel` condicionado (Decisión 5).

`StadiumMap` gana la prop `mode: 'sales' | 'checkin'` en vez de duplicarse: `'sales'` sigue
coloreando por `capacity - availableToSell` (la señal que se mueve antes del evento); `'checkin'`
colorea por `checkedIn / capacity` (la señal que importa el día del evento). Como ahora se usa en
ambos modos con distinto significado, se extrajo de `ZonesDashboard` (que antes lo embebía) a
`EventWorkspace`, para que ambos modos lo monten con su propio `mode` sin que `ZonesDashboard` tenga
que saber de colores de venta.

Al montarse solo un grid a la vez, cada modo tiene su propia instancia de `SimulationPanel` — cambiar
de modo la remonta (pierde sliders locales, no el estado real del backend: el `fetch` de estado se
repite al montar y refleja la simulación en curso igual). Se aceptó frente a compartir una única
instancia porque mantener dos fases (`Sales`/`Arrivals`) visibles a la vez sin remontar habría
necesitado sincronizar el `mode` del panel con el `ModeToggle` externo — acoplamiento que el punto
siguiente evita deliberadamente en la dirección contraria.

## Decisión 4 — Sigue sin fusionarse "cerrar venta" con la fase del simulador

Mismo criterio que ADR 0019/sección 3 del plan de ronda: `EventStatus` (Ticketing) y
`SimulationMode` (AccessControl) siguen siendo conceptos distintos, sin ninguna llamada nueva entre
servicios. El único punto de contacto vive en el frontend: el `SimulationPanel` del modo Ocupación
recibe `defaultMode={event.status === 'OnSale' ? 'Sales' : 'Arrivals'}` — antes de cerrar la venta
arranca igual que el de Ventas (`Sales`), y en cuanto "Cerrar venta" cambia el `Status` del evento
(propagado por `onEventUpdated`, actualización optimista tras el `204` de
`POST /events/{id}/close-sales` — no hay hub que empuje cambios de `Event`), la siguiente vez que se
abra el modo Ocupación de esa pestaña arranca ya en `Arrivals`. Se usa `key={defaultMode}` sobre el
panel para que el cambio de `Status` lo remonte incluso si la pestaña ya estaba abierta en modo
Ocupación, sin esperar a un cambio de modo manual.

## Decisión 5 — `PredictionPanel` condicionado a ventas confirmadas **en la zona**, no a ingresos de `CheckIn`

`EventWorkspace` levanta el `fetch`+sondeo de `EventRevenueDto` (mismo patrón de `setInterval` que
ya usaba `PredictionPanel` para sí mismo) para `RevenueWidget` (presentacional, solo pinta lo que
recibe). Para la condición de montaje de `PredictionPanel` **no se usa `revenue.confirmedCount`**:
se probó en real contra `docker compose up --build` y el motor de simulación (Bloque 8, el camino
**principal** para animar la demo en vivo) mueve `ConfirmedSold` de la zona directamente en
`AccessControl` sin pasar nunca por `CheckIn` — con esa condición, una demo que solo usa el
simulador (sin ninguna compra real vía `ticket-checkout`) dejaba `revenue.confirmedCount` siempre a
`0` y el panel de predicción **no se montaba jamás**, aunque el simulador ya hubiera generado
`occupancy_snapshots` de sobra para una predicción real. Se cambió a
`zones.some(z => z.confirmedSold > 0)`: `ConfirmedSold` sube en los dos caminos (compra real
confirmada o venta simulada), así que el panel aparece en cuanto hay algo que predecir
independientemente de cuál de los dos generó esa venta.

## Decisión 6 — `RevenueWidget` añade una tercera cifra estimada a precio de catálogo

Verificado en real (`docker compose up --build`) que, tras la Decisión 5, el `RevenueWidget` seguía
mostrando siempre `0,00 €` en cualquier demo que solo usara el motor de simulación — coherente con
que `revenue` viene de `CheckIn` (compras reales), pero visualmente "parece roto" en el camino
principal de la demo. Se añade una tercera cifra, **`estimatedRevenue`**, calculada en el frontend
como `Σ zone.confirmedSold × ticketType.effectivePrice` (fusionando zona↔tipo de entrada por
nombre) — la misma señal de `confirmedSold` que ya usa la Decisión 5, así que sube igual con
compras reales o con venta simulada. Se etiqueta explícitamente como "estimado a precio de
catálogo (incluye venta simulada)", **nunca mezclada** con los dos números reales de `CheckIn`
(confirmados/pendientes) — mismo principio de "heurísticas transparentes, nunca datos falsos
disfrazados de reales" que ya sigue `DynamicPricing` (ADR 0016).

**Bug real encontrado de paso, verificado contra el stack, fuera de alcance de este bloque**: la
fusión zona↔tipo de entrada que ya usa `ticket-checkout` (`zoneTicketType.ts`, ADR 0004/0016) solo
compara por **igualdad exacta** de nombre. Contra datos reales del seed (`Grada VIP` como zona,
`VIP` como tipo de entrada) esa igualdad nunca se cumple, así que la zona VIP cae al tipo `General`
por el *fallback* — un fan que compra en `Grada VIP` vería (y pagaría) el precio de `General`, no el
de `VIP`. Se documenta en `.ia/KNOWN-ISSUES.md`; no se corrige en `ticket-checkout` desde esta rama
porque ese fichero lo está tocando `opencode` en paralelo (Bloques 19/20) sobre la misma base de
datos del stack — corregirlo aquí arriesgaría un conflicto de merge sin necesidad. La copia nueva de
`venue-ops` (`zoneTicketType.ts`) ya nace con la regla corregida (igualdad exacta → nombre de tipo
contenido en el nombre de zona, el más largo que coincida → `General`/primero), verificada contra
los mismos datos reales.

## Alternativas descartadas

- **Mantener todas las pestañas montadas (solo ocultas con CSS) para no perder estado local**:
  descartado — multiplicaría los sondeos de `SimulationPanel`/`PredictionPanel` por cada pestaña
  visitada aunque no fuera la activa, sin beneficio real frente a recargar desde el estado ya
  cacheado en `App` (Decisión 2).
- **Un único `SimulationPanel` compartido entre los dos modos** (en vez de una instancia por modo):
  descartado por el acoplamiento que introduciría entre `ModeToggle` (UI) y la fase de simulación
  (dato de negocio/demo) — más simple aceptar el remount y que cada modo declare su propio valor por
  defecto.
- **Endpoint o tabla nueva para el estado de "cerrar venta pendiente de reflejar"**: descartado, la
  actualización optimista tras el `204` es suficiente para una demo sin usuarios concurrentes reales
  editando el mismo evento.

## Consecuencias / riesgo aceptado

- El `Status` de un evento mostrado en pestañas se actualiza de forma optimista tras
  `close-sales`/`cancel` (sin refetch ni hub) — si la llamada tiene éxito en el backend pero la
  respuesta se pierde en el cliente, la pestaña quedaría desincronizada hasta recargar la página.
  Aceptado por el mismo motivo que otros riesgos ya documentados en `.ia/KNOWN-ISSUES.md`: sin
  usuarios concurrentes reales en la demo.
- Build/lint en verde (`npm run build`, `npm run lint` en `ui/venue-ops`, oxlint sin hallazgos).
- Sin cambios de contrato en ningún backend — solo consumo de endpoints ya existentes de los
  Bloques 15/16/17.
