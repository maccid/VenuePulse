import type { KeyboardEvent } from 'react'

export interface RectGeometry {
  x: number
  y: number
  width: number
  height: number
}

// Forma de una zona: o bien un contorno curvo (`path`, hacia el óvalo exterior — Este/Oeste de
// partido y Grada 3 de concierto reutilizan la curva de Norte/Sur girada con `transform`, porque
// el óvalo no es cuadrado y una rotación simple no encajaría), o bien un rectángulo recto (`rect`,
// sin esquinas redondeadas — Pista/VIP de concierto). Ambas llevan el mismo azul fijo sin borde y
// el mismo semáforo de ocupación junto al nombre (ver StadiumMap.tsx).
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

  // El precio va debajo del nombre con aire extra para no pegarse al texto (Este/Oeste escriben
  // el nombre en dos líneas y con el precio colgando justo debajo quedaba encajado).
  const priceY = shape.labelY + shape.labelLines.length * 12 + 8
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
        <text
          x={shape.labelX}
          y={priceY}
          textAnchor="middle"
          dominantBaseline="middle"
          className="stadium-map__zone-price"
          pointerEvents="none"
        >
          {pricing.effectivePrice.toFixed(2)} €
        </text>
      )}
      <g className="stadium-map__zone-light" transform={`translate(${shape.lightX}, ${shape.lightY})`}>
        <circle r={7} className={`stadium-map__zone-light-halo stadium-map__zone-light--${tier}`} />
        <circle r={3} className={`stadium-map__zone-light-core stadium-map__zone-light--${tier}`} />
      </g>
    </g>
  )
}
