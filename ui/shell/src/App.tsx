import { useEffect, useState } from 'react'
import * as signalR from '@microsoft/signalr'
import type { EventSummary, ZoneOccupancy } from './types'
import SiteHeader from './components/SiteHeader'
import OccupancyWidget from './components/OccupancyWidget'
import EventCatalog from './components/EventCatalog'
import CheckoutPanel from './components/CheckoutPanel'
import './App.css'

export default function App() {
  const [events, setEvents] = useState<EventSummary[]>([])
  const [zones, setZones] = useState<ZoneOccupancy[]>([])
  const [selectedEventId, setSelectedEventId] = useState<string | null>(null)

  useEffect(() => {
    fetch('/api/ticketing/api/events')
      .then((r) => r.json())
      .then(setEvents)
      .catch(() => setEvents([]))

    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/occupancy')
      .withAutomaticReconnect()
      .build()

    connection.on('occupancyChanged', (updated: ZoneOccupancy) => {
      setZones((prev) => prev.map((z) => (z.zoneId === updated.zoneId ? updated : z)))
    })

    connection.start().catch((err) => console.error('No se pudo conectar al hub de aforo', err))

    return () => {
      connection.stop()
    }
  }, [])

  useEffect(() => {
    if (selectedEventId === null) {
      setZones([])
      return
    }

    fetch(`/api/access-control/api/zones?eventId=${selectedEventId}`)
      .then((r) => r.json())
      .then(setZones)
      .catch(() => setZones([]))
  }, [selectedEventId])

  return (
    <div className="shell">
      <SiteHeader />

      {selectedEventId === null ? (
        <EventCatalog events={events} onSelectEvent={setSelectedEventId} />
      ) : (
        <>
          <OccupancyWidget zones={zones} />

          <CheckoutPanel eventId={selectedEventId} onBack={() => setSelectedEventId(null)} />
        </>
      )}
    </div>
  )
}
