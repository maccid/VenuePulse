export interface ZoneLayout {
  x: number
  y: number
  width: number
  height: number
}

export interface ZonePricing {
  price: number
  effectivePrice: number
  priceTier: string
}

interface Props {
  name: string
  layout: ZoneLayout
  tier: 'low' | 'mid' | 'high'
  isSelected: boolean
  interactive: boolean
  pricing?: ZonePricing
  onSelect?: () => void
}

export default function ZoneShape({ name, layout, tier, isSelected, interactive, pricing, onSelect }: Props) {
  const classes = [
    'stadium-map__zone',
    `stadium-map__zone--${tier}`,
    isSelected ? 'stadium-map__zone--selected' : '',
    interactive ? 'stadium-map__zone--interactive' : '',
  ]
    .filter(Boolean)
    .join(' ')

  return (
    <g
      className={classes}
      onClick={interactive ? onSelect : undefined}
      role={interactive ? 'button' : undefined}
      aria-pressed={interactive ? isSelected : undefined}
      tabIndex={interactive ? 0 : undefined}
      onKeyDown={
        interactive
          ? (e) => {
              if (e.key === 'Enter' || e.key === ' ') {
                e.preventDefault()
                onSelect?.()
              }
            }
          : undefined
      }
    >
      <rect x={layout.x} y={layout.y} width={layout.width} height={layout.height} rx={4} />
      <text
        x={layout.x + layout.width / 2}
        y={layout.y + layout.height / 2 - (pricing ? 7 : 0)}
        textAnchor="middle"
        dominantBaseline="middle"
      >
        {name}
      </text>
      {pricing && (
        <text
          x={layout.x + layout.width / 2}
          y={layout.y + layout.height / 2 + 10}
          textAnchor="middle"
          dominantBaseline="middle"
          className="stadium-map__zone-price"
        >
          {pricing.effectivePrice.toFixed(2)} €
        </text>
      )}
    </g>
  )
}
