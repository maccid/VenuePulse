import { createSharedStore } from './createSharedStore'

export interface EventSelectionState {
  eventId: string | null
  selectEvent: (eventId: string | null) => void
}

export const eventSelectionStore = createSharedStore<EventSelectionState>('eventSelection', (set) => ({
  eventId: null,
  selectEvent: (eventId) => set({ eventId }),
}))
