import type { EventSummary, ScanResult, ZoneOccupancy } from '../../shared/types'
import PredictionPanel from '../prediction/PredictionPanel'
import ScanPanel from './ScanPanel'
import SimulationPanel from '../simulation/SimulationPanel'
import StadiumMap from '../map/StadiumMap'
import ZonesDashboard from '../zones/ZonesDashboard'

interface Props {
  event: EventSummary
  zones: ZoneOccupancy[]
  scanTicket: (ticketId: string) => Promise<ScanResult>
  onZonesReload: () => void
}

export default function OccupancyGrid({ event, zones, scanTicket, onZonesReload }: Props) {
  const hasConfirmedSales = zones.some((zone) => zone.confirmedSold > 0)

  return (
    <div className="mode-grid">
      <ZonesDashboard zones={zones} />
      <SimulationPanel
        eventId={event.id}
        eventStartsAtUtc={event.startsAtUtc}
        onSimulationEnded={onZonesReload}
        defaultMode="Arrivals"
      />
      <ScanPanel onScan={scanTicket} />
      <StadiumMap zones={zones} mode="checkin" />
      {hasConfirmedSales && <PredictionPanel eventId={event.id} eventStartsAtUtc={event.startsAtUtc} />}
    </div>
  )
}
