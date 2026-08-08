export interface EventSummary {
  id: string
  name: string
  venueName: string
  startsAtUtc: string
}

export interface ZoneOccupancy {
  zoneId: string
  eventId: string
  name: string
  capacity: number
  availableToSell: number
  confirmedSold: number
  checkedIn: number
}

export type ScanResult = { kind: 'idle' } | { kind: 'ok' } | { kind: 'error'; message: string }
