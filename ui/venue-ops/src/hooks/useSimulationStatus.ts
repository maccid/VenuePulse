import { useEffect, useRef, useState } from 'react'
import type { SimulationRunStatus } from '../shared/types'

const STATUS_POLL_MS = 500

export function useSimulationStatus(eventId: string, onSimulationEnded: () => void) {
  const [status, setStatus] = useState<SimulationRunStatus | null>(null)
  const statusRef = useRef(status)
  const onSimulationEndedRef = useRef(onSimulationEnded)

  useEffect(() => {
    statusRef.current = status
  }, [status])

  useEffect(() => {
    onSimulationEndedRef.current = onSimulationEnded
  }, [onSimulationEnded])

  useEffect(() => {
    if (eventId === '') {
      setStatus(null)
      return
    }

    let cancelled = false
    const interval = window.setInterval(async () => {
      const response = await fetch(`/api/access-control/api/simulation/events/${eventId}/status`)
      if (cancelled || !response.ok) return

      const current = (await response.json().catch(() => null)) as SimulationRunStatus | null
      const wasRunning = statusRef.current?.running === true
      const justEnded = wasRunning && !current?.running
      setStatus(current)
      if (justEnded) {
        onSimulationEndedRef.current()
      }
    }, STATUS_POLL_MS)

    return () => {
      cancelled = true
      window.clearInterval(interval)
    }
  }, [eventId])

  const running = status?.running === true
  const progressPct = Math.round((status?.progress ?? 0) * 100)

  return { status, running, progressPct }
}
