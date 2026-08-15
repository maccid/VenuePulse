import type { ZoneOccupancy } from '@venue/core/domain'

interface Props {
  zone: ZoneOccupancy
}

export default function ZoneGauge({ zone }: Props) {
  const pct = zone.capacity > 0 ? Math.round((zone.checkedIn / zone.capacity) * 100) : 0
  const noShows = Math.max(0, zone.confirmedSold - zone.checkedIn)

  return (
    <div className="card">
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
