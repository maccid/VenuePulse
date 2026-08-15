import { useEffect, useRef, useState } from 'react'
import type { ZoneOccupancy } from '../shared/types'
import { useEventScopedConnection } from '@venue/core'

export function useZonesByEvent(activeId: string | null) {
  const [zonesByEvent, setZonesByEvent] = useState<Record<string, ZoneOccupancy[]>>({})
  const zonesByEventRef = useRef(zonesByEvent)

  useEffect(() => {
    zonesByEventRef.current = zonesByEvent
  }, [zonesByEvent])

  useEventScopedConnection('/hubs/occupancy', activeId, (connection) => {
    connection.on('occupancyChanged', (updated: ZoneOccupancy) => {
      setZonesByEvent((prev) => {
        const existing = prev[updated.eventId]
        if (existing === undefined) return prev

        const hasZone = existing.some((z) => z.zoneId === updated.zoneId)
        const next = hasZone
          ? existing.map((z) => (z.zoneId === updated.zoneId ? updated : z))
          : [...existing, updated]

        return { ...prev, [updated.eventId]: next }
      })
    })
  })

  function loadZones(eventId: string) {
    fetch(`/api/access-control/api/zones?eventId=${eventId}`)
      .then((r) => r.json())
      .then((zones: ZoneOccupancy[]) => setZonesByEvent((prev) => ({ ...prev, [eventId]: zones })))
      .catch(() => setZonesByEvent((prev) => ({ ...prev, [eventId]: [] })))
  }

  useEffect(() => {
    if (activeId === null) return
    if (zonesByEventRef.current[activeId] !== undefined) return
    loadZones(activeId)
  }, [activeId])

  return { zonesByEvent, loadZones }
}
