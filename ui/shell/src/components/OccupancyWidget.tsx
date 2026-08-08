import type { ZoneOccupancy } from '../types'

interface Props {
  zones: ZoneOccupancy[]
}

export default function OccupancyWidget({ zones }: Props) {
  const totalCapacity = zones.reduce((sum, z) => sum + z.capacity, 0)
  const totalCheckedIn = zones.reduce((sum, z) => sum + z.checkedIn, 0)
  const pct = totalCapacity > 0 ? (totalCheckedIn / totalCapacity) * 100 : 0

  return (
    <section className="card occupancy-widget" aria-label="Aforo en vivo del recinto">
      <h2>Aforo en vivo</h2>
      <div className="occupancy-bar">
        <div className="occupancy-fill" style={{ width: `${pct}%` }} />
      </div>
      <p className="occupancy-widget__summary">
        <span className="occupancy-widget__count">
          {totalCheckedIn} <em>/ {totalCapacity} personas dentro del recinto</em>
        </span>
      </p>
      <ul className="zone-list">
        {zones.map((z) => (
          <li key={z.zoneId}>
            <span>
              <strong>{z.name}</strong>
            </span>
            <span>
              {z.checkedIn} / {z.capacity}
            </span>
          </li>
        ))}
      </ul>
    </section>
  )
}
