import type { ZoneOccupancy } from '../types'
import ZoneGauge from './ZoneGauge'
import StadiumMap from './StadiumMap'

interface Props {
  zones: ZoneOccupancy[]
}

export default function ZonesDashboard({ zones }: Props) {
  return (
    <section className="zones-dashboard">
      <h2>Aforo por puerta</h2>
      <StadiumMap zones={zones} />
      <div className="zone-grid">
        {zones.map((z) => (
          <ZoneGauge key={z.zoneId} zone={z} />
        ))}
      </div>
    </section>
  )
}
