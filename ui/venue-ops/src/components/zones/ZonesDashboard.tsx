import type { ZoneOccupancy } from '../../shared/types'
import ZoneGauge from './ZoneGauge'

interface Props {
  zones: ZoneOccupancy[]
}

export default function ZonesDashboard({ zones }: Props) {
  return (
    <section>
      <h2 className="mb-4">Aforo por puerta</h2>
      <div className="grid grid-cols-[repeat(auto-fit,minmax(240px,1fr))] gap-4">
        {zones.map((z) => (
          <ZoneGauge key={z.zoneId} zone={z} />
        ))}
      </div>
    </section>
  )
}
