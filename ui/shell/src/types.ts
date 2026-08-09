export type EventType = 'Match' | 'Concert'

export interface EventSummary {
  id: string
  name: string
  venueName: string
  startsAtUtc: string
  type: EventType
}
