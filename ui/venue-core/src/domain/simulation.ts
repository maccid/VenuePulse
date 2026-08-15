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
