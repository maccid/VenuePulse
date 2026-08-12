import { useState } from 'react'
import type { CreateEventRequest, EventSummary } from '../shared/types'

export function useCreateEvent(onCreated: (event: EventSummary) => void) {
  const [error, setError] = useState<string | null>(null)
  const [busy, setBusy] = useState(false)

  async function create(request: CreateEventRequest): Promise<boolean> {
    setError(null)
    setBusy(true)
    try {
      const response = await fetch('/api/ticketing/api/events', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(request),
      })
      if (!response.ok) {
        setError('No se pudo crear el evento.')
        return false
      }
      onCreated((await response.json()) as EventSummary)
      return true
    } catch {
      setError('No se pudo crear el evento.')
      return false
    } finally {
      setBusy(false)
    }
  }

  return { create, error, busy }
}
