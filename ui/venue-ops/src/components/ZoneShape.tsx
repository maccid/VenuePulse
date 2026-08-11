export interface ZoneLayout {
  x: number
  y: number
  width: number
  height: number
}

// Forma de una grada de partido: contorno real (curva hacia el óvalo exterior, ver
// StadiumMap.tsx) en vez de un rectángulo — copia adaptada de la de `ticket-checkout` (mismas
// coordenadas de curva, sin código compartido entre micro frontends, ver ADR 0004). A diferencia
// de `ticket-checkout`, aquí la ocupación sigue tiñendo todo el bloque (sin semáforo aparte): en
// operaciones se necesita ver el color de un vistazo, no un azul fijo con luz de detalle.
export interface StandShape {
  path: string
  transform?: string
  transformOrigin?: string
  labelX: number
  labelY: number
  labelLines: string[]
}

interface Props {
  name: string
  layout?: ZoneLayout
  shape?: StandShape
  tier: 'low' | 'mid' | 'high'
  variant?: 'tiered' | 'stand'
}

export default function ZoneShape({ name, layout, shape, tier, variant = 'tiered' }: Props) {
  const className = `stadium-map__zone stadium-map__zone--${tier}`

  if (variant === 'stand' && shape) {
    return (
      <g className={className}>
        {shape.transform ? (
          <g
            transform={shape.transform}
            style={shape.transformOrigin ? { transformOrigin: shape.transformOrigin } : undefined}
          >
            <path d={shape.path} />
          </g>
        ) : (
          <path d={shape.path} />
        )}
        <text x={shape.labelX} y={shape.labelY} textAnchor="middle" dominantBaseline="middle">
          {shape.labelLines.map((line, index) => (
            <tspan key={line} x={shape.labelX} dy={index === 0 ? 0 : 12}>
              {line}
            </tspan>
          ))}
        </text>
      </g>
    )
  }

  if (!layout) return null

  return (
    <g className={className}>
      <rect x={layout.x} y={layout.y} width={layout.width} height={layout.height} rx={4} />
      <text
        x={layout.x + layout.width / 2}
        y={layout.y + layout.height / 2}
        textAnchor="middle"
        dominantBaseline="middle"
      >
        {name}
      </text>
    </g>
  )
}
