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

const TIER_CLASSES: Record<Props['tier'], string> = {
  low: 'fill-success/35 stroke-success',
  mid: 'fill-accent/35 stroke-accent',
  high: 'fill-danger/35 stroke-danger',
}

export default function ZoneShape({ shape, tier }: Props) {
  const outlineClassName = `${TIER_CLASSES[tier]} stroke-1`
  const outline = shape.rect ? (
    <rect x={shape.rect.x} y={shape.rect.y} width={shape.rect.width} height={shape.rect.height} className={outlineClassName} />
  ) : (
    <path d={shape.path} className={outlineClassName} />
  )

  return (
    <g>
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
        className="pointer-events-none text-[11px] font-semibold fill-text"
      >
        {shape.labelLines.map((line, index) => (
          <tspan key={line} x={shape.labelX} dy={index === 0 ? 0 : 12}>
            {line}
          </tspan>
        ))}
      </text>
    </g>
  )
}
