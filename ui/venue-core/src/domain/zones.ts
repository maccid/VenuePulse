export interface ZoneOccupancy {
  zoneId: string
  eventId: string
  name: string
  capacity: number
  availableToSell: number
  confirmedSold: number
  checkedIn: number
  cancelled: boolean
}

export type OccupancyMetric = 'sales' | 'checkin'

export function occupancyRatio(zone: ZoneOccupancy, metric: OccupancyMetric = 'sales'): number {
  if (zone.capacity === 0) return 0
  if (metric === 'checkin') return Math.min(1, Math.max(0, zone.checkedIn / zone.capacity))
  return Math.min(1, Math.max(0, (zone.capacity - zone.availableToSell) / zone.capacity))
}

export type OccupancyTier = 'low' | 'mid' | 'high'

export function occupancyTier(ratio: number): OccupancyTier {
  if (ratio >= 0.9) return 'high'
  if (ratio >= 0.7) return 'mid'
  return 'low'
}
