import type { OccupancyPrediction, PredictionPoint } from '../types'

interface Props {
  prediction: OccupancyPrediction
}

const W = 640
const H = 240
const MARGIN = { top: 16, right: 12, bottom: 28, left: 40 }
const PLOT_W = W - MARGIN.left - MARGIN.right
const PLOT_H = H - MARGIN.top - MARGIN.bottom

function toX(progress: number) {
  return MARGIN.left + progress * PLOT_W
}

function toY(value: number, maxY: number) {
  return MARGIN.top + PLOT_H * (1 - value / maxY)
}

function polyline(points: PredictionPoint[], maxY: number) {
  return points
    .map((point) => `${toX(point.progress).toFixed(1)},${toY(point.arrivals, maxY).toFixed(1)}`)
    .join(' ')
}

function parseUtc(iso: string) {
  return new Date(iso).getTime()
}

/**
 * Gráfica SVG de la curva de llegada (Bloque 11) sin librerías — coherencia con Bloque 5: el
 * historial observado se superpone a la curva proyectada, con el total previsto, lo vendido y el
 * instante actual como referencias.
 */
export default function PredictionChart({ prediction }: Props) {
  const { historical, projected, confirmedSold, predictedTotalArrivals, nowUtc, windowStartUtc, eventStartsAtUtc } =
    prediction

  const observedPeak = historical.length > 0 ? Math.max(...historical.map((point) => point.arrivals)) : 0
  const projectedPeak = projected.length > 0 ? Math.max(...projected.map((point) => point.arrivals)) : 0
  const maxValue = Math.max(confirmedSold, predictedTotalArrivals, observedPeak, projectedPeak, 1)
  const maxY = Math.ceil(maxValue / 10) * 10

  const windowMillis = parseUtc(eventStartsAtUtc) - parseUtc(windowStartUtc)
  const nowProgress = windowMillis > 0 ? Math.min(1, Math.max(0, (parseUtc(nowUtc) - parseUtc(windowStartUtc)) / windowMillis)) : 0

  const xTicks = [
    { label: '-3 h', progress: 0 },
    { label: '-2 h', progress: 1 / 3 },
    { label: '-1 h', progress: 2 / 3 },
    { label: 'Evento', progress: 1 },
  ]

  return (
    <svg
      className="prediction-chart"
      viewBox={`0 0 ${W} ${H}`}
      role="img"
      aria-label="Curva de llegada prevista frente a la observada"
    >
      <text x={2} y={MARGIN.top + 4} className="prediction-chart__axis-label">
        {maxY}
      </text>
      <text x={2} y={MARGIN.top + PLOT_H + 4} className="prediction-chart__axis-label">
        0
      </text>

      <line
        x1={MARGIN.left}
        y1={toY(confirmedSold, maxY)}
        x2={W - MARGIN.right}
        y2={toY(confirmedSold, maxY)}
        className="prediction-chart__sold"
      />
      <text
        x={W - MARGIN.right - 4}
        y={toY(confirmedSold, maxY) - 5}
        textAnchor="end"
        className="prediction-chart__sold-label"
      >
        {confirmedSold} vendidas
      </text>

      <line
        x1={MARGIN.left}
        y1={toY(predictedTotalArrivals, maxY)}
        x2={W - MARGIN.right}
        y2={toY(predictedTotalArrivals, maxY)}
        className="prediction-chart__target"
      />
      <text
        x={W - MARGIN.right - 4}
        y={toY(predictedTotalArrivals, maxY) + 14}
        textAnchor="end"
        className="prediction-chart__target-label"
      >
        ≈{predictedTotalArrivals} previstos
      </text>

      <line
        x1={toX(nowProgress)}
        y1={MARGIN.top}
        x2={toX(nowProgress)}
        y2={MARGIN.top + PLOT_H}
        className="prediction-chart__now"
      />
      <text x={toX(nowProgress) - 4} y={MARGIN.top + 12} textAnchor="end" className="prediction-chart__now-label">
        ahora
      </text>

      <polyline points={polyline(projected, maxY)} className="prediction-chart__projected" />

      {historical.map((point) => (
        <circle
          key={point.timeUtc}
          cx={toX(point.progress)}
          cy={toY(point.arrivals, maxY)}
          r={3.5}
          className="prediction-chart__observed"
        />
      ))}

      {xTicks.map((tick) => (
        <g key={tick.label}>
          <line
            x1={toX(tick.progress)}
            y1={MARGIN.top}
            x2={toX(tick.progress)}
            y2={MARGIN.top + PLOT_H}
            className="prediction-chart__grid"
          />
          <text x={toX(tick.progress)} y={H - 8} textAnchor="middle" className="prediction-chart__axis-label">
            {tick.label}
          </text>
        </g>
      ))}
    </svg>
  )
}
