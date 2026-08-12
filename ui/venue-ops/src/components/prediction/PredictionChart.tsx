import type { OccupancyPrediction } from '../../shared/types'
import {
  CHART_HEIGHT,
  CHART_MARGIN,
  CHART_WIDTH,
  PLOT_HEIGHT,
  buildXTicks,
  ceilToTens,
  polylinePoints,
  progressWithinWindow,
  toX,
  toY,
} from './chartGeometry'

interface Props {
  prediction: OccupancyPrediction
}

export default function PredictionChart({ prediction }: Props) {
  const { historical, projected, confirmedSold, predictedTotalArrivals, nowUtc, windowStartUtc, eventStartsAtUtc } =
    prediction

  const observedPeak = historical.length > 0 ? Math.max(...historical.map((point) => point.arrivals)) : 0
  const projectedPeak = projected.length > 0 ? Math.max(...projected.map((point) => point.arrivals)) : 0
  const maxValue = Math.max(confirmedSold, predictedTotalArrivals, observedPeak, projectedPeak, 1)
  const maxY = ceilToTens(maxValue)

  const nowProgress = progressWithinWindow(nowUtc, windowStartUtc, eventStartsAtUtc)
  const xTicks = buildXTicks()

  return (
    <svg
      className="prediction-chart"
      viewBox={`0 0 ${CHART_WIDTH} ${CHART_HEIGHT}`}
      role="img"
      aria-label="Curva de llegada prevista frente a la observada"
    >
      <text x={2} y={CHART_MARGIN.top + 4} className="prediction-chart__axis-label">
        {maxY}
      </text>
      <text x={2} y={CHART_MARGIN.top + PLOT_HEIGHT + 4} className="prediction-chart__axis-label">
        0
      </text>

      <line
        x1={CHART_MARGIN.left}
        y1={toY(confirmedSold, maxY)}
        x2={CHART_WIDTH - CHART_MARGIN.right}
        y2={toY(confirmedSold, maxY)}
        className="prediction-chart__sold"
      />
      <text
        x={CHART_WIDTH - CHART_MARGIN.right - 4}
        y={toY(confirmedSold, maxY) - 5}
        textAnchor="end"
        className="prediction-chart__sold-label"
      >
        {confirmedSold} vendidas
      </text>

      <line
        x1={CHART_MARGIN.left}
        y1={toY(predictedTotalArrivals, maxY)}
        x2={CHART_WIDTH - CHART_MARGIN.right}
        y2={toY(predictedTotalArrivals, maxY)}
        className="prediction-chart__target"
      />
      <text
        x={CHART_WIDTH - CHART_MARGIN.right - 4}
        y={toY(predictedTotalArrivals, maxY) + 14}
        textAnchor="end"
        className="prediction-chart__target-label"
      >
        ≈{predictedTotalArrivals} previstos
      </text>

      <line
        x1={toX(nowProgress)}
        y1={CHART_MARGIN.top}
        x2={toX(nowProgress)}
        y2={CHART_MARGIN.top + PLOT_HEIGHT}
        className="prediction-chart__now"
      />
      <text x={toX(nowProgress) - 4} y={CHART_MARGIN.top + 12} textAnchor="end" className="prediction-chart__now-label">
        ahora
      </text>

      <polyline points={polylinePoints(projected, maxY)} className="prediction-chart__projected" />

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
            y1={CHART_MARGIN.top}
            x2={toX(tick.progress)}
            y2={CHART_MARGIN.top + PLOT_HEIGHT}
            className="prediction-chart__grid"
          />
          <text x={toX(tick.progress)} y={CHART_HEIGHT - 8} textAnchor="middle" className="prediction-chart__axis-label">
            {tick.label}
          </text>
        </g>
      ))}
    </svg>
  )
}
