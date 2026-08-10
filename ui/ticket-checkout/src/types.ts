export interface TicketType {
  id: string
  eventId: string
  name: string
  price: number
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

// "AwaitingPayment" es un estado real del Ticket (no solo interno de la Saga, ver ADR de la
// partición de la Saga): el aforo ya está reservado de verdad y se espera a que el cliente elija
// método de pago.
export type TicketStatus = 'Pending' | 'AwaitingPayment' | 'Confirmed' | 'Rejected' | 'CheckedIn'

export interface TicketStatusUpdate {
  ticketId: string
  status: TicketStatus
  rejectionReason: string | null
}

export type PaymentMethod = 'Card' | 'Bizum'

// Límite de entradas por compra — no es una limitación real del backend (cada entrada es una
// petición independiente), es una barrera de producto para no dejar vaciar una zona entera de
// una sentada en la demo.
export const MAX_TICKETS_PER_PURCHASE = 10

// Cada línea del carrito es una entrada real ya reservada en el backend (no una intención local
// sin bloquear) — "añadir al carrito" dispara ya la compra real, ver ADR del carrito.
export interface CartLine {
  ticketId: string
  zoneId: string
  zoneName: string
  ticketTypeName: string
  unitPrice: number
  priceTier: string
  status: TicketStatus
  rejectionReason: string | null
}
