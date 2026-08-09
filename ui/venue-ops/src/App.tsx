import { useEffect, useRef, useState } from 'react'
import * as signalR from '@microsoft/signalr'
import type { ActivityEntry, EventSummary, ScanResult, TicketStatusUpdate, ZoneOccupancy } from './types'
import SiteHeader from './components/SiteHeader'
import ScanPanel from './components/ScanPanel'
import ZonesDashboard from './components/ZonesDashboard'
import SimulationPanel from './components/SimulationPanel'
import PredictionPanel from './components/PredictionPanel'
import ActivityFeed from './components/ActivityFeed'
import './App.css'

const MAX_ACTIVITY_ENTRIES = 20

export default function App() {
  const [events, setEvents] = useState<EventSummary[]>([])
  const [selectedEventId, setSelectedEventId] = useState<string | null>(null)
  const [zones, setZones] = useState<ZoneOccupancy[]>([])
  const [activity, setActivity] = useState<ActivityEntry[]>([])

  // Evita cierres obsoletos dentro del handler del hub de entradas, que se suscribe una sola vez.
  const zonesRef = useRef(zones)
  useEffect(() => {
    zonesRef.current = zones
  }, [zones])

  const selectedEventIdRef = useRef(selectedEventId)
  useEffect(() => {
    selectedEventIdRef.current = selectedEventId
  }, [selectedEventId])

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
    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/tickets')
      .withAutomaticReconnect()
      .build()

    // El hub emite para todas las entradas de todos los eventos; se filtra aquí por el evento
    // seleccionado (vía ref, la suscripción solo se monta una vez) y se resuelve el nombre de la
    // zona con las zonas ya cargadas — sin pedir la entrada completa por separado.
    connection.on('ticketStatusChanged', (update: TicketStatusUpdate) => {
      if (update.eventId !== selectedEventIdRef.current) return
      if (update.status !== 'AwaitingPayment' && update.status !== 'Confirmed' && update.status !== 'Rejected') return

      const zone = zonesRef.current.find((z) => z.zoneId === update.zoneId)

      setActivity((prev) =>
        [
          {
            id: `${update.ticketId}-${update.status}`,
            zoneName: zone?.name ?? 'Zona desconocida',
            status: update.status,
            rejectionReason: update.rejectionReason,
            atUtc: new Date().toISOString(),
          },
          ...prev,
        ].slice(0, MAX_ACTIVITY_ENTRIES),
      )
    })

    connection.start().catch((err) => console.error('No se pudo conectar al hub de entradas', err))

    return () => {
      connection.stop()
    }
  }, [])

  useEffect(() => {
    if (selectedEventId === null) {
      setZones([])
      return
    }

    setActivity([])
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

      <ActivityFeed entries={activity} />
    </div>
  )
}
