import { useEffect, useState } from 'react'
import type { EventSummary } from '@venue/core/domain'

export function useEventCatalog() {
  const [events, setEvents] = useState<EventSummary[]>([])
  const [activeId, setActiveId] = useState<string | null>(null)
  const [showCreate, setShowCreate] = useState(false)

  useEffect(() => {
    fetch('/api/ticketing/api/events')
      .then((r) => r.json())
      .then((evs: EventSummary[]) => {
        const sorted = [...evs].sort((a, b) => b.startsAtUtc.localeCompare(a.startsAtUtc))
        setEvents(sorted)
        if (sorted[0]) setActiveId(sorted[0].id)
        else setShowCreate(true)
      })
      .catch(() => setEvents([]))
  }, [])

  function updateEvent(updated: EventSummary) {
    setEvents((prev) => prev.map((ev) => (ev.id === updated.id ? updated : ev)))
  }

  function addEvent(event: EventSummary) {
    setEvents((prev) => [...prev, event])
  }

  return { events, activeId, setActiveId, showCreate, setShowCreate, updateEvent, addEvent }
}
