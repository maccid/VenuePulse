# ADR 0013 — Motor de simulación de demo en AccessControl (Bloque 8)

## Contexto

El demo en vivo necesita "dar vida" a los contadores: el día del evento, las entradas confirmadas
(`ConfirmedSold`) y las personas que pasan el torniquete (`CheckedIn`) deben subir en tiempo real
delante del operador, sin esperar a que un humano escanee cientos de tickets. Además, el
Bloque 11 (predicción de ocupación) necesita una serie temporal de ocupación generada por el propio
sistema para poder entrenar/validar un modelo estadístico honesto.

Antes de esto, la única forma de subir `ConfirmedSold` era una compra real vía Saga, y el único
`CheckedIn` era un escaneo real. No es viable reproducirlo a escala de forma manual.

## Decisión

Un **motor de simulación controlable** vive **solo en `AccessControl`** (es el dueño de los
contadores de zona, del SignalR y de la DB), con estas piezas:

| Pieza | Ubicación | Qué hace |
|---|---|---|
| `SimulationEngine` (singleton, estado en memoria) | Application/Simulation | Un run por evento; avanza un reloj virtual y aplica las curvas |
| `SimulationCurves` (funciones puras) | Application/Simulation | `Sales(p)=p²`, `Arrivals(p)=1-(1-p)³`, `[0,1]→[0,1]` |
| `SimulationMode` | Application/Simulation | `Sales` (los 7 días previos) / `Arrivals` (las 3 horas previas) |
| `SimulationTickWorker` (BackgroundService ~500ms) | Infrastructure | Resuelve servicios scoped (zonas, snapshots) desde un scope nuevo por tick y llama a `AdvanceAsync` |
| `SimulationController` | Api | `POST api/simulation/events/{id}/start\|stop\|reset`, `GET .../status` |
| `occupancy_snapshots` (`ZoneOccupancySnapshot`) | Domain + DB | Serie temporal (tiempo simulado, `ConfirmedSold`, `CheckedIn`) que alimenta el Bloque 11 |

- Las dos señales se separan en fases (`Sales` primero, `Arrivals` después) para que el demo no
  confunda ventas con torniquete — el problema que motivaba el bloque.
- El operador elige **fase, duración real (intensidad), objetivo de venta % y no-shows %** desde
  `venue-ops`. No es caos autónomo: solo avanza lo que se le pide arrancar.
- Los no-shows son naturales: `RecordSimulatedCheckIns` nunca supera `ConfirmedSold`, así que
  `CheckedIn < ConfirmedSold` queda representado solo.
- `Ticketing` no se toca: `AccessControl` no consulta la DB de otro servicio (bounded contexts),
  `venue-ops` trae `EventStartsAtUtc` del catálogo de Ticketing como dato del request.

## Justificación

- **Por qué AccessControl y no un servicio nuevo**: no hay una necesidad real de escalar el motor de
  forma independiente, y añadir un servicio solo para esto es sobre-ingeniería (ver README, "qué NO
  se construyó"). El motor es un worker in-process que ya comparte la DB donde viven los contadores.
- **Por qué curvas `p²` / `1-(1-p)³` y no datos reales**: modelan los dos comportamientos que
  importan — "la venta acelera en los últimos días" (cuadrática, arranque lento) y "la
  llegada se concentra en las horas previas" (cúbica, más abrupta). Un dataset sintético con
  forma rara no tendría sentido.
- **Por qué estado en memoria y no persistido**: es un generador de señales para el demo, no un
  proceso de negocio. Si el servicio cae, se reinicia el run; lo que sí se persiste es el resultado
  (`occupancy_snapshots`), que es lo que consume el Bloque 11.
- **Por qué singleton + worker scoped**: el motor (singleton) no puede inyectar repositorios scoped
  (DbContext), así que el estado vive en el singleton y el worker resuelve los servicios por tick
  desde un scope nuevo — patrón estándar para hosted services que tocan EF.
- **Por qué tiempo simulado en el snapshot y no el reloj real**: el Bloque 11 modela la curva de
  llegada sobre una línea temporal plausible (días/horas antes del evento), no sobre cuándo el
  operador pulsó el botón.
