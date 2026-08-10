import type { ZoneOccupancy } from '../types'
import ZoneGauge from './ZoneGauge'

interface Props {
  zones: ZoneOccupancy[]
}

// El plano (StadiumMap) vive a nivel del workspace del evento, no aquí — en el Bloque 18 pasa a
// mostrarse en ambos modos con un coloreado distinto (ver ADR 0022), así que ya no tiene sentido
// que sea parte del dashboard de solo-ocupación.
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
