import type { ScanResult } from './shared/types'
import { useEventCatalog } from './hooks/useEventCatalog'
import { useZonesByEvent } from './hooks/useZonesByEvent'
import { useTicketActivityFeed } from './hooks/useTicketActivityFeed'
import SiteHeader from './components/ui/SiteHeader'
import EventList from './components/event/EventList'
import EventCreateForm from './components/event/EventCreateForm'
import EventWorkspace from './components/event/EventWorkspace'
import './App.css'

export default function App() {
  const { events, activeId, setActiveId, showCreate, setShowCreate, updateEvent, addEvent } = useEventCatalog()
  const { zonesByEvent, loadZones } = useZonesByEvent(activeId)
  const activityByEvent = useTicketActivityFeed(zonesByEvent)

  async function scanTicket(ticketId: string): Promise<ScanResult> {
    const response = await fetch(`/api/checkin/api/tickets/${ticketId}/check-in`, { method: 'POST' })

    if (response.ok) return { kind: 'ok' }

    const body = await response.json().catch(() => ({ error: 'Entrada no válida' }))
    return { kind: 'error', message: body.error ?? 'No se pudo hacer el check-in' }
  }

  const activeEvent = activeId === null ? undefined : events.find((ev) => ev.id === activeId)

  return (
    <div className="venue-ops">
      <SiteHeader />

      <div className="venue-ops__layout">
        <EventList
          events={events}
          activeId={activeId}
          onSelect={setActiveId}
          onCreate={() => setShowCreate(true)}
        />

        {showCreate ? (
          <div className="ops-new-event">
            <EventCreateForm
              onCreated={(created) => {
                addEvent(created)
                setActiveId(created.id)
                setShowCreate(false)
              }}
              onCancel={() => setShowCreate(false)}
            />
          </div>
        ) : activeEvent ? (
          <EventWorkspace
            key={activeEvent.id}
            event={activeEvent}
            zones={zonesByEvent[activeEvent.id] ?? []}
            activity={activityByEvent[activeEvent.id] ?? []}
            onZonesReload={() => loadZones(activeEvent.id)}
            scanTicket={scanTicket}
            onEventUpdated={updateEvent}
          />
        ) : (
          <div className="venue-ops__empty card">
            <p>Selecciona un evento del listado para ver sus operaciones.</p>
          </div>
        )}
      </div>
    </div>
  )
}
