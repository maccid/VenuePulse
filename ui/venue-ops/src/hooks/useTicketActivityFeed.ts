import { useEffect, useRef, useState } from 'react'
import type { ActivityEntry } from '../shared/types'
import type { TicketStatusUpdate, ZoneOccupancy } from '@venue/core/domain'
import { useEventScopedConnection } from '@venue/core/hub'

const MAX_ACTIVITY_ENTRIES = 20

export function useTicketActivityFeed(activeId: string | null, zonesByEvent: Record<string, ZoneOccupancy[]>) {
  const [activityByEvent, setActivityByEvent] = useState<Record<string, ActivityEntry[]>>({})
  const zonesByEventRef = useRef(zonesByEvent)

  useEffect(() => {
    zonesByEventRef.current = zonesByEvent
  }, [zonesByEvent])

  useEventScopedConnection('/hubs/tickets', activeId, (connection) => {
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
  })

  return activityByEvent
}
