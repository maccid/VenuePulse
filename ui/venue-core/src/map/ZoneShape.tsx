import type { KeyboardEvent } from 'react'
import type { OccupancyTier } from '../domain'
import type { StandShape } from './standShapes'

export interface ZonePricing {
  price: number
  effectivePrice: number
  priceTier: string
}

interface Props {
  name: string
  shape: StandShape
  tier: OccupancyTier
  interactive: boolean
  isSelected: boolean
  pricing?: ZonePricing
  onSelect?: () => void
}

const TIER_FILL: Record<OccupancyTier, string> = {
  low: 'fill-green-500/35 stroke-green-500',
  mid: 'fill-amber-500/35 stroke-amber-500',
  high: 'fill-red-500/35 stroke-red-500',
}

const TIER_LIGHT: Record<OccupancyTier, string> = {
  low: 'fill-green-500',
  mid: 'fill-amber-500',
  high: 'fill-red-500',
}

export default function ZoneShape({ name, shape, tier, interactive, isSelected, pricing, onSelect }: Props) {
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

  const outlineClassName = interactive
    ? [
        'fill-blue-600/85',
        'group-hover:stroke-blue-800 group-hover:[stroke-width:1.5]',
        'group-focus-visible:stroke-blue-800 group-focus-visible:[stroke-width:1.5]',
        isSelected ? 'stroke-slate-900 [stroke-width:2.5]' : 'stroke-none',
      ].join(' ')
    : `${TIER_FILL[tier]} stroke-1`

  const outline = shape.rect ? (
    <rect x={shape.rect.x} y={shape.rect.y} width={shape.rect.width} height={shape.rect.height} className={outlineClassName} />
  ) : (
    <path d={shape.path} className={outlineClassName} />
  )

  return (
    <g
      className={['group', interactive ? 'cursor-pointer focus:outline-none' : ''].filter(Boolean).join(' ')}
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
      <text
        x={shape.labelX}
        y={shape.labelY}
        textAnchor="middle"
        dominantBaseline="middle"
        className={`pointer-events-none text-[11px] font-semibold ${interactive ? 'fill-white' : 'fill-slate-700'}`}
      >
        {shape.labelLines.map((line, index) => (
          <tspan key={line} x={shape.labelX} dy={index === 0 ? 0 : 12}>
            {line}
          </tspan>
        ))}
      </text>
      {interactive && pricing && (
        <g className="vp-map-zone-tooltip pointer-events-none opacity-0 transition-opacity duration-150 group-hover:opacity-100">
          <rect x={shape.labelX - tooltipWidth / 2} y={tooltipY} width={tooltipWidth} height={17} rx={8.5} className="fill-slate-900 stroke-none" />
          <text x={shape.labelX} y={tooltipY + 8.5} textAnchor="middle" dominantBaseline="middle" className="fill-white text-[10px] font-bold">
            {priceLabel}
          </text>
        </g>
      )}
      {interactive && (
        <g className="vp-map-zone-light pointer-events-none" transform={`translate(${shape.lightX}, ${shape.lightY})`}>
          <circle r={7} className={`vp-map-light-halo ${TIER_LIGHT[tier]}`} />
          <circle r={3} className={`vp-map-light-core ${TIER_LIGHT[tier]}`} />
        </g>
      )}
    </g>
  )
}
