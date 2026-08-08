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

export type SimulationMode = 'Sales' | 'Arrivals'

export interface SimulationRunStatus {
  eventId: string
  mode: SimulationMode
  running: boolean
  progress: number
  virtualNowUtc: string
}

export interface SimulationStartRequest {
  mode: SimulationMode
  eventStartsAtUtc: string
  durationSeconds: number
  targetSoldPct: number
  noShowPct: number
}
