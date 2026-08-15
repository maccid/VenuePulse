import { useEffect, useState } from 'react'
import type { EventSummary } from '@venue/core/domain'

const EVENT_PARAM = 'event'
const ADD_PARAM = 'add'

function readUrl(): { eventId: string | null; adding: boolean } {
  const params = new URLSearchParams(window.location.search)
  return { eventId: params.get(EVENT_PARAM), adding: params.has(ADD_PARAM) }
}

function writeUrl(eventId: string | null, adding: boolean) {
  const url = new URL(window.location.href)
  if (eventId) url.searchParams.set(EVENT_PARAM, eventId)
  else url.searchParams.delete(EVENT_PARAM)
  if (adding) url.searchParams.set(ADD_PARAM, '')
  else url.searchParams.delete(ADD_PARAM)
  window.history.pushState({}, '', url)
}

export function useEventCatalog() {
  const [events, setEvents] = useState<EventSummary[]>([])
  const [activeId, setActiveId] = useState<string | null>(() => readUrl().eventId)
  const [showCreate, setShowCreate] = useState(() => readUrl().adding)

  useEffect(() => {
    fetch('/api/ticketing/api/events')
      .then((r) => r.json())
      .then((evs: EventSummary[]) => {
        const sorted = [...evs].sort((a, b) => b.startsAtUtc.localeCompare(a.startsAtUtc))
        setEvents(sorted)
        if (readUrl().eventId || readUrl().adding) return
        if (sorted[0]) setActiveId(sorted[0].id)
        else setShowCreate(true)
      })
      .catch(() => setEvents([]))
  }, [])

  useEffect(() => {
    const handlePopState = () => {
      const { eventId, adding } = readUrl()
      setActiveId(eventId)
      setShowCreate(adding)
    }
    window.addEventListener('popstate', handlePopState)
    return () => window.removeEventListener('popstate', handlePopState)
  }, [])

  function selectEvent(id: string) {
    setActiveId(id)
    setShowCreate(false)
    writeUrl(id, false)
  }

  function startCreate() {
    setActiveId(null)
    setShowCreate(true)
    writeUrl(null, true)
  }

  function cancelCreate() {
    setShowCreate(false)
    writeUrl(activeId, false)
  }

  function updateEvent(updated: EventSummary) {
    setEvents((prev) => prev.map((ev) => (ev.id === updated.id ? updated : ev)))
  }

  function addEvent(event: EventSummary) {
    setEvents((prev) => [...prev, event])
  }

  return {
    events,
    activeId,
    showCreate,
    selectEvent,
    startCreate,
    cancelCreate,
    updateEvent,
    addEvent,
  }
}
