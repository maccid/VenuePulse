import { useEffect, useState } from 'react'
import * as signalR from '@microsoft/signalr'
import type { EventSummary, ScanResult, ZoneOccupancy } from './types'
import SiteHeader from './components/SiteHeader'
import ScanPanel from './components/ScanPanel'
import ZonesDashboard from './components/ZonesDashboard'
import SimulationPanel from './components/SimulationPanel'
import PredictionPanel from './components/PredictionPanel'
import './App.css'

export default function App() {
  const [events, setEvents] = useState<EventSummary[]>([])
  const [selectedEventId, setSelectedEventId] = useState<string | null>(null)
  const [zones, setZones] = useState<ZoneOccupancy[]>([])

  useEffect(() => {
    fetch('/api/ticketing/api/events')
      .then((r) => r.json())
      .then((evs: EventSummary[]) => {
        setEvents(evs)
        if (evs[0]) setSelectedEventId(evs[0].id)
      })
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

    loadZones(selectedEventId)
  }, [selectedEventId])

  function loadZones(eventId: string) {
    fetch(`/api/access-control/api/zones?eventId=${eventId}`)
      .then((r) => r.json())
      .then(setZones)
      .catch(() => setZones([]))
  }

  async function scanTicket(ticketId: string): Promise<ScanResult> {
    const response = await fetch(`/api/checkin/api/tickets/${ticketId}/check-in`, { method: 'POST' })

    if (response.ok) return { kind: 'ok' }

    const body = await response.json().catch(() => ({ error: 'Entrada no válida' }))
    return { kind: 'error', message: body.error ?? 'No se pudo hacer el check-in' }
  }

  const selectedEvent = events.find((ev) => ev.id === selectedEventId)

  return (
    <div className="venue-ops">
      <SiteHeader />

      <label className="event-selector">
        Evento
        <select value={selectedEventId ?? ''} onChange={(e) => setSelectedEventId(e.target.value)}>
          {events.map((ev) => (
            <option key={ev.id} value={ev.id}>{ev.name}</option>
          ))}
        </select>
      </label>

      {selectedEvent && selectedEventId !== null && (
        <SimulationPanel
          eventId={selectedEventId}
          eventStartsAtUtc={selectedEvent.startsAtUtc}
          onSimulationEnded={() => loadZones(selectedEventId)}
        />
      )}

      {selectedEvent && selectedEventId !== null && (
        <PredictionPanel eventId={selectedEventId} eventStartsAtUtc={selectedEvent.startsAtUtc} />
      )}

      <ScanPanel onScan={scanTicket} />

      <ZonesDashboard zones={zones} />
    </div>
  )
}
