# ADR 0026 — Concierto: 6 zonas reales y rediseño visual del plano de compra

## Contexto

El set de zonas de concierto llevaba fijo desde su alta inicial (ver ADR 0017): 3 zonas —
`Pista` (60 aforo), `Grada Norte` (20) y `Grada Sur` (20) — sobre un plano esquemático (un
rectángulo de "pista/gradas" genérico con el escenario arriba, sin geometría propia).

Petición explícita del usuario (producto): el plano de compra de `ticket-checkout` para concierto
debe representar el recinto con **6 zonas seleccionables** sobre el mismo óvalo exterior que ya
usa partido (ver ADR 0024) — 3 gradas normales alrededor (mismo precio entre ellas) más una Pista
de pie y 2 zonas VIP junto al escenario, con el escenario como forma decorativa (2 rectángulos
solapados), no seleccionable.

## Alternativas descartadas

- **Mantener 3 zonas y simular 6 bloques visualmente**: descartado por el mismo motivo que ADR
  0024 con partido — el usuario pidió 6 zonas **elegibles**, no una subdivisión solo visual de 3
  zonas reales.
- **VIP al mismo precio que Pista** o **VIP entre Grada y Pista**: descartadas — el usuario
  confirmó explícitamente que VIP debe ser el precio más alto de los tres niveles (Grada < Pista <
  VIP), por ser las zonas más cercanas al escenario.
- **Geometría en el backend**: sigue descartado por el mismo motivo que ADR 0017/0024 — el layout
  de 6 zonas es fijo y conocido de antemano.

## Decisión

- **`AccessControl.ZoneService.ConcertZones`** pasa a 6 entradas: `Grada 1` (15), `Grada 2` (15),
  `Grada 3` (15), `Pista` (35), `VIP 1` (10), `VIP 2` (10) — aforo total 100, igual que antes
  (60+20+20), por continuidad, no por ningún requisito de negocio distinto.
- **`Ticketing.DefaultTicketTypeCatalog`** para concierto pasa a 6 tipos: `Grada 1`/`Grada 2`/
  `Grada 3` (25€, precio base, igual que la Grada Norte/Sur anterior), `Pista` (60€, sin cambio) y
  `VIP 1`/`VIP 2` (90€ — mismo precio que `Grada VIP Norte`/`Grada VIP Sur` de partido, ADR 0024,
  por continuidad entre tipos de evento, no por relación real en el backend). Los nombres
  coinciden a propósito con los de zona (mismo mecanismo de fusión por nombre del ADR 0016/0018).
- **`StadiumMap` de `ticket-checkout`** (concierto): mismo óvalo exterior que partido
  (`stadium-map__exterior`, `rx 85`). Las 3 gradas reutilizan literalmente las curvas de Norte/Sur/
  Oeste de partido (mismo `<path>`, ver `ZoneShape.tsx`) — mismo azul fijo sin borde, con semáforo.
  Pista/VIP1/VIP2 son rectángulos rectos (`rx 0`, sin las esquinas redondeadas de partido) del mismo
  azul. El escenario es 2 `<rect>` oscuros que se solapan formando una "L", con el rótulo
  "ESCENARIO" en vertical dentro del rectángulo grande — decorativo, no es una zona (`zones`), igual
  que ya lo era antes.
- **`StadiumMap` de `venue-ops`** (copia adaptada, ADR 0017): mismas 6 formas y mismo óvalo, pero
  sin semáforo — el bloque entero se tiñe por tier de ocupación (verde/naranja/rojo), igual que ya
  se decidió para partido en el ADR de esa fecha; operaciones necesita ver ocupación de un vistazo,
  no un azul fijo con luz de detalle.

## Consecuencias / riesgo aceptado

- Los tipos de entrada de concierto pasan de 3 a 6 (mismo mecanismo de fusión zona↔tipo por nombre
  del ADR 0016/0018, sin relación cruzada real en el backend) — cualquier evento de concierto
  creado después de este cambio usa el nuevo set; los eventos ya provisionados con el set de 3
  zonas antiguo no se migran (no hay migración de datos de producción en este proyecto).
- El aforo de Pista baja de 60 a 35 para dejar sitio a las 2 zonas VIP nuevas — cifra elegida por
  continuidad del aforo total (100), no por ningún requisito de negocio distinto.
