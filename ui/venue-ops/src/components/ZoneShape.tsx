export interface ZoneLayout {
  x: number
  y: number
  width: number
  height: number
}

interface Props {
  name: string
  layout: ZoneLayout
  tier: 'low' | 'mid' | 'high'
}

export default function ZoneShape({ name, layout, tier }: Props) {
  return (
    <g className={`stadium-map__zone stadium-map__zone--${tier}`}>
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
