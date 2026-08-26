import type { EventSummary, ZoneOccupancy } from '@venue/core/domain'
import { MapPin } from 'lucide-react'
import PredictionPanel from '../prediction/PredictionPanel'
import SimulationPanel from '../simulation/SimulationPanel'
import { StadiumMap } from '@venue/core/map'
import ZonesDashboard from '../zones/ZonesDashboard'

interface Props {
  event: EventSummary
  zones: ZoneOccupancy[]
  onZonesReload: () => void
}

export default function OccupancyGrid({ event, zones, onZonesReload }: Props) {
  const hasConfirmedSales = zones.some((zone) => zone.confirmedSold > 0)

  return (
    <div className="flex flex-col gap-6">
      <div className="grid grid-cols-[repeat(auto-fit,minmax(320px,1fr))] items-start gap-6">
        <section className="card">
          <h2 className="flex items-center gap-2">
            <MapPin className="size-[18px] text-accent" aria-hidden="true" />
            Mapa del evento
          </h2>
          <StadiumMap zones={zones} mode="checkin" />
        </section>
        <SimulationPanel
          eventId={event.id}
          eventStartsAtUtc={event.startsAtUtc}
          onSimulationEnded={onZonesReload}
          defaultMode="Arrivals"
        />
      </div>

      <div className="grid grid-cols-[repeat(auto-fit,minmax(320px,1fr))] items-start gap-6">
        <ZonesDashboard zones={zones} />
        {hasConfirmedSales && <PredictionPanel eventId={event.id} eventStartsAtUtc={event.startsAtUtc} />}
      </div>
    </div>
  )
}
