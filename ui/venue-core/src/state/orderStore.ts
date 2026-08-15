import type { OrderStatus, OrderTicketStatus } from '../domain/orders'
import type { TicketStatus } from '../domain/tickets'
import { createSharedStore } from './createSharedStore'

export interface OrderState {
  orderId: string | null
  status: OrderStatus | null
  rejectionReason: string | null
  tickets: OrderTicketStatus[]
  setOrder: (patch: Partial<Pick<OrderState, 'orderId' | 'status' | 'rejectionReason' | 'tickets'>>) => void
  updateTicketStatus: (
    ticketId: string,
    status: TicketStatus,
    rejectionReason: string | null,
    expiresAtUtc: string | null,
  ) => void
  reset: () => void
}

export const orderStore = createSharedStore<OrderState>('order', (set) => ({
  orderId: null,
  status: null,
  rejectionReason: null,
  tickets: [],
  setOrder: (patch) => set(patch),
  updateTicketStatus: (ticketId, status, rejectionReason, expiresAtUtc) =>
    set((state) => ({
      tickets: state.tickets.map((ticket) =>
        ticket.ticketId === ticketId ? { ...ticket, status, rejectionReason, expiresAtUtc } : ticket,
      ),
    })),
  reset: () => set({ orderId: null, status: null, rejectionReason: null, tickets: [] }),
}))
