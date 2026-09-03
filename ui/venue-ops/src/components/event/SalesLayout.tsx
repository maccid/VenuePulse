import type { ActivityEntry } from '../../shared/types'
import type { EventRevenueDto, EventSummary, ZoneOccupancy } from '@venue/core/domain'
import { MapPin } from 'lucide-react'
import ActivityFeed from './ActivityFeed'
import RevenueWidget from './RevenueWidget'
import SimulationPanel from '../simulation/SimulationPanel'
import { StadiumMap } from '@venue/core/map'

interface Props {
  event: EventSummary
  zones: ZoneOccupancy[]
  isOnSale: boolean
  revenue: EventRevenueDto | null
  revenueError: string | null
  estimatedRevenue: number
  activity: ActivityEntry[]
  onSimulationEnded: () => void
}

export default function SalesLayout({
  event,
  zones,
  isOnSale,
  revenue,
  revenueError,
  estimatedRevenue,
  activity,
  onSimulationEnded,
}: Props) {
  const totalSold = zones.reduce((sum, zone) => sum + zone.confirmedSold, 0)

  return (
    <div className="flex flex-col gap-6">
      <div className="grid grid-cols-[repeat(auto-fit,minmax(320px,1fr))] gap-6">
        <section className="card">
          <h2 className="flex items-center gap-2">
            <MapPin className="size-[18px] text-accent" aria-hidden="true" />
            Mapa del evento
          </h2>
          <StadiumMap zones={zones} mode="sales" />
        </section>

        {isOnSale && (
          <SimulationPanel
            eventId={event.id}
            eventStartsAtUtc={event.startsAtUtc}
            onSimulationEnded={onSimulationEnded}
            defaultMode="Sales"
          />
        )}
      </div>

      <div className="grid grid-cols-[repeat(auto-fit,minmax(320px,1fr))] gap-6">
        <RevenueWidget revenue={revenue} error={revenueError} estimatedRevenue={estimatedRevenue} sold={totalSold} />
        <section className="card">
          <ActivityFeed entries={activity} />
        </section>
      </div>
    </div>
  )
}
