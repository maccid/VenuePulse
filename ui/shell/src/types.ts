export type EventType = 'Match' | 'Concert'
export type EventStatus = 'OnSale' | 'SalesClosed' | 'Cancelled'

export interface EventSummary {
  id: string
  name: string
  venueName: string
  startsAtUtc: string
  type: EventType
  status: EventStatus
  relevance: 'Standard' | 'Special'
}
