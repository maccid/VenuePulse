import { useEffect, useRef, useState } from 'react'
import type { ActivityEntry, TicketStatusUpdate, ZoneOccupancy } from '../shared/types'
import { createHubConnection } from '../lib/hubConnection'

const MAX_ACTIVITY_ENTRIES = 20

export function useTicketActivityFeed(zonesByEvent: Record<string, ZoneOccupancy[]>) {
  const [activityByEvent, setActivityByEvent] = useState<Record<string, ActivityEntry[]>>({})
  const zonesByEventRef = useRef(zonesByEvent)

  useEffect(() => {
    zonesByEventRef.current = zonesByEvent
  }, [zonesByEvent])

  useEffect(() => {
    const connection = createHubConnection('/hubs/tickets')

    connection.on('ticketStatusChanged', (update: TicketStatusUpdate) => {
      const shouldTrack =
        update.status === 'AwaitingPayment' || update.status === 'Confirmed' || update.status === 'Rejected'
      if (!shouldTrack) return

      const zone = zonesByEventRef.current[update.eventId]?.find((z) => z.zoneId === update.zoneId)
      const entry: ActivityEntry = {
        id: `${update.ticketId}-${update.status}`,
        zoneName: zone?.name ?? 'Zona desconocida',
        status: update.status,
        rejectionReason: update.rejectionReason,
        atUtc: new Date().toISOString(),
      }

      setActivityByEvent((prev) => {
        const existing = prev[update.eventId] ?? []
        return { ...prev, [update.eventId]: [entry, ...existing].slice(0, MAX_ACTIVITY_ENTRIES) }
      })
    })

    connection.start().catch((err) => console.error('No se pudo conectar al hub de entradas', err))

    return () => {
      connection.stop()
    }
  }, [])

  return activityByEvent
}
