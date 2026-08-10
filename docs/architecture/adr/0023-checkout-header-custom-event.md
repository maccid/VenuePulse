# ADR 0023 — Cabecera del checkout y comunicación Web Component → shell por `CustomEvent`

## Contexto

Bloque 19+20 de la ronda 3 (`.ia/TODO.md`, opencode): el checkout (`ticket-checkout`) mostraba
solo "Comprar entrada" — sin contexto de qué evento se estaba comprando. Al entrar desde el
catálogo (`shell`), el usuario no ve ni el nombre del evento ni cómo volver atrás de forma clara.
Además, el total a pagar solo se veía en la línea "Total" del carrito, nunca en el botón que
dispara el pago.

Dos necesidades:

- **Cabecera con info del evento** (nombre, categoría, fecha — mismo estilo que la `EventCard` del
  catálogo) y un **botón "Volver al catálogo"**.
- El **total visible en el botón de pago** ("Pagar todo — 135,00 €") y un aviso cuando el evento
  ya no está a la venta.

El micro frontend `ticket-checkout` se monta como Web Component en **light DOM** dentro de la
página de `shell` (ADR 0004): no tiene Shadow DOM, comparte documento y CSS, y no tiene acceso al
estado de React del host. Esto condiciona cómo debe conseguir la info del evento y cómo debe
avisar al host para navegar.

## Decisión 1 — La cabecera obtiene el evento con `GET /api/events` (sin endpoint nuevo)

`CheckoutHeader` necesita `name`, `type`, `startsAtUtc` y `venueName`. El Web Component solo
recibe `event-id` por atributo; no hay endpoint de detalle (`GET /events/{id}`) y no merece la
pena crearlo: el catálogo ya carga la lista completa con `GET /api/events`, y el id llega a mano,
así que se busca el evento en esa lista por id.

- Se descartó pasar la info del evento por atributos del Web Component (`name`, `date`...): el
  atributo ya es el `eventId` y duplicar el resto del resumen en el contrato del componente
  (declarado en `ui/shell/src/custom-elements.d.ts`) sería un acoplamiento de contrato para
  exactamente el mismo dato que el componente ya puede leer solo.
- Se descartó un endpoint nuevo `GET /api/events/{id}`: resolver un detalle puntual con la lista
  que el catálogo ya tiene, y que además sirve el mismo shape (`EventSummaryDto`), es más simple.
  Si algún día la lista dejara de bastar, ese sería el momento de añadir el endpoint.

## Decisión 2 — El botón "Volver al catálogo" avisa por `CustomEvent('back-to-catalog')`

El botón vive dentro del micro frontend, pero la navegación es responsabilidad del host
(`shell`), que es quien controla `selectedEventId`. El Web Component no puede llamar a `onBack`
de React directamente (vive en otro bundle, sin paso de props), así que **despacha un
`CustomEvent('back-to-catalog', { bubbles: true, composed: true })`** sobre `window`, y
`CheckoutPanel` (en `shell`) lo escucha y reenvía como su `onBack` habitual.

- `composed: true` es lo que permite que un evento de un custom element atraviese cualquier
  shadow boundary; en este componente sin Shadow DOM sería indiferente, pero se fija igualmente
  para no depender de ese detalle.
- En **modo standalone** (`npm run dev` de `ticket-checkout`, sin shell que escuche), `main.tsx`
  escucha el mismo evento y recarga sin `eventId` — el equivalente dev de "volver al catálogo".
- Se descartó un evento `CustomEvent` con `detail` (p. ej. datos del evento): la navegación no
  necesita datos, solo la orden de volver; si algún día hiciera falta contexto, se añadiría.

## Decisión 3 — El plano pasa a sección propia y el total se mueve al botón de pago

- El `StadiumMap` dejaba de compartir la columna estrecha con el carrito (cambio de
  estructura/CSS dentro de `PurchaseFlow`, **sin cambios de props**): el plano ahora vive en su
  propia sección más amplia.
- El total ya lo calculaba `CartPanel` (`activeLines.reduce(...)`); ese valor se pasa ahora a
  `PaymentForm` y el botón de envío muestra **"Pagar todo — {total} €"** en lugar del genérico
  "Confirmar y pagar".
- Si el evento ya no está `OnSale` (cerrado/cancelado, campo que `EventSummaryDto` ya expone
  desde el Bloque 15), el flujo de compra se sustituye por un aviso — coherente con el filtro del
  Bloque 20 en el catálogo de `shell`.

## Alternativas descartadas

- **Pasar `onBack` al Web Component por atributo/propiedad**: no existe el mecanismo (light DOM +
  bundles separados, ADR 0004); habría que montar el componente desde React con refs a la
  propiedad del elemento, más frágil que un evento del DOM.
- **`window.location` para volver**: el shell es una SPA que controla el estado con React; saltar
  a otra URL duplicaría el estado de navegación.
- **Badge "no a la venta" en el propio checkout en vez de sustituir el flujo**: el aviso claro
  evita mostrar un wizard muerto (plano, carrito, pago) que no se puede usar.

## Consecuencias / riesgo aceptado

- `CustomEvent('back-to-catalog')` es un contrato implícito entre dos apps (el emisor en
  `ticket-checkout`, el receptor en `shell`), igual que el propio tag `<ticket-checkout-app>` —
  se documenta en ambos lados (comentario en `CheckoutHeader.tsx` y en `CheckoutPanel.tsx`).
- Cambiar el nombre del evento rompería la navegación del checkout silenciosamente: mitigado con
  un nombre descriptivo y documentado; a esta escala (2 apps del mismo repo, ADR 0004) no merece
  un módulo de contratos compartido.
- Bloque 20 (filtro "no pasados" en el catálogo de `shell`) se cuela en la misma rama sin ADR
  propio: es una decisión de producto simple (no mostrar eventos que ya no se pueden comprar), no
  de arquitectura.
- Tests: sin tests de UI en este repo (apps frontend sin framework de tests, ver ADR 0004); la
  verificación es build + revisión manual. Backend: `DefaultTicketTypeCatalog` alineado con el
  set de zonas 3+3 (ADR 0018) — los nombres `General`/`Grada VIP` y `Pista`/`Grada Norte`/
  `Grada Sur` casan por coincidencia de nombre con sus zonas; tests de `Ticketing` actualizados.
