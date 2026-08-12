import { useState } from 'react'
import type { ActivityEntry, EventSummary, ScanResult, TicketType, ZoneOccupancy } from '../../shared/types'
import { matchTicketType } from '../../shared/zoneTicketType'
import { useEventRevenue } from '../../hooks/useEventRevenue'
import { useTicketTypePrices } from '../../hooks/useTicketTypePrices'
import PhaseIndicator from './PhaseIndicator'
import SalesLayout from './SalesLayout'
import OccupancyGrid from './OccupancyGrid'
import Modal from '../../shared/Modal'

interface Props {
  event: EventSummary
  zones: ZoneOccupancy[]
  activity: ActivityEntry[]
  onZonesReload: () => void
  scanTicket: (ticketId: string) => Promise<ScanResult>
  onEventUpdated: (event: EventSummary) => void
}

const CONFIRM_CONTENT: Record<'close-sales' | 'cancel', { title: string; message: string; nextStatus: EventSummary['status'] }> = {
  'close-sales': {
    title: 'Cerrar venta',
    message: '¿Cerrar la venta de este evento? No se podrán comprar más entradas.',
    nextStatus: 'SalesClosed',
  },
  cancel: {
    title: 'Cancelar evento',
    message: '¿Cancelar este evento? Las entradas confirmadas se reembolsarán.',
    nextStatus: 'Cancelled',
  },
}

function estimateRevenueFromZoneSales(zones: ZoneOccupancy[], ticketTypes: TicketType[]): number {
  return zones.reduce((sum, zone) => {
    const ticketType = matchTicketType(zone.name, ticketTypes)
    return sum + (ticketType?.effectivePrice ?? 0) * zone.confirmedSold
  }, 0)
}

export default function EventWorkspace({ event, zones, activity, onZonesReload, scanTicket, onEventUpdated }: Props) {
  const { revenue, error: revenueError } = useEventRevenue(event.id)
  const { ticketTypes } = useTicketTypePrices(event.id)
  const [actionError, setActionError] = useState<string | null>(null)
  const [actionBusy, setActionBusy] = useState(false)
  const [pendingAction, setPendingAction] = useState<'close-sales' | 'cancel' | null>(null)

  const estimatedRevenue = estimateRevenueFromZoneSales(zones, ticketTypes)

  async function runLifecycleAction(kind: 'close-sales' | 'cancel') {
    setActionError(null)
    setActionBusy(true)
    try {
      const response = await fetch(`/api/ticketing/api/events/${event.id}/${kind}`, { method: 'POST' })
      if (!response.ok) {
        setActionError('La acción no se pudo completar (el evento ya no está en venta).')
        return
      }
      onEventUpdated({ ...event, status: CONFIRM_CONTENT[kind].nextStatus })
    } catch {
      setActionError('La acción no se pudo completar.')
    } finally {
      setActionBusy(false)
      setPendingAction(null)
    }
  }

  const isOnSale = event.status === 'OnSale'
  const isOccupancy = event.status === 'SalesClosed'

  function openConfirm(kind: 'close-sales' | 'cancel') {
    setActionError(null)
    setPendingAction(kind)
  }

  const confirm = pendingAction === null ? null : CONFIRM_CONTENT[pendingAction]

  return (
    <div className="ops-detail">
      <div className="ops-detail__toolbar">
        <PhaseIndicator status={event.status} />

        {isOnSale && (
          <div className="sales-layout__actions">
            <button
              type="button"
              className="sales-layout__close"
              onClick={() => openConfirm('close-sales')}
              disabled={actionBusy}
            >
              Cerrar venta
            </button>
            <button
              type="button"
              className="sales-layout__cancel"
              onClick={() => openConfirm('cancel')}
              disabled={actionBusy}
            >
              Cancelar evento
            </button>
          </div>
        )}
      </div>

      {isOccupancy ? (
        <OccupancyGrid event={event} zones={zones} scanTicket={scanTicket} onZonesReload={onZonesReload} />
      ) : (
        <SalesLayout
          event={event}
          zones={zones}
          isOnSale={isOnSale}
          revenue={revenue}
          revenueError={revenueError}
          estimatedRevenue={estimatedRevenue}
          activity={activity}
          onSimulationEnded={onZonesReload}
        />
      )}

      {confirm && pendingAction !== null && (
        <Modal title={confirm.title} onClose={() => setPendingAction(null)}>
          <p className="modal__text">{confirm.message}</p>
          {actionError && <p className="scan-result scan-result--error">{actionError}</p>}
          <div className="modal__actions">
            <button
              type="button"
              className={pendingAction === 'cancel' ? 'sales-layout__cancel' : 'sales-layout__close'}
              onClick={() => void runLifecycleAction(pendingAction)}
              disabled={actionBusy}
            >
              {actionBusy ? 'Procesando…' : pendingAction === 'cancel' ? 'Cancelar evento' : 'Cerrar venta'}
            </button>
            <button type="button" className="modal__secondary" onClick={() => setPendingAction(null)} disabled={actionBusy}>
              Volver
            </button>
          </div>
        </Modal>
      )}
    </div>
  )
}
