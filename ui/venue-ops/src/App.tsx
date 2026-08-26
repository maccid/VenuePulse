import { useEffect, useState } from 'react'
import type { ScanResult } from './shared/types'
import type { TicketType } from '@venue/core/domain'
import { useEventCatalog } from './hooks/useEventCatalog'
import { useZonesByEvent } from './hooks/useZonesByEvent'
import { useTicketActivityFeed } from './hooks/useTicketActivityFeed'
import SiteHeader from './components/ui/SiteHeader'
import EventList from './components/event/EventList'
import EventCreateForm from './components/event/EventCreateForm'
import EventWorkspace from './components/event/EventWorkspace'
import ScanPage from './components/event/ScanPage'
import './App.css'

const isScanRoute = window.location.pathname.replace(/\/+$/, '') === '/venue-ops/scan'

async function scanTicket(ticketId: string): Promise<ScanResult> {
  const response = await fetch(`/api/checkin/api/tickets/${ticketId}/check-in`, { method: 'POST' })

  if (response.ok) return { kind: 'ok' }

  const body = await response.json().catch(() => ({ error: 'Entrada no válida' }))
  return { kind: 'error', message: body.error ?? 'No se pudo hacer el check-in' }
}

export default function App() {
  return isScanRoute ? <ScanApp /> : <OpsApp />
}

function ScanApp() {
  return <ScanPage onScan={scanTicket} />
}

function OpsApp() {
  const { events, activeId, selectEvent, showCreate, startCreate, cancelCreate, updateEvent, addEvent } =
    useEventCatalog()
  const { zonesByEvent, loadZones } = useZonesByEvent(activeId)

  const [ticketTypes, setTicketTypes] = useState<TicketType[]>([])
  useEffect(() => {
    if (!activeId) { setTicketTypes([]); return }
    fetch(`/api/ticketing/api/events/${activeId}/ticket-types`)
      .then((r) => r.ok ? r.json() : [])
      .then((data) => setTicketTypes(data as TicketType[]))
      .catch(() => setTicketTypes([]))
  }, [activeId])

  const { activityByEvent, clearEvent: clearActivity } = useTicketActivityFeed(activeId, zonesByEvent, ticketTypes)

  const activeEvent = activeId === null ? undefined : events.find((ev) => ev.id === activeId)

  return (
    <div className="mx-auto max-w-[1440px] px-6 py-16">
      <SiteHeader />

      <div className="grid grid-cols-[minmax(280px,25%)_1fr] items-start gap-6">
        <EventList
          events={events}
          activeId={activeId}
          onSelect={selectEvent}
          onCreate={startCreate}
        />

        {showCreate ? (
          <div className="card">
            <EventCreateForm
              onCreated={(created) => {
                addEvent(created)
                selectEvent(created.id)
              }}
              onCancel={cancelCreate}
            />
          </div>
        ) : activeEvent ? (
          <EventWorkspace
            key={activeEvent.id}
            event={activeEvent}
            zones={zonesByEvent[activeEvent.id] ?? []}
            activity={activityByEvent[activeEvent.id] ?? []}
            onZonesReload={() => loadZones(activeEvent.id)}
            onSimulationReset={() => {
              loadZones(activeEvent.id)
              clearActivity(activeEvent.id)
            }}
            onEventUpdated={updateEvent}
          />
        ) : (
          <div className="card text-sm text-text-muted">
            <p>Selecciona un evento del listado para ver sus operaciones.</p>
          </div>
        )}
      </div>
    </div>
  )
}
