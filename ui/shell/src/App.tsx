import { useEffect, useState } from 'react'
import * as signalR from '@microsoft/signalr'
import type { EventSummary, ZoneOccupancy } from './types'
import './App.css'

export default function App() {
  const [events, setEvents] = useState<EventSummary[]>([])
  const [zones, setZones] = useState<ZoneOccupancy[]>([])
  const [selectedEventId, setSelectedEventId] = useState<string | null>(null)
  const [holder, setHolder] = useState('Ana')

  useEffect(() => {
    fetch('/api/ticketing/api/events')
      .then((r) => r.json())
      .then(setEvents)
      .catch(() => setEvents([]))

    fetch('/api/access-control/api/zones')
      .then((r) => r.json())
      .then(setZones)
      .catch(() => setZones([]))

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

  const totalCapacity = zones.reduce((sum, z) => sum + z.capacity, 0)
  const totalCheckedIn = zones.reduce((sum, z) => sum + z.checkedIn, 0)

  return (
    <div className="shell">
      <header className="shell-header">
        <h1>VenuePulse</h1>
        <p className="tagline">Ticketing y aforo en tiempo real del recinto</p>
      </header>

      <section className="occupancy-widget" aria-label="Aforo en vivo del recinto">
        <h2>Aforo en vivo</h2>
        <div className="occupancy-bar">
          <div
            className="occupancy-fill"
            style={{ width: totalCapacity > 0 ? `${(totalCheckedIn / totalCapacity) * 100}%` : '0%' }}
          />
        </div>
        <p>{totalCheckedIn} / {totalCapacity} personas dentro del recinto</p>
        <ul className="zone-list">
          {zones.map((z) => (
            <li key={z.zoneId}>
              <span>{z.name}</span>
              <span>{z.checkedIn} / {z.capacity}</span>
            </li>
          ))}
        </ul>
      </section>

      {selectedEventId === null ? (
        <section className="events">
          <h2>Próximos eventos</h2>
          <div className="holder-field">
            <label htmlFor="holder">Tu nombre</label>
            <input id="holder" value={holder} onChange={(e) => setHolder(e.target.value)} />
          </div>
          <ul className="event-list">
            {events.map((ev) => (
              <li key={ev.id} className="event-card">
                <div>
                  <strong>{ev.name}</strong>
                  <p>{ev.venueName} · {new Date(ev.startsAtUtc).toLocaleString('es-ES')}</p>
                </div>
                <button onClick={() => setSelectedEventId(ev.id)}>Comprar entrada</button>
              </li>
            ))}
          </ul>
        </section>
      ) : (
        <section className="checkout-panel">
          <button className="back-link" onClick={() => setSelectedEventId(null)}>← Volver al catálogo</button>
          {/* Micro frontend independiente: su propio bundle, su propio fetch de tipos de entrada
              y zonas, montado aquí como Web Component (ver ADR 0004). */}
          <ticket-checkout-app event-id={selectedEventId} holder={holder} />
        </section>
      )}
    </div>
  )
}
