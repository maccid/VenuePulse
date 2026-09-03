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
  return (
    <div className="flex min-w-0 flex-col gap-6">
      <div className="grid grid-cols-[repeat(auto-fit,minmax(320px,1fr))] gap-6">
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
          resetScope="arrivals"
        />
      </div>

      <ZonesDashboard zones={zones} />
      <PredictionPanel eventId={event.id} eventStartsAtUtc={event.startsAtUtc} />
    </div>
  )
}
