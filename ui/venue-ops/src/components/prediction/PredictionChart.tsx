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
      className="block h-auto w-full"
      viewBox={`0 0 ${CHART_WIDTH} ${CHART_HEIGHT}`}
      role="img"
      aria-label="Curva de llegada prevista frente a la observada"
    >
      <text x={2} y={CHART_MARGIN.top + 4} className="fill-text-muted text-[11px]">
        {maxY}
      </text>
      <text x={2} y={CHART_MARGIN.top + PLOT_HEIGHT + 4} className="fill-text-muted text-[11px]">
        0
      </text>

      <line
        x1={CHART_MARGIN.left}
        y1={toY(confirmedSold, maxY)}
        x2={CHART_WIDTH - CHART_MARGIN.right}
        y2={toY(confirmedSold, maxY)}
        className="stroke-text-muted [stroke-dasharray:4_4]"
      />
      <text
        x={CHART_WIDTH - CHART_MARGIN.right - 4}
        y={toY(confirmedSold, maxY) - 5}
        textAnchor="end"
        className="fill-text-muted text-[11px]"
      >
        {confirmedSold} vendidas
      </text>

      <line
        x1={CHART_MARGIN.left}
        y1={toY(predictedTotalArrivals, maxY)}
        x2={CHART_WIDTH - CHART_MARGIN.right}
        y2={toY(predictedTotalArrivals, maxY)}
        className="stroke-success [stroke-width:1.5] [stroke-dasharray:6_4]"
      />
      <text
        x={CHART_WIDTH - CHART_MARGIN.right - 4}
        y={toY(predictedTotalArrivals, maxY) + 14}
        textAnchor="end"
        className="fill-success text-[11px] font-semibold"
      >
        ≈{predictedTotalArrivals} previstos
      </text>

      <line
        x1={toX(nowProgress)}
        y1={CHART_MARGIN.top}
        x2={toX(nowProgress)}
        y2={CHART_MARGIN.top + PLOT_HEIGHT}
        className="stroke-danger [stroke-width:1.5]"
      />
      <text x={toX(nowProgress) - 4} y={CHART_MARGIN.top + 12} textAnchor="end" className="fill-danger text-[11px]">
        ahora
      </text>

      <polyline points={polylinePoints(projected, maxY)} className="fill-none stroke-accent [stroke-width:2.5]" />

      {historical.map((point) => (
        <circle
          key={point.timeUtc}
          cx={toX(point.progress)}
          cy={toY(point.arrivals, maxY)}
          r={3.5}
          className="fill-text"
        />
      ))}

      {xTicks.map((tick) => (
        <g key={tick.label}>
          <line
            x1={toX(tick.progress)}
            y1={CHART_MARGIN.top}
            x2={toX(tick.progress)}
            y2={CHART_MARGIN.top + PLOT_HEIGHT}
            className="stroke-border"
          />
          <text x={toX(tick.progress)} y={CHART_HEIGHT - 8} textAnchor="middle" className="fill-text-muted text-[11px]">
            {tick.label}
          </text>
        </g>
      ))}
    </svg>
  )
}
