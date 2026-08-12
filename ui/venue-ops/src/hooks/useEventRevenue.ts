import { useEffect, useState } from 'react'
import type { EventRevenueDto } from '../shared/types'

const REVENUE_POLL_MS = 3000

export function useEventRevenue(eventId: string) {
  const [revenue, setRevenue] = useState<EventRevenueDto | null>(null)
  const [error, setError] = useState<string | null>(null)

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

  return { revenue, error }
}
