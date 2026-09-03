import type { ZoneOccupancy } from '@venue/core/domain'
import { occupancyRatio, occupancyTier } from '@venue/core/domain'

interface Props {
  zone: ZoneOccupancy
}

const TIER_BORDER: Record<string, string> = {
  low: 'border-t-green-500',
  mid: 'border-t-amber-500',
  high: 'border-t-red-500',
}

export default function ZoneGauge({ zone }: Props) {
  const pct = zone.capacity > 0 ? Math.round((zone.checkedIn / zone.capacity) * 100) : 0
  const noShows = Math.max(0, zone.confirmedSold - zone.checkedIn)
  const tier = occupancyTier(occupancyRatio(zone, 'checkin'))

  return (
    <div
      className={`rounded-md border border-border border-t-[3px] bg-bg p-3 ${TIER_BORDER[tier]}`}
    >
      <h3 className="mb-3">{zone.name}</h3>
      <div className="h-2.5 overflow-hidden rounded-full bg-accent-bg">
        <div
          className="h-full bg-linear-to-r from-accent to-accent-hover transition-[width] duration-[400ms]"
          style={{ width: `${pct}%` }}
        />
      </div>
      <p className="mt-3 flex items-baseline justify-between">
        <span className="font-[650]">
          {zone.checkedIn} <em className="text-sm font-normal not-italic text-text-muted">/ {zone.capacity} dentro</em>
        </span>
        <span className="text-sm text-text-muted">{pct}%</span>
      </p>
      <p className="mt-1 text-sm text-text-muted">
        {zone.confirmedSold} entradas vendidas
        {noShows > 0 && <em className="not-italic text-danger"> · {noShows} no-shows</em>}
      </p>
    </div>
  )
}
