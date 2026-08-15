import type { ZoneOccupancy } from '../domain/zones'
import { createSharedStore } from './createSharedStore'

export interface OccupancyState {
  byEvent: Record<string, ZoneOccupancy[]>
  setZones: (eventId: string, zones: ZoneOccupancy[]) => void
}

export const occupancyStore = createSharedStore<OccupancyState>('occupancy', (set) => ({
  byEvent: {},
  setZones: (eventId, zones) =>
    set((state) => ({
      byEvent: { ...state.byEvent, [eventId]: zones },
    })),
}))
