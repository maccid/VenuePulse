import type { KeyboardEvent } from 'react'

export interface ZoneLayout {
  x: number
  y: number
  width: number
  height: number
}

// Forma de una grada de partido: contorno real (curva hacia el exterior ovalado, ver
// StadiumMap.tsx) en vez de un rectángulo — Este/Oeste reutilizan la curva de Norte/Sur girada
// con `transform` porque el óvalo exterior no es cuadrado (324×264) y una rotación simple no
// encajaría con el campo.
export interface StandShape {
  path: string
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
  layout?: ZoneLayout
  shape?: StandShape
  tier: 'low' | 'mid' | 'high'
  // 'stand' = grada de partido: contorno curvo, azul fijo sin borde (el borde solo aparece al
  // interactuar) y semáforo de ocupación junto al nombre. 'tiered' = concierto: rectángulo con el
  // semáforo verde/naranja/rojo tiñendo todo el bloque, sin cambios.
  variant?: 'tiered' | 'stand'
  isSelected: boolean
  interactive: boolean
  pricing?: ZonePricing
  onSelect?: () => void
}

export default function ZoneShape({
  name,
  layout,
  shape,
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

  const handleKeyDown = interactive
    ? (e: KeyboardEvent<SVGGElement>) => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault()
          onSelect?.()
        }
      }
    : undefined

  if (variant === 'stand' && shape) {
    // El precio va debajo del nombre con aire extra para no pegarse al texto (Este/Oeste escriben
    // el nombre en dos líneas y con el precio colgando justo debajo quedaba encajado).
    const priceY = shape.labelY + shape.labelLines.length * 12 + 8

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
            <path d={shape.path} />
          </g>
        ) : (
          <path d={shape.path} />
        )}
        <text
          x={shape.labelX}
          y={shape.labelY}
          textAnchor="middle"
          dominantBaseline="middle"
          pointerEvents="none"
        >
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

  if (!layout) return null

  // Gradas estrechas (Este/Oeste, concierto) no tienen ancho para el nombre en una sola línea —
  // se reparte en varias líneas apiladas (una palabra por línea), sin rotar el texto.
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
      onKeyDown={handleKeyDown}
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
