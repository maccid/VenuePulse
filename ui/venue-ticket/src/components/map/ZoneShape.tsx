import type { KeyboardEvent } from 'react'

export interface RectGeometry {
  x: number
  y: number
  width: number
  height: number
}

export interface StandShape {
  path?: string
  rect?: RectGeometry
  transform?: string
  transformOrigin?: string
  labelX: number
  labelY: number
  labelLines: string[]
  lightX: number
  lightY: number
  tooltipY?: number
}

export interface ZonePricing {
  price: number
  effectivePrice: number
  priceTier: string
}

interface Props {
  name: string
  shape: StandShape
  tier: 'low' | 'mid' | 'high'
  isSelected: boolean
  interactive: boolean
  pricing?: ZonePricing
  onSelect?: () => void
}

export default function ZoneShape({ name, shape, tier, isSelected, interactive, pricing, onSelect }: Props) {
  const classes = [
    'stadium-map__zone',
    'stadium-map__zone--stand',
    isSelected ? 'stadium-map__zone--selected' : '',
    interactive ? 'stadium-map__zone--interactive' : '',
  ]
    .filter(Boolean)
    .join(' ')

  const handleKeyDown = interactive
    ? (e: KeyboardEvent<SVGGElement>) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault()
          onSelect?.()
        }
      }
    : undefined

  const priceLabel = pricing ? `${pricing.effectivePrice.toFixed(2)} €` : null
  const tooltipWidth = priceLabel ? priceLabel.length * 5.8 + 14 : 0
  const tooltipY = shape.tooltipY ?? shape.lightY - 24
  const outline = shape.rect ? (
    <rect x={shape.rect.x} y={shape.rect.y} width={shape.rect.width} height={shape.rect.height} />
  ) : (
    <path d={shape.path} />
  )

  return (
    <g
      className={classes}
      onClick={interactive ? onSelect : undefined}
      role={interactive ? 'button' : undefined}
      aria-pressed={interactive ? isSelected : undefined}
      aria-label={name}
      tabIndex={interactive ? 0 : undefined}
      onKeyDown={handleKeyDown}
    >
      {shape.transform ? (
        <g transform={shape.transform} style={shape.transformOrigin ? { transformOrigin: shape.transformOrigin } : undefined}>
          {outline}
        </g>
      ) : (
        outline
      )}
      <text x={shape.labelX} y={shape.labelY} textAnchor="middle" dominantBaseline="middle" pointerEvents="none">
        {shape.labelLines.map((line, index) => (
          <tspan key={line} x={shape.labelX} dy={index === 0 ? 0 : 12}>
            {line}
          </tspan>
        ))}
      </text>
      {pricing && (
        <g className="stadium-map__zone-tooltip" pointerEvents="none">
          <rect x={shape.labelX - tooltipWidth / 2} y={tooltipY} width={tooltipWidth} height={17} rx={8.5} />
          <text x={shape.labelX} y={tooltipY + 8.5} textAnchor="middle" dominantBaseline="middle">
            {priceLabel}
          </text>
        </g>
      )}
      <g className="stadium-map__zone-light" transform={`translate(${shape.lightX}, ${shape.lightY})`}>
        <circle r={7} className={`stadium-map__zone-light-halo stadium-map__zone-light--${tier}`} />
        <circle r={3} className={`stadium-map__zone-light-core stadium-map__zone-light--${tier}`} />
      </g>
    </g>
  )
}
