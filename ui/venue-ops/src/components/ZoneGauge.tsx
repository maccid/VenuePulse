import type { ZoneOccupancy } from '../types'

interface Props {
  zone: ZoneOccupancy
}

export default function ZoneGauge({ zone }: Props) {
  const pct = zone.capacity > 0 ? Math.round((zone.checkedIn / zone.capacity) * 100) : 0
  const noShows = Math.max(0, zone.confirmedSold - zone.checkedIn)

  return (
    <div className="card zone-gauge">
      <h3>{zone.name}</h3>
      <div className="gauge-bar">
        <div className="gauge-fill" style={{ width: `${pct}%` }} />
      </div>
      <p className="zone-gauge__stats">
        <span className="zone-gauge__count">
          {zone.checkedIn} <em>/ {zone.capacity} dentro</em>
        </span>
        <span className="zone-gauge__pct">{pct}%</span>
      </p>
      <p className="zone-meta">
        {zone.confirmedSold} entradas vendidas
        {noShows > 0 && <em className="zone-meta__no-shows"> · {noShows} no-shows</em>}
      </p>
    </div>
  )
}
