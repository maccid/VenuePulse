import { useEffect, useRef, useState } from 'react'
import * as signalR from '@microsoft/signalr'
import type { ActivityEntry, EventSummary, ScanResult, TicketStatusUpdate, ZoneOccupancy } from './types'
import SiteHeader from './components/SiteHeader'
import EventTabs, { NEW_EVENT_TAB_ID } from './components/EventTabs'
import EventCreateForm from './components/EventCreateForm'
import EventWorkspace from './components/EventWorkspace'
import './App.css'

const MAX_ACTIVITY_ENTRIES = 20

export default function App() {
  const [events, setEvents] = useState<EventSummary[]>([])
  const [activeId, setActiveId] = useState<string>(NEW_EVENT_TAB_ID)
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
        setEvents(evs)
        if (evs[0]) setActiveId(evs[0].id)
      })
      .catch(() => setEvents([]))

    const connection = new signalR.HubConnectionBuilder()
      .withUrl('/hubs/occupancy')
      .withAutomaticReconnect()
      .build()

    // Un único socket para todos los eventos (Bloque 18): solo aplica la actualización a pestañas
    // que ya han hecho su carga perezosa inicial (existing !== undefined) — evita reconstruir un
    // aforo a medias para una pestaña que el operador nunca ha abierto.
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

  // Carga perezosa por pestaña: solo se piden zonas la primera vez que se activa un evento — las
  // visitas siguientes reutilizan lo que ya está en zonesByEvent (mantenido fresco por el socket
  // de arriba aunque la pestaña no esté montada).
  useEffect(() => {
    if (activeId === NEW_EVENT_TAB_ID) return
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

  const activeEvent = events.find((ev) => ev.id === activeId)

  return (
    <div className="venue-ops">
      <SiteHeader />

      <EventTabs events={events} activeId={activeId} onSelect={setActiveId} />

      {activeId === NEW_EVENT_TAB_ID || !activeEvent ? (
        <EventCreateForm
          onCreated={(created) => {
            setEvents((prev) => [...prev, created])
            setActiveId(created.id)
          }}
          onCancel={() => setActiveId(events[0]?.id ?? NEW_EVENT_TAB_ID)}
        />
      ) : (
        <EventWorkspace
          key={activeEvent.id}
          event={activeEvent}
          zones={zonesByEvent[activeEvent.id] ?? []}
          activity={activityByEvent[activeEvent.id] ?? []}
          onZonesReload={() => loadZones(activeEvent.id)}
          scanTicket={scanTicket}
          onEventUpdated={updateEvent}
        />
      )}
    </div>
  )
}
