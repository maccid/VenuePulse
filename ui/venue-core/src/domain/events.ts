export type EventType = 'Match' | 'Concert'
export type EventStatus = 'OnSale' | 'SalesClosed' | 'Cancelled'
export type EventRelevance = 'Standard' | 'Special'

export interface EventSummary {
  id: string
  name: string
  venueName: string
  startsAtUtc: string
  type: EventType
  status: EventStatus
  relevance: EventRelevance
}

export interface CreateEventRequest {
  name: string
  startsAtUtc: string
  type: EventType
  relevance: EventRelevance
}

export function eventTypeLabel(type: EventType): string {
  const labels: Record<EventType, string> = { Match: 'Partido', Concert: 'Concierto' }
  return labels[type]
}

export function formatEventDateTime(
  startsAtUtc: string,
): { day: number; month: string; weekday: string; time: string } {
  const date = new Date(startsAtUtc)
  return {
    day: date.getDate(),
    month: date.toLocaleDateString('es-ES', { month: 'short' }).replace('.', '').toUpperCase(),
    weekday: date.toLocaleDateString('es-ES', { weekday: 'short' }).replace('.', ''),
    time: date.toLocaleTimeString('es-ES', { hour: '2-digit', minute: '2-digit' }),
  }
}
