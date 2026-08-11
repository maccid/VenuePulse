import { useEffect, useRef, useState } from 'react'
import * as signalR from '@microsoft/signalr'
import type { ActivityEntry, EventSummary, ScanResult, TicketStatusUpdate, ZoneOccupancy } from './types'
import SiteHeader from './components/SiteHeader'
import EventList from './components/EventList'
import EventCreateForm from './components/EventCreateForm'
import EventWorkspace from './components/EventWorkspace'
import './App.css'

const MAX_ACTIVITY_ENTRIES = 20

export default function App() {
  const [events, setEvents] = useState<EventSummary[]>([])
  const [activeId, setActiveId] = useState<string | null>(null)
  const [showCreate, setShowCreate] = useState(false)
  const [zonesByEvent, setZonesByEvent] = useState<Record<string, ZoneOccupancy[]>>({})
  const [activityByEvent, setActivityByEvent] = useState<Record<string, ActivityEntry[]>>({})

  // Los handlers de los hubs se suscriben una sola vez; leen el estado más reciente por ref para
  // evitar cierres obsoletos.
  const zonesByEventRef = useRef(zonesByEvent)
  useEffect(() => {
    zonesByEventRef.current = zonesByEvent
  }, [zonesByEvent])

  useEffect(() => {
    fetch('/api/ticketing/api/events')
      .then((r) => r.json())
      .then((evs: EventSummary[]) => {
        const sorted = [...evs].sort((a, b) => b.startsAtUtc.localeCompare(a.startsAtUtc))
        setEvents(sorted)
        if (sorted[0]) setActiveId(sorted[0].id)
        else setShowCreate(true)
      })
      .catch(() => setEvents([]))

    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/occupancy')
      .withAutomaticReconnect()
      .build()

    // Un único socket para todos los eventos (Bloque 18): solo aplica la actualización a pestañas
    // que ya han hecho su carga perezosa inicial (existing !== undefined) — evita reconstruir un
    // aforo a medias para un evento que el operador nunca ha abierto.
    connection.on('occupancyChanged', (updated: ZoneOccupancy) => {
      setZonesByEvent((prev) => {
        const existing = prev[updated.eventId]
        if (existing === undefined) return prev

        const hasZone = existing.some((z) => z.zoneId === updated.zoneId)
        const next = hasZone
          ? existing.map((z) => (z.zoneId === updated.zoneId ? updated : z))
          : [...existing, updated]

        return { ...prev, [updated.eventId]: next }
      })
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

    connection.on('ticketStatusChanged', (update: TicketStatusUpdate) => {
      if (update.status !== 'AwaitingPayment' && update.status !== 'Confirmed' && update.status !== 'Rejected') return

      const zone = zonesByEventRef.current[update.eventId]?.find((z) => z.zoneId === update.zoneId)

      setActivityByEvent((prev) => {
        const existing = prev[update.eventId] ?? []
        const entry: ActivityEntry = {
          id: `${update.ticketId}-${update.status}`,
          zoneName: zone?.name ?? 'Zona desconocida',
          status: update.status,
          rejectionReason: update.rejectionReason,
          atUtc: new Date().toISOString(),
        }
        return { ...prev, [update.eventId]: [entry, ...existing].slice(0, MAX_ACTIVITY_ENTRIES) }
      })
    })

    connection.start().catch((err) => console.error('No se pudo conectar al hub de entradas', err))

    return () => {
      connection.stop()
    }
  }, [])

  // Carga perezosa por evento: solo se piden zonas la primera vez que se selecciona — las
  // visitas siguientes reutilizan lo que ya está en zonesByEvent (mantenido fresco por el socket
  // de arriba aunque el detalle no esté montado).
  useEffect(() => {
    if (activeId === null) return
    if (zonesByEventRef.current[activeId] !== undefined) return
    loadZones(activeId)
  }, [activeId])

  function loadZones(eventId: string) {
    fetch(`/api/access-control/api/zones?eventId=${eventId}`)
      .then((r) => r.json())
      .then((zones: ZoneOccupancy[]) => setZonesByEvent((prev) => ({ ...prev, [eventId]: zones })))
      .catch(() => setZonesByEvent((prev) => ({ ...prev, [eventId]: [] })))
  }

  function updateEvent(updated: EventSummary) {
    setEvents((prev) => prev.map((ev) => (ev.id === updated.id ? updated : ev)))
  }

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
                setEvents((prev) => [...prev, created])
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
