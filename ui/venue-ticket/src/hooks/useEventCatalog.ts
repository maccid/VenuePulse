import { useEffect, useState } from 'react'
import type { EventSummary, PricingChanged, TicketType, ZoneOccupancy } from '@venue/core/domain'
import { isAbortedHubStartError, createHubConnection } from '@venue/core/hub'

interface EventCatalog {
  event: EventSummary | undefined
  ticketTypes: TicketType[]
  zones: ZoneOccupancy[]
}

export function useEventCatalog(eventId: string): EventCatalog {
  const [event, setEvent] = useState<EventSummary | undefined>(undefined)
  const [ticketTypes, setTicketTypes] = useState<TicketType[]>([])
  const [zones, setZones] = useState<ZoneOccupancy[]>([])

  useEffect(() => {
    fetch('/api/ticketing/api/events')
      .then((r) => r.json())
      .then((events: EventSummary[]) => setEvent(events.find((e) => e.id === eventId)))
      .catch(() => setEvent(undefined))

    fetch(`/api/ticketing/api/events/${eventId}/ticket-types`)
      .then((r) => r.json())
      .then(setTicketTypes)
      .catch(() => setTicketTypes([]))

    fetch(`/api/access-control/api/zones?eventId=${eventId}`)
      .then((r) => r.json())
      .then(setZones)
      .catch(() => setZones([]))
  }, [eventId])

  useEffect(() => {
    const connection = createHubConnection('/hubs/occupancy')

    connection.on('occupancyChanged', (updated: ZoneOccupancy) => {
      setZones((prev) => prev.map((z) => (z.zoneId === updated.zoneId ? updated : z)))
    })

    async function joinEvent() {
      await connection.invoke('JoinEvent', eventId).catch(() => undefined)
    }

    connection.onreconnected(joinEvent)
    connection.start().then(joinEvent).catch((err) => {
      if (!isAbortedHubStartError(err)) console.error('No se pudo conectar al hub de aforo', err)
    })

    return () => {
      connection.stop()
    }
  }, [eventId])

  useEffect(() => {
    const connection = createHubConnection('/hubs/pricing')

    connection.on('pricingChanged', (updated: PricingChanged) => {
      if (updated.eventId === eventId) setTicketTypes(updated.ticketTypes)
    })

    async function joinEvent() {
      await connection.invoke('JoinEvent', eventId).catch(() => undefined)
    }

    connection.onreconnected(joinEvent)
    connection.start().then(joinEvent).catch((err) => {
      if (!isAbortedHubStartError(err)) console.error('No se pudo conectar al hub de precios', err)
    })

    return () => {
      connection.stop()
    }
  }, [eventId])

  return { event, ticketTypes, zones }
}
