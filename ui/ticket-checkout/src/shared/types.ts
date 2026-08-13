export type EventType = 'Match' | 'Concert'
export type EventStatus = 'OnSale' | 'SalesClosed' | 'Cancelled'

export interface EventSummary {
  id: string
  name: string
  venueName: string
  startsAtUtc: string
  type: EventType
  status: EventStatus
  relevance: 'Standard' | 'Special'
}

export interface TicketType {
  id: string
  eventId: string
  name: string
  price: number
  basePrice: number
  description: string | null
  effectivePrice: number
  priceTier: string
}

export interface PricingChanged {
  eventId: string
  ticketTypes: TicketType[]
}

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

export type TicketStatus = 'Pending' | 'AwaitingPayment' | 'ProcessingPayment' | 'Confirmed' | 'Rejected' | 'CheckedIn'

export interface TicketStatusUpdate {
  ticketId: string
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

export const MAX_TICKETS_PER_PURCHASE = 10

export interface DraftLine {
  zoneId: string
  zoneName: string
  ticketTypeId: string
  ticketTypeName: string
  quantity: number
}

export interface CartLine {
  ticketId: string
  zoneId: string
  zoneName: string
  ticketTypeName: string
  unitPrice: number
  priceTier: string
  status: TicketStatus
  rejectionReason: string | null
  expiresAtUtc: string | null
}

export interface OrderLine {
  ticketTypeId: string
  zoneId: string
  quantity: number
  unitPrice: number
}

export type OrderStatus = 'Reserving' | 'AwaitingPayment' | 'Confirming' | 'Completed' | 'Rejected'

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
