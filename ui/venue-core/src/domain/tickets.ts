import type { PriceTier } from './ticketing'

export type TicketStatus =
  | 'Pending'
  | 'AwaitingPayment'
  | 'ProcessingPayment'
  | 'Confirmed'
  | 'Rejected'
  | 'CheckedIn'
  | 'Refunded'

export interface TicketStatusUpdate {
  ticketId: string
  eventId: string
  zoneId: string
  status: TicketStatus
  rejectionReason: string | null
  expiresAtUtc: string | null
}

export type PaymentMethod = 'Card' | 'Bizum'

export interface BuyerData {
  nombre: string
  apellidos: string
  email: string
}

export interface CartLine {
  ticketId: string
  zoneId: string
  zoneName: string
  ticketTypeName: string
  unitPrice: number
  priceTier: PriceTier
  status: TicketStatus
  rejectionReason: string | null
  expiresAtUtc: string | null
}

export interface DraftLine {
  zoneId: string
  zoneName: string
  ticketTypeId: string
  ticketTypeName: string
  quantity: number
}

export interface EventRevenueDto {
  eventId: string
  confirmedRevenue: number
  confirmedCount: number
  pendingRevenue: number
  pendingCount: number
}
