import { occupancyRatio } from './zones'
import type { OccupancyMetric, ZoneOccupancy } from './zones'

export type OccupancyStatus = 'OPEN' | 'LIMITED' | 'NEAR_FULL' | 'FULL' | 'CLOSED'

export function occupancyStatusFor(zone: ZoneOccupancy, metric: OccupancyMetric = 'sales'): OccupancyStatus {
  if (zone.cancelled) return 'CLOSED'
  const ratio = occupancyRatio(zone, metric)
  if (ratio >= 0.9) return 'FULL'
  if (ratio >= 0.7) return 'NEAR_FULL'
  if (ratio >= 0.4) return 'LIMITED'
  return 'OPEN'
}

export type SeatStatus = 'FREE' | 'LOCKED' | 'RESERVED' | 'SOLD' | 'DISABLED'

export interface Seat {
  id: string
  row: string
  number: number
  status: SeatStatus
}
