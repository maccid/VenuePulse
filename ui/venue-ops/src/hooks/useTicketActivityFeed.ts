import { useEffect, useRef, useState } from 'react'
import type { ActivityEntry } from '../shared/types'
import type { OrderStatusUpdate, TicketStatusUpdate, ZoneOccupancy } from '@venue/core/domain'
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
        orderId: null,
        ticketCount: 1,
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

    connection.on('orderStatusChanged', (update: OrderStatusUpdate) => {
      const shouldTrack =
        update.status === 'AwaitingPayment' || update.status === 'Completed' || update.status === 'Rejected'
      if (!shouldTrack) return

      const firstTicket = update.tickets[0]
      const zone = firstTicket
        ? zonesByEventRef.current[update.eventId]?.find((z) => z.zoneId === firstTicket.zoneId)
        : null

      const resolvedStatus = update.status === 'Completed' ? 'Confirmed' : update.status === 'Rejected' ? 'Rejected' : 'AwaitingPayment'

      const entry: ActivityEntry = {
        id: `order-${update.orderId}-${update.status}`,
        orderId: update.orderId,
        ticketCount: update.tickets.length,
        zoneName: zone?.name ?? 'Zona desconocida',
        status: resolvedStatus,
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
