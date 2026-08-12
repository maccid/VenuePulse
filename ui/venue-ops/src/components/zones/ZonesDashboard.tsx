import type { ZoneOccupancy } from '../../shared/types'
import ZoneGauge from './ZoneGauge'

interface Props {
  zones: ZoneOccupancy[]
}

export default function ZonesDashboard({ zones }: Props) {
  return (
    <section className="zones-dashboard">
      <h2>Aforo por puerta</h2>
      <div className="zone-grid">
        {zones.map((z) => (
          <ZoneGauge key={z.zoneId} zone={z} />
        ))}
      </div>
    </section>
  )
}
