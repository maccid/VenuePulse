# ADR 0012 — Predicción de ocupación en `AccessControl` (Bloque 11)

## Contexto

Bloque 11 del plan de mejoras: predecir la ocupación de un evento con un **modelo estadístico
honesto** — regresión/exponential smoothing sobre datos que el propio sistema genera (el motor de
simulación del Bloque 8 escribe `occupancy_snapshots` con tiempo simulado), no "ML mágico". La
métrica que de verdad opera un estadio es **"¿cuántos pasarán el torniquete y cuándo?"**, es decir
la curva de llegada + los no-shows sobre `ConfirmedSold` — no las ventas.

Los datos existen y están bien formados: por cada tick de la fase de llegadas, el simulador
persiste un `ZoneOccupancySnapshot` por zona con `RecordedAtUtc` (tiempo simulado dentro de las 3
horas previas al evento), `ConfirmedSold` y `CheckedIn`. El problema es convertirlos en una
predicción fiable en el demo en vivo.

## Alternativas descartadas

- **Servicio nuevo (`predictions-api`)**: repetir la discusión del ADR 0013 — no hay una necesidad
  real de escalar el modelo de forma independiente, y el modelo consume la DB de snapshots que es
  de `AccessControl`. Añadir un servicio solo para esto es sobre-ingeniería (ver README, "qué NO se
  construyó").
- **ML / caja negra**: descartado por producto — el modelo debe poder explicarse de forma
  simple, no meter un `RandomForest` sobre datos sintéticos. Mismo criterio que los Bloque 9 y 10
  (heurísticas/reglas transparentes, ver `.ia/PRODUCT-VISION.md`).
- **Ajustar forma Y asíntota (regresión no lineal de 2 parámetros)**: con pocos puntos al inicio de
  la ventana la forma es **inidentificable** — infinitas combinaciones (forma, asíntota) explican
  igual los datos y la predicción se dispara. Fijar la forma y ajustar solo la asíntota da un
  estimador estable en el momento en que más ruido hay (los primeros minutos de llegadas).
- **Exponential smoothing de la tasa instantánea**: válido, pero proyecta peor la curva completa
  que una asíntota ajustada sobre la forma de llegada, y no da una bondad de ajuste tan directa
  como el R².

## Decisión

Un **endpoint en `AccessControl`** (`GET api/prediction/events/{eventId}?eventStartsAtUtc=`) que
encaja un modelo de un parámetro sobre los snapshots:

- **Agregación a nivel de evento**, como en el Bloque 9: la serie es por zona pero la métrica
  operativa es global (suma de zonas por instante de tiempo simulado). Una predicción por puerta
  duplicaría la superficie sin añadir nada a la demo.
- **Forma fija `F(p) = 1-(1-p)³`** — la misma curva de llegada que genera el simulador
  (`SimulationCurves.Arrivals`): el sistema predice con la forma de llegada que él mismo genera.
  Esto es una decisión de modelado explícita y documentada, no un accidente.
- **Mínimos cuadrados lineales sobre la asíntota A** (llegadas totales esperadas): minimizar
  `Σ(yᵢ - A·F(pᵢ))²` → `A = Σ y·F / Σ F²`. Cierre analítico, sin optimizador. La asíntota se
  recorta a los invariantes `[última llegada observada, ConfirmedSold]` — nunca predice por debajo
  de lo ya llegado ni por encima de lo vendido.
- **No-shows = `1 - A/ConfirmedSold`**. Son estimados por el ajuste, no un parámetro adivino.
- **Sin historia de llegadas** (fase de ventas, `CheckedIn=0` o < 2 puntos): no hay nada que
  ajustar → se devuelve la curva con no-show asumido (8%, el mismo default del simulador) marcada
  con `FitFromData=false` para que la UI lo muestre como supuesto, no como dato.
- **R² como bondad de ajuste honesta**: el frontend lo muestra ("ajuste sobre los datos del
  simulador · R² …"), para que la predicción se lea como lo que es — un ajuste, con su error — y
  no como una cifra oficial.
- **`eventStartsAtUtc` llega por query param desde venue-ops** (que lo trae del catálogo de
  Ticketing): `AccessControl` no consulta la DB de otro servicio — el mismo patrón que el
  `SimulationStartRequest` del ADR 0013. 404 si el evento aún no tiene snapshots.
- **Gráfica en `venue-ops` con SVG a mano** (nuevo `PredictionChart`, sin librería de charts) —
  coherencia con el Bloque 5 ("sin librerías UI nuevas"). Curva proyectada vs puntos observados,
  línea del total previsto, línea de vendidas y marca del instante actual.
- Piezas: `ArrivalForecast` (modelo puro), `OccupancyPredictionService` + DTOs
  (`Application/Prediction/`), `PredictionController` (`Api`), `GetForEventAsync` en
  `IOccupancySnapshotRepository` (Infrastructure). Sin CQRS/MediatR (ADR 0003).

## Consecuencias / riesgo aceptado

- La predicción es tan honesta como sus datos: entrena sobre una serie que el propio simulador
  genera con la misma forma que usa el modelo. Es autovalidado (R² se refiere a lo bien que encaja
  la curva observada), no una verdad externa — la limitación queda a la vista en la UI, que es
  exactamente lo que pide el bloque ("no ML mágico").
- **Ajuste frágil en los primeros minutos de llegadas**: con pocos puntos, el modelo no puede
  distinguir aún si la cola final será baja o alta; la asíntota se recorta al `ConfirmedSold`
  (predicción optimista) hasta que los datos la corrigen. Es el comportamiento estadísticamente
  correcto y está acotado por los invariantes, no un bug.
- La predicción solo existe tras ejecutar la simulación (los snapshots se generan con el demo) —
  para un evento sin simular el endpoint responde 404 y la UI invita a ejecutarla.
- `occupancy_snapshots` sigue sin TTL/purga fuera del `reset` de la simulación — riesgo ya aceptado
  en el ADR 0013.
- Cubierto por tests unitarios del modelo puro (recuperación de la asíntota, clamping, no-shows
  asumido) y del servicio (agregación por zonas, proyección de la curva, 404 sin datos) — sin
  Postgres ni RabbitMQ reales.
