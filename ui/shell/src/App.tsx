import { useEffect, useState } from 'react'
import type { EventSummary } from './types'
import SiteHeader from './components/SiteHeader'
import EventCatalog from './components/EventCatalog'
import CheckoutPanel from './components/CheckoutPanel'
import './App.css'

export default function App() {
  const [events, setEvents] = useState<EventSummary[]>([])
  const [selectedEventId, setSelectedEventId] = useState<string | null>(null)

  useEffect(() => {
    fetch('/api/ticketing/api/events')
      .then((r) => r.json())
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
