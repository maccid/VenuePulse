# ADR 0017 — Wizard de compra y plano del recinto como mapeo estático en frontend

## Contexto

Bloque 13 del plan de mejoras: dar más realismo/funcionalidad a `ticket-checkout` y `venue-ops`
más allá del CSS — separar la compra en pasos (tipo de entrada → zona → nombre y confirmación) y
representar la elección/estado de zona con un plano visual del recinto en vez de un `<select>`
plano de texto, tanto en la experiencia del fan como en operaciones.

El problema: hoy las zonas (`AccessControl.Domain.Zone`) no tienen ningún campo de
posición/geometría — solo `Name`/`Capacity`/`Reserved`/`ConfirmedSold`/`CheckedIn`. Y no son
arbitrarias: `ZoneService.FixedZones` aprovisiona siempre las mismas 3 (`"Puerta Norte"`,
`"Puerta Sur"`, `"Grada VIP"`) para cualquier evento, vía `EventCreatedConsumer`. Representar un
plano espacial real "como debería ser" exigiría añadir geometría al modelo de dominio para un
valor que hoy es constante.

## Alternativas descartadas

- **Geometría en el backend** (`PositionX/Y`, `Shape`, o similar en `Domain.Zone` + migración EF +
  propagación en `ZoneOccupancyDto`): ingeniería especulativa para 3 nombres de zona hardcodeados
  e idénticos en todos los eventos — mismo criterio que llevó a rechazar la relación cruzada
  Zone↔TicketType en ADR 0016. Se revisita si algún día las zonas dejan de ser fijas por evento.
- **Librería de gráficos/mapas** (d3, recharts, konva...): ninguna de las 3 apps React tiene hoy
  una dependencia de este tipo; un plano de 3 zonas fijas es un SVG a mano de ~80 líneas — no
  justifica añadir una dependencia nueva.
- **Componente de plano compartido entre `ticket-checkout` y `venue-ops`** (paquete npm común o
  workspace): ADR 0004 ya fijó que los micro frontends React no comparten código más allá del
  contrato del Web Component. Introducir tooling de workspace para un único componente de ~80
  líneas usado en 2 sitios es más coste que el que ahorra.

## Decisión

- **`StadiumMap` = mapeo estático en el frontend, indexado por `zone.name`.** Un objeto
  `ZONE_LAYOUT: Record<string, {x,y,width,height}>` con las 3 posiciones fijas conocidas, sin
  tocar el backend ni el DTO. Zonas cuyo nombre no esté en el mapeo (no debería ocurrir con el seed
  actual, pero es una lista abierta) caen a un fallback en texto simple debajo del plano — nunca
  se ocultan.
- **Duplicado, no compartido**, entre `ui/ticket-checkout/src/components/StadiumMap.tsx` (modo
  interactivo: `onSelect`, colorea por `availableToSell` — la métrica de venta que le importa al
  fan) y `ui/venue-ops/src/components/StadiumMap.tsx` (solo lectura, colorea por `checkedIn` — la
  métrica de aforo real que le importa a operaciones, igual que ya usa `ZoneGauge`). Cada copia usa
  los tokens de color de su propia app (`--tc-*` / `--vp-*`).
- **Wizard de 3 pasos en `ticket-checkout`** (`PurchaseWizard` sustituye a `PurchaseForm`): paso 1
  tipo de entrada, paso 2 zona (plano), paso 3 nombre + confirmación + envío. `App.tsx` no cambia
  su lógica de fetch/SignalR, solo pasa los mismos datos/handlers a un componente distinto.
- **`holder` (nombre del comprador) se captura en el paso 3 del wizard, no en `shell`.** Antes vivía
  como input siempre visible en `EventCatalog` (`shell`) y viajaba fijo como atributo del Web
  Component `<ticket-checkout-app holder="...">`. `shell` no tenía ningún otro uso de ese estado.
  Ahora el atributo `holder` sigue existiendo como valor opcional de precarga (con el mismo
  fallback `'Invitado'` que ya tenía `main.tsx`), pero es editable dentro del wizard — el
  componente que ejecuta la compra es dueño del dato que necesita para ejecutarla.
- De paso, al reescribir el paso de zona se corrigió una inconsistencia encontrada: el `<select>`
  original mostraba "libres" como `capacity - confirmedSold`; el nuevo paso usa `availableToSell`
  (el campo que el backend ya calcula correctamente, descontando `reserved`).
- **Sin cambios de backend** en este bloque.

## Consecuencias / riesgo aceptado

- Si en el futuro las zonas dejan de ser 3 fijas por evento (p. ej. layouts distintos por tipo de
  evento), `ZONE_LAYOUT` deja de cubrir el caso y hay que revisitar esta decisión — aceptado como
  límite conocido del alcance actual de la demo.
- El plano de `ticket-checkout` y el de `venue-ops` son dos ficheros casi idénticos con métricas de
  color distintas (venta vs. aforo) — duplicación intencional, no descuido (ver alternativas
  descartadas).
