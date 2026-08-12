import type { VenueOpsMode, ZoneOccupancy } from '../../shared/types'
import ZoneShape from './ZoneShape'
import { MATCH_STANDS, CONCERT_STANDS } from './standShapes'
import { MatchField, ConcertStage } from './StadiumBackground'

interface Props {
  zones: ZoneOccupancy[]
  mode?: VenueOpsMode
}

function occupancyRatio(zone: ZoneOccupancy, mode: VenueOpsMode): number {
  if (zone.capacity === 0) return 0
  if (mode === 'checkin') return Math.min(1, Math.max(0, zone.checkedIn / zone.capacity))
  return Math.min(1, Math.max(0, (zone.capacity - zone.availableToSell) / zone.capacity))
}

function occupancyTier(ratio: number): 'low' | 'mid' | 'high' {
  if (ratio >= 0.9) return 'high'
  if (ratio >= 0.7) return 'mid'
  return 'low'
}

const LEGEND_LABEL: Record<VenueOpsMode, { low: string; mid: string; high: string }> = {
  sales: { low: '< 70% vendido', mid: '70–90% vendido', high: '> 90% vendido' },
  checkin: { low: '< 70% dentro', mid: '70–90% dentro', high: '> 90% dentro' },
}

export default function StadiumMap({ zones, mode = 'sales' }: Props) {
  const isConcert = zones.some((z) => z.name === 'Pista')
  const stands = isConcert ? CONCERT_STANDS : MATCH_STANDS
  const laidOut = zones.filter((z) => stands[z.name])
  const unmapped = zones.filter((z) => !stands[z.name])

  return (
    <div className="stadium-map">
      <svg viewBox="0 0 340 280" role="img" aria-label="Mapa del evento" className="stadium-map__svg">
        <rect x="8" y="8" width="324" height="264" rx="85" className="stadium-map__exterior" />

        {isConcert ? <ConcertStage /> : <MatchField />}

        {laidOut.map((zone) => (
          <ZoneShape key={zone.zoneId} shape={stands[zone.name]} tier={occupancyTier(occupancyRatio(zone, mode))} />
        ))}
      </svg>

      {unmapped.length > 0 && (
        <ul className="stadium-map__fallback">
          {unmapped.map((zone) => (
            <li key={zone.zoneId}>{zone.name}</li>
          ))}
        </ul>
      )}

      <ul className="stadium-map__legend">
        <li className="stadium-map__legend-item stadium-map__legend-item--low">{LEGEND_LABEL[mode].low}</li>
        <li className="stadium-map__legend-item stadium-map__legend-item--mid">{LEGEND_LABEL[mode].mid}</li>
        <li className="stadium-map__legend-item stadium-map__legend-item--high">{LEGEND_LABEL[mode].high}</li>
      </ul>
    </div>
  )
}
