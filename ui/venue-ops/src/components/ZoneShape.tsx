export interface RectGeometry {
  x: number
  y: number
  width: number
  height: number
}

// Forma de una zona: contorno curvo (`path`, hacia el óvalo exterior — copia adaptada de
// `ticket-checkout`, mismas coordenadas de curva, sin código compartido entre micro frontends,
// ver ADR 0004) o rectángulo recto (`rect`, sin esquinas redondeadas — Pista/VIP de concierto,
// ver ADR 0026). A diferencia de `ticket-checkout` no hay semáforo aparte ni azul fijo: el bloque
// entero se tiñe por tier (ver className más abajo), que es lo que necesita operaciones para
// escanear ocupación de un vistazo.
export interface StandShape {
  path?: string
  rect?: RectGeometry
  transform?: string
  transformOrigin?: string
  labelX: number
  labelY: number
  labelLines: string[]
}

interface Props {
  shape: StandShape
  tier: 'low' | 'mid' | 'high'
}

export default function ZoneShape({ shape, tier }: Props) {
  const className = `stadium-map__zone stadium-map__zone--${tier}`
  const outline = shape.rect ? (
    <rect x={shape.rect.x} y={shape.rect.y} width={shape.rect.width} height={shape.rect.height} />
  ) : (
    <path d={shape.path} />
  )

  return (
    <g className={className}>
      {shape.transform ? (
        <g transform={shape.transform} style={shape.transformOrigin ? { transformOrigin: shape.transformOrigin } : undefined}>
          {outline}
        </g>
      ) : (
        outline
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
