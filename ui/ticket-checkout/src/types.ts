export type EventType = 'Match' | 'Concert'
export type EventStatus = 'OnSale' | 'SalesClosed' | 'Cancelled'

// La cabecera necesita el nombre/categoría/fecha del evento, que el Web Component no recibe por
// atributo — se obtiene con el GET /api/events existente y se busca por id (sin endpoint nuevo,
// ver ADR 0023). El Status decide si el flujo de compra se muestra o se sustituye por un aviso.
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
  // Precio de catálogo puro (sin relevancia ni demanda, ver ADR 0019).
  price: number
  // Precio de catálogo con el multiplicador de relevancia aplicado (p. ej. x1.5 en eventos
  // especiales) — es el precio de referencia antes de la heurística de demanda.
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

// Selección local, todavía sin reservar de verdad (sin llamar al backend) — se acumula por
// zona+tipo mientras el título de la tarjeta de compra es "Tus Reservas". Solo al pulsar
// "Reservar" cada unidad se convierte en un `CartLine` real (ver ADR 0025, reabre ADR 0018 punto 3).
export interface DraftLine {
  zoneId: string
  zoneName: string
  ticketTypeId: string
  ticketTypeName: string
  unitPrice: number
  priceTier: string
  quantity: number
}

// Cada línea del carrito es una entrada real ya reservada en el backend a partir del momento en
// que se crea (no una intención local) — pulsar "Reservar" dispara ya la compra real para cada
// `DraftLine`, ver ADR 0025.
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
