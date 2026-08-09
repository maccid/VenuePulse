import type { ZoneOccupancy } from '../types'

interface ZoneLayout {
  x: number
  y: number
  width: number
  height: number
}

// Las 3 zonas son fijas para cualquier evento (ver ZoneService.FixedZones en AccessControl), así
// que un mapeo estático por nombre cubre el 100% del caso de uso actual sin añadir geometría al
// backend — ver ADR 0017.
const ZONE_LAYOUT: Record<string, ZoneLayout> = {
  'Puerta Norte': { x: 40, y: 16, width: 220, height: 44 },
  'Grada VIP': { x: 272, y: 76, width: 48, height: 128 },
  'Puerta Sur': { x: 40, y: 220, width: 220, height: 44 },
}

interface Props {
  zones: ZoneOccupancy[]
  selectedZoneId?: string
  onSelect?: (zoneId: string) => void
}

function occupancyRatio(zone: ZoneOccupancy): number {
  if (zone.capacity === 0) return 0
  return Math.min(1, Math.max(0, (zone.capacity - zone.availableToSell) / zone.capacity))
}

function occupancyTier(ratio: number): 'low' | 'mid' | 'high' {
  if (ratio >= 0.9) return 'high'
  if (ratio >= 0.7) return 'mid'
  return 'low'
}

export default function StadiumMap({ zones, selectedZoneId, onSelect }: Props) {
  const interactive = Boolean(onSelect)
  const laidOut = zones.filter((z) => ZONE_LAYOUT[z.name])
  const unmapped = zones.filter((z) => !ZONE_LAYOUT[z.name])

  return (
    <div className="stadium-map">
      <svg viewBox="0 0 340 280" role="img" aria-label="Plano del recinto" className="stadium-map__svg">
        <rect x="20" y="20" width="300" height="240" rx="6" className="stadium-map__pitch" />
        <line x1="20" y1="140" x2="320" y2="140" className="stadium-map__midline" />
        <circle cx="170" cy="140" r="28" className="stadium-map__midcircle" />

        {laidOut.map((zone) => {
          const layout = ZONE_LAYOUT[zone.name]
          const tier = occupancyTier(occupancyRatio(zone))
          const isSelected = zone.zoneId === selectedZoneId
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
              key={zone.zoneId}
              className={classes}
              onClick={interactive ? () => onSelect?.(zone.zoneId) : undefined}
              role={interactive ? 'button' : undefined}
              aria-pressed={interactive ? isSelected : undefined}
              tabIndex={interactive ? 0 : undefined}
              onKeyDown={
                interactive
                  ? (e) => {
                      if (e.key === 'Enter' || e.key === ' ') {
                        e.preventDefault()
                        onSelect?.(zone.zoneId)
                      }
                    }
                  : undefined
              }
            >
              <rect x={layout.x} y={layout.y} width={layout.width} height={layout.height} rx={4} />
              <text
                x={layout.x + layout.width / 2}
                y={layout.y + layout.height / 2}
                textAnchor="middle"
                dominantBaseline="middle"
              >
                {zone.name}
              </text>
            </g>
          )
        })}
      </svg>

      {unmapped.length > 0 && (
        <ul className="stadium-map__fallback">
          {unmapped.map((zone) => (
            <li key={zone.zoneId}>
              <button type="button" disabled={!interactive} onClick={() => onSelect?.(zone.zoneId)}>
                {zone.name} ({zone.availableToSell} libres)
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  )
}
