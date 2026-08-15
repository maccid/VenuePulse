import { useEffect, useState } from 'react'
import { api, API_ROUTES } from '@venue/core/api'
import type { EventSummary } from '@venue/core/domain'
import SiteHeader from './components/SiteHeader'
import EventCatalog from './components/EventCatalog'
import CheckoutPanel from './components/CheckoutPanel'
import './App.css'

export default function App() {
  const [events, setEvents] = useState<EventSummary[]>([])
  const [selectedEventId, setSelectedEventId] = useState<string | null>(null)

  useEffect(() => {
    api
      .get<EventSummary[]>(API_ROUTES.events)
      .then(setEvents)
      .catch(() => setEvents([]))
  }, [])

  return (
    <div className="shell">
      <SiteHeader />

      {selectedEventId === null ? (
        <EventCatalog events={events} onSelectEvent={setSelectedEventId} />
      ) : (
        <CheckoutPanel eventId={selectedEventId} onBack={() => setSelectedEventId(null)} />
      )}
    </div>
  )
}
