import { useEffect, useRef, useState } from 'react'
import type { EventRevenueDto, TicketStatusUpdate } from '@venue/core/domain'
import { useEventScopedConnection } from '@venue/core/hub'

const REVENUE_POLL_MS = 3000

export function useEventRevenue(eventId: string) {
  const [revenue, setRevenue] = useState<EventRevenueDto | null>(null)
  const [error, setError] = useState<string | null>(null)
  const revenueRef = useRef(revenue)

  useEffect(() => {
    revenueRef.current = revenue
  }, [revenue])

  useEffect(() => {
    let cancelled = false

    async function loadRevenue() {
      try {
        const response = await fetch(`/api/checkin/api/tickets/events/${eventId}/revenue`)
        if (cancelled) return
        if (!response.ok) {
          setError('No se pudieron cargar los ingresos')
          return
        }
        setRevenue((await response.json()) as EventRevenueDto)
        setError(null)
      } catch {
        if (!cancelled) setError('No se pudieron cargar los ingresos')
      }
    }

    void loadRevenue()
    const interval = window.setInterval(loadRevenue, REVENUE_POLL_MS)

    return () => {
      cancelled = true
      window.clearInterval(interval)
    }
  }, [eventId])

  useEventScopedConnection('/hubs/tickets', eventId, (connection) => {
    connection.on('ticketStatusChanged', (update: TicketStatusUpdate) => {
      setRevenue((prev) => {
        if (!prev) return prev

        const price = update.price ?? 0

        if (update.status === 'AwaitingPayment') {
          return {
            ...prev,
            pendingCount: prev.pendingCount + 1,
            pendingRevenue: prev.pendingRevenue + price,
          }
        }

        if (update.status === 'Confirmed') {
          return {
            ...prev,
            pendingCount: Math.max(0, prev.pendingCount - 1),
            pendingRevenue: Math.max(0, prev.pendingRevenue - price),
            confirmedCount: prev.confirmedCount + 1,
            confirmedRevenue: prev.confirmedRevenue + price,
          }
        }

        if (update.status === 'Rejected') {
          return {
            ...prev,
            pendingCount: Math.max(0, prev.pendingCount - 1),
            pendingRevenue: Math.max(0, prev.pendingRevenue - price),
          }
        }

        return prev
      })
    })
  })

  return { revenue, error }
}
