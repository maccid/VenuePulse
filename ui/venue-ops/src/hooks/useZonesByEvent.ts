import { useEffect, useRef, useState } from 'react'
import type { ZoneOccupancy } from '../shared/types'
import { createHubConnection } from '../shared/hubConnection'

export function useZonesByEvent(activeId: string | null) {
  const [zonesByEvent, setZonesByEvent] = useState<Record<string, ZoneOccupancy[]>>({})
  const zonesByEventRef = useRef(zonesByEvent)

  useEffect(() => {
    zonesByEventRef.current = zonesByEvent
  }, [zonesByEvent])

  useEffect(() => {
    const connection = createHubConnection('/hubs/occupancy')

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

    connection.start().catch((err) => console.error('No se pudo conectar al hub de aforo', err))

    return () => {
      connection.stop()
    }
  }, [])

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
