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
  // 'stand' = grada de partido: azul fijo (la ocupación la indica una luz aparte, ver
  // StadiumMap.tsx), en vez del semáforo verde/naranja/rojo de 'tiered' (concierto).
  variant?: 'tiered' | 'stand'
  isSelected: boolean
  interactive: boolean
  pricing?: ZonePricing
  onSelect?: () => void
}

export default function ZoneShape({
  name,
  layout,
  tier,
  variant = 'tiered',
  isSelected,
  interactive,
  pricing,
  onSelect,
}: Props) {
  const classes = [
    'stadium-map__zone',
    variant === 'stand' ? 'stadium-map__zone--stand' : `stadium-map__zone--${tier}`,
    isSelected ? 'stadium-map__zone--selected' : '',
    interactive ? 'stadium-map__zone--interactive' : '',
  ]
    .filter(Boolean)
    .join(' ')

  // Gradas estrechas (Este/Oeste, ver StadiumMap MATCH_LAYOUT) no tienen ancho para el nombre en
  // una sola línea — se reparte en varias líneas apiladas (una palabra por línea), sin rotar el
  // texto, en vez de encajarlo girado 90°.
  const centerX = layout.x + layout.width / 2
  const centerY = layout.y + layout.height / 2 - (pricing ? 7 : 0)
  const nameLines = layout.width < 60 ? name.split(' ') : [name]
  const lineHeight = 12
  const firstLineY = centerY - ((nameLines.length - 1) * lineHeight) / 2

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
      <text x={centerX} y={firstLineY} textAnchor="middle" dominantBaseline="middle">
        {nameLines.map((line, index) => (
          <tspan key={line} x={centerX} dy={index === 0 ? 0 : lineHeight}>
            {line}
          </tspan>
        ))}
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
