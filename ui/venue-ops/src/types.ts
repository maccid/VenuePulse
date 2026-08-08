export interface ZoneOccupancy {
  zoneId: string
  name: string
  capacity: number
  confirmedSold: number
  checkedIn: number
}

export type ScanResult = { kind: 'idle' } | { kind: 'ok' } | { kind: 'error'; message: string }
