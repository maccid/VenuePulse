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
