export type PriceTier = 'Demanda baja' | 'Normal' | 'Demanda alta' | 'Últimas entradas'

export interface TicketType {
  id: string
  eventId: string
  name: string
  price: number
  basePrice: number
  description: string | null
  effectivePrice: number
  priceTier: PriceTier
}

export interface PricingChanged {
  eventId: string
  ticketTypes: TicketType[]
}
