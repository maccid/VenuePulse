import type { TicketType, ZoneOccupancy } from './types'

// Fusiona zona + tipo de entrada por coincidencia de nombre (p. ej. "Grada VIP" -> tipo "VIP",
// "Pista" -> tipo "Pista"), sin relación real en el backend — Zone (AccessControl) y TicketType
// (Ticketing) siguen siendo bounded contexts separados (ver ADR 0016). Los nombres de zona se
// eligen a propósito para que esta coincidencia funcione siempre; si ninguna coincide, cae al
// tipo "General" y si tampoco existe, al primero disponible.
export function matchTicketType(zoneName: string, ticketTypes: TicketType[]): TicketType | undefined {
  return (
    ticketTypes.find((t) => t.name === zoneName) ??
    ticketTypes.find((t) => t.name === 'General') ??
    ticketTypes[0]
  )
}

export function pricingByZoneId(
  zones: ZoneOccupancy[],
  ticketTypes: TicketType[],
): Record<string, { price: number; effectivePrice: number; priceTier: string }> {
  const result: Record<string, { price: number; effectivePrice: number; priceTier: string }> = {}

  for (const zone of zones) {
    const ticketType = matchTicketType(zone.name, ticketTypes)
    if (ticketType) {
      result[zone.zoneId] = {
        price: ticketType.price,
        effectivePrice: ticketType.effectivePrice,
        priceTier: ticketType.priceTier,
      }
    }
  }

  return result
}
