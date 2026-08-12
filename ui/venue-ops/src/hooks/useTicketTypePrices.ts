import { useEffect, useState } from 'react'
import type { TicketType } from '../shared/types'

const PRICE_POLL_MS = 3000

export function useTicketTypePrices(eventId: string) {
  const [ticketTypes, setTicketTypes] = useState<TicketType[]>([])

  useEffect(() => {
    let cancelled = false

    async function loadTicketTypes() {
      try {
        const response = await fetch(`/api/ticketing/api/events/${eventId}/ticket-types`)
        if (cancelled || !response.ok) return
        setTicketTypes((await response.json()) as TicketType[])
      } catch {
        return
      }
    }

    void loadTicketTypes()
    const interval = window.setInterval(loadTicketTypes, PRICE_POLL_MS)

    return () => {
      cancelled = true
      window.clearInterval(interval)
    }
  }, [eventId])

  return { ticketTypes }
}
