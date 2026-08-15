import type { TicketStatus } from './tickets'

export type OrderStatus = 'Reserving' | 'AwaitingPayment' | 'Confirming' | 'Completed' | 'Rejected'

export interface OrderLine {
  ticketTypeId: string
  zoneId: string
  quantity: number
  unitPrice: number
}

export interface OrderTicketStatus {
  ticketId: string
  zoneId: string
  ticketTypeId: string
  unitPrice: number
  status: TicketStatus
  rejectionReason: string | null
  expiresAtUtc: string | null
}

export interface OrderStatusUpdate {
  orderId: string
  eventId: string
  status: OrderStatus
  rejectionReason: string | null
  tickets: OrderTicketStatus[]
}

export function isResolvedOrderStatus(status: OrderStatus | null): boolean {
  return status === 'Completed' || status === 'Rejected'
}
