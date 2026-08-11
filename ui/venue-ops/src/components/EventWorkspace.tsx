import { useEffect, useState } from 'react'
import type { ActivityEntry, EventRevenueDto, EventSummary, ScanResult, TicketType, ZoneOccupancy } from '../types'
import { matchTicketType } from '../zoneTicketType'
import PhaseIndicator from './PhaseIndicator'
import RevenueWidget from './RevenueWidget'
import SimulationPanel from './SimulationPanel'
import PredictionPanel from './PredictionPanel'
import ActivityFeed from './ActivityFeed'
import StadiumMap from './StadiumMap'
import ZonesDashboard from './ZonesDashboard'
import ScanPanel from './ScanPanel'
import Modal from './Modal'

interface Props {
  event: EventSummary
  zones: ZoneOccupancy[]
  activity: ActivityEntry[]
  onZonesReload: () => void
  scanTicket: (ticketId: string) => Promise<ScanResult>
  onEventUpdated: (event: EventSummary) => void
}

const REVENUE_POLL_MS = 3000

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

// Un workspace por evento, montado solo mientras ese evento está seleccionado (carga perezosa, ver
// App.tsx). La vista sigue al estado del evento — fases, no modos intercambiables: OnSale/Cancelled
// muestran el detalle de venta (mapa del evento primero, simulación al lado, e ingresos + actividad
// debajo) y SalesClosed el grid de ocupación. "Cerrar venta"/"Cancelar evento" abren un modal en
// vez del window.confirm anterior, y solo tienen sentido con el evento en OnSale (Event.CloseSales/
// Cancel en Ticketing ya lo exigen server-side; aquí solo se ocultan los botones y la simulación
// para no ofrecer algo que el backend rechazaría). El cierre es 100% manual: una simulación nunca
// cierra el evento (decisión de la sesión).
export default function EventWorkspace({ event, zones, activity, onZonesReload, scanTicket, onEventUpdated }: Props) {
  const [revenue, setRevenue] = useState<EventRevenueDto | null>(null)
  const [revenueError, setRevenueError] = useState<string | null>(null)
  const [ticketTypes, setTicketTypes] = useState<TicketType[]>([])
  const [actionError, setActionError] = useState<string | null>(null)
  const [actionBusy, setActionBusy] = useState(false)
  const [pendingAction, setPendingAction] = useState<'close-sales' | 'cancel' | null>(null)

  useEffect(() => {
    let cancelled = false

    async function loadRevenue() {
      try {
        const response = await fetch(`/api/checkin/api/tickets/events/${event.id}/revenue`)
        if (cancelled) return
        if (!response.ok) {
          setRevenueError('No se pudieron cargar los ingresos')
          return
        }
        setRevenue((await response.json()) as EventRevenueDto)
        setRevenueError(null)
      } catch {
        if (!cancelled) setRevenueError('No se pudieron cargar los ingresos')
      }
    }

    void loadRevenue()
    const interval = window.setInterval(loadRevenue, REVENUE_POLL_MS)

    return () => {
      cancelled = true
      window.clearInterval(interval)
    }
  }, [event.id])

  // Precios por tipo de entrada, para estimar ingresos por venta simulada (ver Decisión de ADR
  // 0022): el simulador (Bloque 8) mueve zone.confirmedSold sin crear ningún Ticket real en
  // CheckIn, así que `revenue` se queda siempre a 0 en una demo solo-simulador. Se sondea igual que
  // los ingresos reales porque effectivePrice cambia con la ocupación (Bloque 9).
  useEffect(() => {
    let cancelled = false

    async function loadTicketTypes() {
      try {
        const response = await fetch(`/api/ticketing/api/events/${event.id}/ticket-types`)
        if (cancelled || !response.ok) return
        setTicketTypes((await response.json()) as TicketType[])
      } catch {
        // Silencioso a propósito: si falla, la estimación simplemente se queda en 0 — no es un
        // dato de negocio real, no merece su propio estado de error junto al de ingresos reales.
      }
    }

    void loadTicketTypes()
    const interval = window.setInterval(loadTicketTypes, REVENUE_POLL_MS)

    return () => {
      cancelled = true
      window.clearInterval(interval)
    }
  }, [event.id])

  const estimatedRevenue = zones.reduce((sum, zone) => {
    const ticketType = matchTicketType(zone.name, ticketTypes)
    return sum + (ticketType?.effectivePrice ?? 0) * zone.confirmedSold
  }, 0)

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
  // Ojo: NO se puede usar revenue.confirmedCount aquí (CheckIn, solo compras reales) — el motor de
  // simulación (Bloque 8) es el camino principal para animar la demo y mueve ConfirmedSold de la
  // zona directamente en AccessControl sin pasar nunca por CheckIn, así que revenue se quedaría
  // siempre a 0 en ese camino y el panel no se montaría jamás. zones.confirmedSold sí sube en
  // ambos caminos (compra real o simulada).
  const hasConfirmedSales = zones.some((z) => z.confirmedSold > 0)
  const totalSold = zones.reduce((sum, z) => sum + z.confirmedSold, 0)
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
          {hasConfirmedSales && (
            <PredictionPanel eventId={event.id} eventStartsAtUtc={event.startsAtUtc} />
          )}
        </div>
      ) : (
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
                onSimulationEnded={onZonesReload}
                defaultMode="Sales"
              />
            )}
          </div>

          <div className="sales-layout__footer">
            <RevenueWidget
              revenue={revenue}
              error={revenueError}
              estimatedRevenue={estimatedRevenue}
              sold={totalSold}
            />
            <section className="sales-layout__panel">
              <ActivityFeed entries={activity} />
            </section>
          </div>
        </div>
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
