import { useEffect, useState } from 'react'
import * as signalR from '@microsoft/signalr'
import type { EventSummary, ZoneOccupancy } from './types'
import './App.css'

type ScanResult = { kind: 'idle' } | { kind: 'ok' } | { kind: 'error'; message: string }

export default function App() {
  const [events, setEvents] = useState<EventSummary[]>([])
  const [selectedEventId, setSelectedEventId] = useState<string | null>(null)
  const [zones, setZones] = useState<ZoneOccupancy[]>([])
  const [ticketId, setTicketId] = useState('')
  const [scanResult, setScanResult] = useState<ScanResult>({ kind: 'idle' })

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

  async function handleScan(e: React.FormEvent) {
    e.preventDefault()
    setScanResult({ kind: 'idle' })

    const response = await fetch(`/api/checkin/api/tickets/${ticketId}/check-in`, { method: 'POST' })

    if (response.ok) {
      setScanResult({ kind: 'ok' })
      setTicketId('')
    } else {
      const body = await response.json().catch(() => ({ error: 'Entrada no válida' }))
      setScanResult({ kind: 'error', message: body.error ?? 'No se pudo hacer el check-in' })
    }
  }

  return (
    <div className="venue-ops">
      <header>
        <h1>VenuePulse · Operaciones</h1>
        <p className="tagline">Escaneo de torniquete y aforo en tiempo real por puerta</p>
        <label className="event-selector">
          Evento
          <select value={selectedEventId ?? ''} onChange={(e) => setSelectedEventId(e.target.value)}>
            {events.map((ev) => (
              <option key={ev.id} value={ev.id}>{ev.name}</option>
            ))}
          </select>
        </label>
      </header>

      <section className="scan-panel">
        <h2>Escanear entrada</h2>
        <form onSubmit={handleScan}>
          <input
            placeholder="ID de la entrada"
            value={ticketId}
            onChange={(e) => setTicketId(e.target.value)}
            required
          />
          <button type="submit">Escanear</button>
        </form>
        {scanResult.kind === 'ok' && <p className="scan-ok">✔ Check-in registrado, aforo actualizado.</p>}
        {scanResult.kind === 'error' && <p className="scan-error">✖ {scanResult.message}</p>}
      </section>

      <section className="zones-dashboard">
        <h2>Aforo por puerta</h2>
        <div className="zone-grid">
          {zones.map((z) => {
            const pct = z.capacity > 0 ? Math.round((z.checkedIn / z.capacity) * 100) : 0
            return (
              <div key={z.zoneId} className="zone-gauge">
                <h3>{z.name}</h3>
                <div className="gauge-bar">
                  <div className="gauge-fill" style={{ width: `${pct}%` }} />
                </div>
                <p>{z.checkedIn} / {z.capacity} dentro ({pct}%)</p>
                <p className="zone-meta">{z.confirmedSold} entradas vendidas</p>
              </div>
            )
          })}
        </div>
      </section>
    </div>
  )
}
