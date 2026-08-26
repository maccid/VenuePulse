import { useEffect, useRef, useState } from 'react'
import type { ActivityEntry } from '../shared/types'
import type { OrderStatusUpdate, TicketStatusUpdate, TicketType, ZoneOccupancy } from '@venue/core/domain'
import { useEventScopedConnection } from '@venue/core/hub'

const MAX_ACTIVITY_ENTRIES = 20
const STORAGE_PREFIX = 'venuepulse:activity:'
const STORAGE_MAX_AGE_MS = 24 * 60 * 60 * 1000
const INDIVIDUAL_DELAY_MS = 1500

function loadFromStorage(eventId: string): ActivityEntry[] {
  try {
    const raw = localStorage.getItem(STORAGE_PREFIX + eventId)
    if (!raw) return []
    const entries: ActivityEntry[] = JSON.parse(raw)
    const cutoff = Date.now() - STORAGE_MAX_AGE_MS
    return entries.filter((e) => new Date(e.atUtc).getTime() > cutoff)
  } catch {
    return []
  }
}

function saveToStorage(eventId: string, entries: ActivityEntry[]) {
  try {
    localStorage.setItem(STORAGE_PREFIX + eventId, JSON.stringify(entries.slice(0, MAX_ACTIVITY_ENTRIES)))
  } catch {
    // storage full or unavailable — silently ignore
  }
}

function buildTicketTypeSummary(
  tickets: { ticketTypeId: string }[],
  ticketTypes: TicketType[],
): string {
  const counts = new Map<string, number>()
  for (const t of tickets) {
    counts.set(t.ticketTypeId, (counts.get(t.ticketTypeId) ?? 0) + 1)
  }

  const parts = Array.from(counts.entries()).map(([typeId, count]) => {
    const name = ticketTypes.find((tt) => tt.id === typeId)?.name ?? typeId.slice(0, 8)
    return `${count}x ${name}`
  })

  return parts.join(', ')
}

export function useTicketActivityFeed(
  activeId: string | null,
  zonesByEvent: Record<string, ZoneOccupancy[]>,
  ticketTypes: TicketType[] = [],
) {
  const [activityByEvent, setActivityByEvent] = useState<Record<string, ActivityEntry[]>>(() => {
    if (!activeId) return {}
    return { [activeId]: loadFromStorage(activeId) }
  })
  const zonesByEventRef = useRef(zonesByEvent)
  const ticketTypesRef = useRef(ticketTypes)
  const pendingTimersRef = useRef<Map<string, ReturnType<typeof setTimeout>>>(new Map())
  const orderTicketIdsRef = useRef<Set<string>>(new Set())

  useEffect(() => {
    zonesByEventRef.current = zonesByEvent
  }, [zonesByEvent])

  useEffect(() => {
    ticketTypesRef.current = ticketTypes
  }, [ticketTypes])

  useEffect(() => {
    return () => {
      for (const timer of pendingTimersRef.current.values()) {
        clearTimeout(timer)
      }
      pendingTimersRef.current.clear()
    }
  }, [])

  function addEntry(eventId: string, entry: ActivityEntry) {
    setActivityByEvent((prev) => {
      const existing = prev[eventId] ?? []
      const next = [entry, ...existing].slice(0, MAX_ACTIVITY_ENTRIES)
      saveToStorage(eventId, next)
      return { ...prev, [eventId]: next }
    })
  }

  useEventScopedConnection('/hubs/tickets', activeId, (connection) => {
    connection.on('ticketStatusChanged', (update: TicketStatusUpdate) => {
      const shouldTrack =
        update.status === 'AwaitingPayment' || update.status === 'Confirmed' || update.status === 'Rejected'
      if (!shouldTrack) return

      const zone = zonesByEventRef.current[update.eventId]?.find((z) => z.zoneId === update.zoneId)
      const entry: ActivityEntry = {
        id: `${update.ticketId}-${update.status}`,
        orderId: null,
        ticketId: update.ticketId,
        ticketCount: 1,
        ticketTypeSummary: null,
        zoneName: zone?.name ?? 'Zona desconocida',
        status: update.status,
        rejectionReason: update.rejectionReason,
        atUtc: new Date().toISOString(),
      }

      const key = update.ticketId
      const existingTimer = pendingTimersRef.current.get(key)
      if (existingTimer) clearTimeout(existingTimer)

      const timer = setTimeout(() => {
        pendingTimersRef.current.delete(key)
        if (!orderTicketIdsRef.current.has(key)) {
          addEntry(update.eventId, entry)
        }
      }, INDIVIDUAL_DELAY_MS)
      pendingTimersRef.current.set(key, timer)
    })

    connection.on('orderStatusChanged', (update: OrderStatusUpdate) => {
      const shouldTrack =
        update.status === 'AwaitingPayment' || update.status === 'Completed' || update.status === 'Rejected'
      if (!shouldTrack) return

      for (const t of update.tickets) {
        orderTicketIdsRef.current.add(t.ticketId)
        const timer = pendingTimersRef.current.get(t.ticketId)
        if (timer) {
          clearTimeout(timer)
          pendingTimersRef.current.delete(t.ticketId)
        }
      }

      const firstTicket = update.tickets[0]
      const zone = firstTicket
        ? zonesByEventRef.current[update.eventId]?.find((z) => z.zoneId === firstTicket.zoneId)
        : null

      const resolvedStatus = update.status === 'Completed' ? 'Confirmed' : update.status === 'Rejected' ? 'Rejected' : 'AwaitingPayment'

      const entry: ActivityEntry = {
        id: `order-${update.orderId}-${update.status}`,
        orderId: update.orderId,
        ticketId: null,
        ticketCount: update.tickets.length,
        ticketTypeSummary: buildTicketTypeSummary(update.tickets, ticketTypesRef.current),
        zoneName: zone?.name ?? 'Zona desconocida',
        status: resolvedStatus,
        rejectionReason: update.rejectionReason,
        atUtc: new Date().toISOString(),
      }

      setActivityByEvent((prev) => {
        const existing = prev[update.eventId] ?? []
        const orderIds = new Set(update.tickets.map((t) => t.ticketId))
        const filtered = existing.filter((e) => e.ticketId === null || !orderIds.has(e.ticketId))
        const next = [entry, ...filtered].slice(0, MAX_ACTIVITY_ENTRIES)
        saveToStorage(update.eventId, next)
        return { ...prev, [update.eventId]: next }
      })
    })
  })

  return activityByEvent
}
