import { useEffect, useState } from 'react'
import { api, API_ROUTES } from '@venue/core/api'
import type { EventSummary } from '@venue/core/domain'
import SiteHeader from './components/SiteHeader'
import EventCatalog from './components/EventCatalog'
import CheckoutPanel from './components/CheckoutPanel'
import './App.css'

const EVENT_PARAM = 'event'

function eventFromUrl(): string | null {
  return new URLSearchParams(window.location.search).get(EVENT_PARAM)
}

function updateUrl(eventId: string | null) {
  const url = new URL(window.location.href)
  if (eventId) {
    url.searchParams.set(EVENT_PARAM, eventId)
  } else {
    url.searchParams.delete(EVENT_PARAM)
  }
  window.history.pushState({}, '', url)
}

export default function App() {
  const [events, setEvents] = useState<EventSummary[]>([])
  const [selectedEventId, setSelectedEventId] = useState<string | null>(() => eventFromUrl())

  useEffect(() => {
    api
      .get<EventSummary[]>(API_ROUTES.events)
      .then(setEvents)
      .catch(() => setEvents([]))
  }, [])

  useEffect(() => {
    const handlePopState = () => setSelectedEventId(eventFromUrl())
    window.addEventListener('popstate', handlePopState)
    return () => window.removeEventListener('popstate', handlePopState)
  }, [])

  function selectEvent(id: string) {
    setSelectedEventId(id)
    updateUrl(id)
  }

  function backToCatalog() {
    setSelectedEventId(null)
    updateUrl(null)
  }

  return (
    <div className="shell">
      <SiteHeader />

      {selectedEventId === null ? (
        <EventCatalog events={events} onSelectEvent={selectEvent} />
      ) : (
        <CheckoutPanel eventId={selectedEventId} onBack={backToCatalog} />
      )}
    </div>
  )
}
