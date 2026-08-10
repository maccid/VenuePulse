import { useEffect, useState } from 'react'
import type { ActivityEntry, EventRevenueDto, EventSummary, ScanResult, TicketType, VenueOpsMode, ZoneOccupancy } from '../types'
import { matchTicketType } from '../zoneTicketType'
import ModeToggle from './ModeToggle'
import RevenueWidget from './RevenueWidget'
import SimulationPanel from './SimulationPanel'
import PredictionPanel from './PredictionPanel'
import ActivityFeed from './ActivityFeed'
import StadiumMap from './StadiumMap'
import ZonesDashboard from './ZonesDashboard'
import ScanPanel from './ScanPanel'

interface Props {
  event: EventSummary
  zones: ZoneOccupancy[]
  activity: ActivityEntry[]
  onZonesReload: () => void
  scanTicket: (ticketId: string) => Promise<ScanResult>
  onEventUpdated: (event: EventSummary) => void
}

const REVENUE_POLL_MS = 3000

// Un workspace por pestaña, montado solo mientras esa pestaña está activa (carga perezosa, ver
// App.tsx) — dos modos en grid en vez del apilado vertical anterior (ver ADR 0022). "Cerrar
// venta"/"Cancelar evento" solo tienen sentido con el evento en OnSale (Event.CloseSales/Cancel en
// Ticketing ya lo exigen server-side; aquí solo se ocultan los botones para no ofrecer una acción
// que el backend rechazaría con 409).
export default function EventWorkspace({ event, zones, activity, onZonesReload, scanTicket, onEventUpdated }: Props) {
  const [mode, setMode] = useState<VenueOpsMode>('sales')
  const [revenue, setRevenue] = useState<EventRevenueDto | null>(null)
  const [revenueError, setRevenueError] = useState<string | null>(null)
  const [ticketTypes, setTicketTypes] = useState<TicketType[]>([])
  const [actionError, setActionError] = useState<string | null>(null)
  const [actionBusy, setActionBusy] = useState(false)

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

  async function runLifecycleAction(kind: 'close-sales' | 'cancel', confirmMessage: string, nextStatus: EventSummary['status']) {
    if (!window.confirm(confirmMessage)) return

    setActionError(null)
    setActionBusy(true)
    try {
      const response = await fetch(`/api/ticketing/api/events/${event.id}/${kind}`, { method: 'POST' })
      if (!response.ok) {
        setActionError('La acción no se pudo completar (el evento ya no está en venta).')
        return
      }
      onEventUpdated({ ...event, status: nextStatus })
    } catch {
      setActionError('La acción no se pudo completar.')
    } finally {
      setActionBusy(false)
    }
  }

  const isOnSale = event.status === 'OnSale'
  const occupancyDefaultSimulationMode = isOnSale ? 'Sales' : 'Arrivals'
  // Ojo: NO se puede usar revenue.confirmedCount aquí (CheckIn, solo compras reales) — el motor de
  // simulación (Bloque 8) es el camino principal para animar la demo y mueve ConfirmedSold de la
  // zona directamente en AccessControl sin pasar nunca por CheckIn, así que revenue se quedaría
  // siempre a 0 en ese camino y el panel no se montaría jamás. zones.confirmedSold sí sube en
  // ambos caminos (compra real o simulada).
  const hasConfirmedSales = zones.some((z) => z.confirmedSold > 0)

  return (
    <div className="event-workspace">
      <ModeToggle mode={mode} onChange={setMode} />

      {mode === 'sales' ? (
        <div className="mode-grid">
          <SimulationPanel eventId={event.id} eventStartsAtUtc={event.startsAtUtc} onSimulationEnded={onZonesReload} defaultMode="Sales" />
          <RevenueWidget revenue={revenue} error={revenueError} estimatedRevenue={estimatedRevenue} />
          <StadiumMap zones={zones} mode="sales" />
          <ActivityFeed entries={activity} />

          {isOnSale && (
            <section className="card event-actions">
              <h2>Ciclo de vida del evento</h2>
              <div className="event-actions__buttons">
                <button
                  onClick={() => runLifecycleAction('close-sales', '¿Cerrar la venta de este evento? No se podrán comprar más entradas.', 'SalesClosed')}
                  disabled={actionBusy}
                >
                  Cerrar venta
                </button>
                <button
                  className="event-actions__cancel"
                  onClick={() => runLifecycleAction('cancel', '¿Cancelar este evento? Las entradas confirmadas se reembolsarán.', 'Cancelled')}
                  disabled={actionBusy}
                >
                  Cancelar evento
                </button>
              </div>
              {actionError && <p className="scan-result scan-result--error">{actionError}</p>}
            </section>
          )}
        </div>
      ) : (
        <div className="mode-grid">
          <ZonesDashboard zones={zones} />
          <SimulationPanel
            key={occupancyDefaultSimulationMode}
            eventId={event.id}
            eventStartsAtUtc={event.startsAtUtc}
            onSimulationEnded={onZonesReload}
            defaultMode={occupancyDefaultSimulationMode}
          />
          <ScanPanel onScan={scanTicket} />
          <StadiumMap zones={zones} mode="checkin" />
          {hasConfirmedSales && (
            <PredictionPanel eventId={event.id} eventStartsAtUtc={event.startsAtUtc} />
          )}
        </div>
      )}
    </div>
  )
}
