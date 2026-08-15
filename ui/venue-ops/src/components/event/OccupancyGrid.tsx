import type { ScanResult } from '../../shared/types'
import type { EventSummary, ZoneOccupancy } from '@venue/core/domain'
import PredictionPanel from '../prediction/PredictionPanel'
import ScanPanel from './ScanPanel'
import SimulationPanel from '../simulation/SimulationPanel'
import { StadiumMap } from '@venue/core/map'
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
    <div className="grid grid-cols-[repeat(auto-fit,minmax(360px,1fr))] items-start gap-6">
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
