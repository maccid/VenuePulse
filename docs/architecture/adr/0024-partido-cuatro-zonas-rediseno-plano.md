# ADR 0024 — Partido: 4 zonas reales y rediseño visual del plano de compra

## Contexto

El Bloque 15/16 (ver `.ia/TODO.md`, ronda 3) había fijado deliberadamente un set de 3 zonas para
partido (`Puerta Norte`/`Puerta Sur` + `Grada VIP`), descartando explícitamente una versión de 4
zonas por considerarla sobreingeniería en ese momento (commit `fix: keep develop zone set after
merge`).

Petición explícita del usuario (producto): el plano de compra de `ticket-checkout` para partido
debe representar un estadio con **4 gradas seleccionables** (Norte/Sur + 2 laterales), con el
campo en verde y el fondo del recinto en gris — revirtiendo la decisión anterior. Además, las
gradas deben verse en un azul fijo (no con el semáforo verde/naranja/rojo de ocupación) con un
indicador de estado sutil y parpadeante en su lugar, y el botón "Volver al catálogo" de
`CheckoutHeader` debe quedar pegado a la derecha de la cabecera en vez de encima de la tarjeta del
evento.

## Alternativas descartadas

- **Mantener 3 zonas en el backend y simular 4 bloques visualmente en el SVG** (repartiendo una
  zona real entre dos posiciones, o dejando una de las 4 posiciones decorativa/no comprable):
  descartado porque el usuario pidió expresamente 4 zonas **elegibles** — un bloque decorativo no
  cumple ese requisito y habría sido peor que no tener el 4º bloque.
- **Semáforo verde/naranja/rojo también en las gradas de partido** (igual que hoy en concierto):
  descartado a petición explícita — las gradas de partido usan un azul de marca fijo; la ocupación
  se indica con una luz pequeña y parpadeante superpuesta, no tiñendo todo el bloque.
- **Geometría en el backend** (`PositionX/Y` en `Domain.Zone`): sigue descartado por el mismo
  motivo que ADR 0017 — el layout de 4 zonas sigue siendo fijo y conocido de antemano, no hay
  necesidad real de persistir coordenadas.

## Decisión

- **`AccessControl.ZoneService.MatchZones`** pasa a 4 entradas: `Puerta Norte` (40), `Puerta Sur`
  (40), `Grada Este VIP` (8), `Grada Oeste VIP` (7) — sustituye a la `Grada VIP` (15) única.
  Concierto no cambia (`Pista` + `Grada Norte`/`Grada Sur`).
- **`Ticketing.DefaultTicketTypeCatalog`** para partido pasa a 3 tipos: `General` (40€, sigue
  cubriendo Puerta Norte/Sur), `Grada Este VIP` (90€) y `Grada Oeste VIP` (90€) — los nombres
  coinciden a propósito con los de zona (mismo mecanismo de fusión por nombre del ADR 0016/0018,
  sin relación cruzada real en el backend).
- **`StadiumMap` de `ticket-checkout`** (partido): fondo gris del estadio completo (nuevo
  `<rect>` grande, huecos en las esquinas donde no llegan las gradas), campo verde central con
  líneas de un campo de fútbol real (banda central, círculo central, áreas, punto de penalti,
  puramente decorativo), y las 4 gradas en azul fijo (`stadium-map__zone--stand`, ya no
  `--low/--mid/--high`). La ocupación se indica con una luz pequeña (halo + núcleo) en la esquina
  de cada grada, coloreada por el mismo tier de siempre (verde/naranja/rojo) y animada con un
  parpadeo sutil (CSS `@keyframes`, sin JS ni librerías nuevas). El plano de concierto no se toca
  (semáforo de color completo, sin estas luces) — pendiente de un rediseño propio, ya avisado por
  el usuario para otra tarea.
- **`StadiumMap` de `venue-ops`** (copia adaptada, ADR 0017): solo se actualizan las coordenadas
  del layout de partido a las 4 zonas nuevas, sin tocar su semáforo de color — el rediseño visual
  pedido es específico de la experiencia de compra del fan.
- **`CheckoutHeader`**: la cabecera pasa a fila (`flex-direction: row`, `justify-content:
  space-between`) con la tarjeta del evento a la izquierda y el botón "Volver al catálogo" pegado
  a la derecha, con el estilo de botón sólido por defecto en vez de enlace de texto.

## Consecuencias / riesgo aceptado

- Revierte una decisión ya documentada (Bloque 15/16) a petición directa del usuario — se acepta
  la inconsistencia con el histórico de `.ia/TODO.md` de rondas anteriores; este ADR es la fuente
  de verdad vigente sobre el número de zonas de partido.
- Las 2 gradas VIP (Este/Oeste) reparten la capacidad que antes tenía una sola `Grada VIP` (15 →
  8+7): cifra elegida por continuidad de aforo total, no por ningún requisito de negocio distinto.
- El plano de concierto queda visualmente inconsistente con el de partido hasta que se aborde su
  propio rediseño (fuera de alcance de este ADR).
