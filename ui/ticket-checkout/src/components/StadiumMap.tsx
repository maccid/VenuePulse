import type { ZoneOccupancy } from '../types'
import ZoneShape, { type ZonePricing } from './ZoneShape'
import { MATCH_STANDS, CONCERT_STANDS } from './standShapes'
import { MatchField, ConcertStage } from './StadiumBackground'

interface Props {
  zones: ZoneOccupancy[]
  selectedZoneId?: string
  onSelect?: (zoneId: string) => void
  pricingByZoneId?: Record<string, ZonePricing>
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

export default function StadiumMap({ zones, selectedZoneId, onSelect, pricingByZoneId }: Props) {
  const interactive = Boolean(onSelect)
  const isConcert = zones.some((z) => z.name === 'Pista')
  const stands = isConcert ? CONCERT_STANDS : MATCH_STANDS
  const laidOut = zones.filter((z) => stands[z.name])
  const unmapped = zones.filter((z) => !stands[z.name])

  return (
    <div className="stadium-map">
      <svg viewBox="0 0 340 280" role="img" aria-label="Plano del recinto" className="stadium-map__svg">
        {/* Estadio: fondo gris de todo el recinto, óvalo (rx alto) compartido por partido y
            concierto (ver ADR 0026), con hueco en las esquinas donde no llegan las gradas. */}
        <rect x="8" y="8" width="324" height="264" rx="85" className="stadium-map__exterior" />

        {isConcert ? <ConcertStage /> : <MatchField />}

        {laidOut.map((zone) => (
          <ZoneShape
            key={zone.zoneId}
            name={zone.name}
            shape={stands[zone.name]}
            tier={occupancyTier(occupancyRatio(zone))}
            isSelected={zone.zoneId === selectedZoneId}
            interactive={interactive}
            pricing={pricingByZoneId?.[zone.zoneId]}
            onSelect={interactive ? () => onSelect?.(zone.zoneId) : undefined}
          />
        ))}
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
