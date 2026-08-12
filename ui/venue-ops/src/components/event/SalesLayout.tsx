import type { ActivityEntry, EventRevenueDto, EventSummary, ZoneOccupancy } from '../../shared/types'
import ActivityFeed from './ActivityFeed'
import RevenueWidget from './RevenueWidget'
import SimulationPanel from '../simulation/SimulationPanel'
import StadiumMap from '../map/StadiumMap'

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
    <div className="sales-layout">
      <div className="sales-layout__main">
        <section className="sales-layout__panel">
          <h2 className="sales-layout__panel-title">
            <svg
              className="sales-layout__panel-icon"
              viewBox="0 0 24 24"
              fill="none"
              stroke="currentColor"
              strokeWidth="2"
              strokeLinecap="round"
              strokeLinejoin="round"
              aria-hidden="true"
            >
              <path d="M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z" />
              <circle cx="12" cy="10" r="3" />
            </svg>
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

      <div className="sales-layout__footer">
        <RevenueWidget revenue={revenue} error={revenueError} estimatedRevenue={estimatedRevenue} sold={totalSold} />
        <section className="sales-layout__panel">
          <ActivityFeed entries={activity} />
        </section>
      </div>
    </div>
  )
}
