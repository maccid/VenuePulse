import type { OccupancyMetric, ZoneOccupancy } from '../domain'
import { occupancyRatio, occupancyTier } from '../domain'
import ZoneShape, { type ZonePricing } from './ZoneShape'
import { MATCH_STANDS, CONCERT_STANDS } from './standShapes'
import { MatchField, ConcertStage } from './StadiumBackground'

interface Props {
  zones: ZoneOccupancy[]
  mode?: OccupancyMetric
  selectedZoneId?: string
  onSelect?: (zoneId: string) => void
  pricingByZoneId?: Record<string, ZonePricing>
}

const LEGEND_LABEL: Record<OccupancyMetric, { low: string; mid: string; high: string }> = {
  sales: { low: '< 70% vendido', mid: '70–90% vendido', high: '> 90% vendido' },
  checkin: { low: '< 70% dentro', mid: '70–90% dentro', high: '> 90% dentro' },
}

export default function StadiumMap({ zones, mode = 'sales', selectedZoneId, onSelect, pricingByZoneId }: Props) {
  const interactive = Boolean(onSelect)
  const isConcert = zones.some((z) => z.name === 'Pista')
  const stands = isConcert ? CONCERT_STANDS : MATCH_STANDS
  const laidOut = zones.filter((z) => stands[z.name])
  const unmapped = zones.filter((z) => !stands[z.name])

  return (
    <div className={interactive ? 'flex w-full justify-center' : 'mb-6'}>
      <svg
        viewBox="0 0 340 280"
        role="img"
        aria-label="Plano del recinto"
        className={`block h-auto w-full ${interactive ? 'max-w-[460px]' : 'max-w-[360px]'}`}
      >
        <rect x="8" y="8" width="324" height="264" rx="85" className="fill-[#e4e7ec] stroke-[#d1d5db]" />

        {isConcert ? <ConcertStage /> : <MatchField />}

        {laidOut.map((zone) => (
          <ZoneShape
            key={zone.zoneId}
            name={zone.name}
            shape={stands[zone.name]}
            tier={occupancyTier(occupancyRatio(zone, mode))}
            interactive={interactive}
            isSelected={zone.zoneId === selectedZoneId}
            pricing={pricingByZoneId?.[zone.zoneId]}
            onSelect={interactive ? () => onSelect?.(zone.zoneId) : undefined}
          />
        ))}
      </svg>

      {unmapped.length > 0 && (
        <ul className={interactive ? 'mt-2 flex flex-wrap list-none gap-2 p-0' : 'mt-2 list-none p-0 text-sm text-slate-500'}>
          {unmapped.map((zone) => (
            <li key={zone.zoneId}>
              {interactive ? (
                <button type="button" onClick={() => onSelect?.(zone.zoneId)}>
                  {zone.name} ({zone.availableToSell} libres)
                </button>
              ) : (
                zone.name
              )}
            </li>
          ))}
        </ul>
      )}

      {!interactive && (
        <ul className="mt-2 flex flex-wrap list-none gap-4 p-0 text-xs text-slate-500">
          <li className="flex items-center gap-1.5 before:content-[''] before:size-2.5 before:rounded-full before:bg-green-500">
            {LEGEND_LABEL[mode].low}
          </li>
          <li className="flex items-center gap-1.5 before:content-[''] before:size-2.5 before:rounded-full before:bg-amber-500">
            {LEGEND_LABEL[mode].mid}
          </li>
          <li className="flex items-center gap-1.5 before:content-[''] before:size-2.5 before:rounded-full before:bg-red-500">
            {LEGEND_LABEL[mode].high}
          </li>
        </ul>
      )}
    </div>
  )
}
